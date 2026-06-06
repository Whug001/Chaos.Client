#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Controls.World.Popups.Dialog;
using Chaos.Client.Definitions;
using Chaos.Client.Extensions;
using Chaos.Client.Models;
using Chaos.Networking.Entities.Client;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
#endregion

namespace Chaos.Client.Controls.World.Popups.Market;

/// <summary>
///     The market / auction-house window. Reuses the ornate dialog frame (<see cref="FramedDialogPanelBase" />) and
///     hosts a 3-tab nav (Search / Results / Sell) over swappable page controls. Mounted on WorldScreen.Root,
///     toggled via a debug hotkey (F12) for now until the NPC entry point is wired.
/// </summary>
/// <remarks>
///     There is no DAT-backed <c>_market</c> control file (prefabs resolve only from the Setoa/Cious archives — see
///     <c>UiComponentRepository.LoadPrefabSet</c> — so a new one cannot be authored without game-data tooling). We reuse
///     the existing <c>_nsett</c> prefab purely to satisfy the non-null prefab requirement and inherit its <c>"OK"</c>
///     button, then override <see cref="PrefabPanel.Width" />/<see cref="PrefabPanel.Height" /> to the canonical Market
///     dimensions. The frame art is drawn programmatically by the base from the
///     live Width/Height, so the override yields the exact intended size.
/// </remarks>
public sealed class MarketControl : FramedDialogPanelBase, IInventoryDropTarget
{
    //canonical market panel size (the visual spec).
    private const int PANEL_WIDTH = 560;
    private const int PANEL_HEIGHT = 300;

    //the panel is centered horizontally but pinned this far from the top of the screen (not vertically centered).
    private const int TOP_MARGIN = 15;

    private const int OK_RIGHT_MARGIN = 20;
    private const int OK_BOTTOM_MARGIN = 3;

    //frame insets: usable content begins after the ornate border.
    private const int CONTENT_LEFT = 22;
    private const int CONTENT_TOP = 28;
    private const int CONTENT_RIGHT = 22;
    private const int FRAME_BOTTOM = 47;

    //tab strip metrics: tabs sit along the top of the content area using the nd_mtab.spf frame.
    private const int TAB_WIDTH = 60;
    private const int TAB_HEIGHT = 16;
    private const int TAB_STRIP_TOP = 8;

    private static readonly (MarketTab Tab, string Caption)[] TabDefs =
    [
        (MarketTab.Search, "Search"),
        (MarketTab.Results, "Results"),
        (MarketTab.Sell, "Sell"),
        (MarketTab.Logs, "Logs")
    ];

    private readonly TabButton[] Tabs;
    private readonly UIElement[] TabPages; //one page per tab, built in BuildPage

    private MarketTab Current = MarketTab.Search;
    private MarketLogsControl? LogsPage;
    private MarketResultsControl? ResultsPage;
    private MarketSellControl? SellPage;

    public MarketControl()
        : base("_nsett", false)
    {
        Name = "Market";
        Visible = false;
        UsesControlStack = true; //inherited Show/Hide push/pop the InputDispatcher stack

        //override the borrowed prefab's anchor size with the intended market dimensions, then position it:
        //horizontally centered, pinned ~15px from the top of the screen (CenterOnScreen sets both, then override Y).
        Width = PANEL_WIDTH;
        Height = PANEL_HEIGHT;
        this.CenterOnScreen();
        Y = TOP_MARGIN;

        OkButton = CreateButton("OK"); //the prefab's button (correctly sized + wired into the frame); re-skinned below

        if (OkButton is not null)
        {
            //re-skin the inherited "Ok" button as "Close" (_nbtn.spf frame 0 normal / 1 pressed). It already just hides
            //the panel; clear the hover/selected/disabled states (they still pointed at the prefab's "Ok" art) so only
            //the Close normal + pressed frames ever show.
            OkButton.NormalTexture = UiRenderer.Instance!.GetSpfTexture("_nbtn.spf");
            OkButton.PressedTexture = UiRenderer.Instance!.GetSpfTexture("_nbtn.spf", 1);
            OkButton.HoverTexture = null;
            OkButton.SelectedTexture = null;
            OkButton.DisabledTexture = null;

            OkButton.Clicked += Hide;
            OkButton.X = Width - OkButton.Width - OK_RIGHT_MARGIN;
            OkButton.Y = Height - OkButton.Height - OK_BOTTOM_MARGIN;
        }

        //the tab strip is centered at the top; a separator rests directly beneath it (tabs sit on top, no gap).
        var tabStripWidth = TabDefs.Length * TAB_WIDTH;
        var tabStripX = (Width - tabStripWidth) / 2;
        var tabStripBottom = TAB_STRIP_TOP + TAB_HEIGHT;

        //separator added before the tabs so the tabs draw on top of it at the shared seam; spans 80% of the width, centered.
        var separatorWidth = Width * 4 / 5;

        AddChild(
            new CustomSeparator(SeparatorOrientation.Horizontal, separatorWidth)
            {
                X = (Width - separatorWidth) / 2,
                Y = tabStripBottom
            });

        //build the 3-tab nav strip from the shared nd_mtab.spf frame (normal = frame 0, selected = frame 1).
        var normalBg = UiRenderer.Instance!.GetSpfTexture("nd_mtab.spf");
        var selectedBg = UiRenderer.Instance!.GetSpfTexture("nd_mtab.spf", 1);

        Tabs = new TabButton[TabDefs.Length];

        for (var i = 0; i < TabDefs.Length; i++)
        {
            var tab = new TabButton(normalBg, selectedBg, TabDefs[i].Caption)
            {
                X = tabStripX + i * TAB_WIDTH,
                Y = TAB_STRIP_TOP,
                Width = TAB_WIDTH,
                Height = TAB_HEIGHT
            };

            var captured = TabDefs[i].Tab;
            tab.Clicked += () => ShowTab(captured);
            Tabs[i] = tab;
            AddChild(tab);
        }

        //build the pages: every tab now hosts a real page control sized to the shared content rect.
        var pageRect = ContentRect();
        TabPages = new UIElement[TabDefs.Length];

        for (var i = 0; i < TabDefs.Length; i++)
        {
            var page = BuildPage(TabDefs[i].Tab, pageRect);
            page.Visible = false;

            TabPages[i] = page;
            AddChild(page);
        }

        ShowTab(MarketTab.Search);
    }

    /// <summary>Raised when the user clicks Search; carries the search criteria so the networking layer can issue a request.</summary>
    public event Action<MarketSearchCriteria>? SearchRequested;

    /// <summary>Raised when the user confirms a buy on the Results tab; carries the chosen listing and quantity. The screen
    /// owns the buy-confirm popup (it must mount on the root, above this clipped panel), so it subscribes here.</summary>
    public event Action<MarketListing, int>? BuyRequested;

    /// <summary>Raised when the user navigates to a different server page on the Results tab; carries the zero-based page index.</summary>
    public event Action<int>? PageRequested;

    /// <summary>Raised after this window hides (Close button, Escape, or an external Hide call) so the screen can dismiss
    /// dependent popups (e.g. the buy-confirm) that live on the root rather than as children of this panel.</summary>
    public event Action? Closed;

    /// <summary>Seller intent events, re-raised from the Sell page for the (future) market networking layer.</summary>
    public event Action<byte, int>? ListItemRequested;
    public event Action<ulong, int>? SetPriceRequested;
    public event Action<ulong, int>? DelistRequested;
    public event Action? CollectGoldRequested;

    /// <summary>
    ///     Raised when the user drops an inventory item onto a matching Sell-tab listing row. Re-raised from
    ///     <see cref="MarketSellControl.AddToListingRequested" />. Arguments: (listingId, inventorySlot, quantity).
    /// </summary>
    public event Action<ulong, byte, int>? AddToListingRequested;

    /// <summary>Raised when the Logs tab becomes active, so the screen can lazily request the sales-log snapshot.</summary>
    public event Action? LogsRequested;

    /// <summary>
    ///     Feeds a server-paged result set into the Results tab. Does not change the active tab — the search flow
    ///     handles tab switching (and post-buy refreshes arrive while the user is already on Results).
    /// </summary>
    public void SetResults(int page, int total, IReadOnlyList<MarketListing> listings) =>
        ResultsPage?.SetResults(page, total, listings);

    /// <summary>Replaces the Sell tab's listing set with the server's authoritative snapshot.</summary>
    public void SetSellListings(IReadOnlyList<MarketSellListing> listings) => SellPage?.SetListings(listings);

    /// <summary>Replaces the Logs tab's contents with the server's sales-log snapshot (newest first).</summary>
    public void SetSalesLog(IReadOnlyList<MarketSaleLog> entries) => LogsPage?.SetEntries(entries);

    /// <summary>Updates the pending gold payout shown on the Sell tab.</summary>
    public void SetSellPendingPayout(int gold) => SellPage?.SetPendingPayout(gold);

    /// <summary>True while the Sell tab is the active tab.</summary>
    public bool IsOnSellTab => Current == MarketTab.Sell;

    /// <summary>True if the point is over the Sell tab's listings drop zone.</summary>
    public bool SellDropZoneContains(int screenX, int screenY) => SellPage?.DropZoneContains(screenX, screenY) ?? false;

    public bool AcceptsInventoryDrop(byte slot, int screenX, int screenY)
        => (slot != 0) && Visible && IsOnSellTab && SellDropZoneContains(screenX, screenY);

    /// <summary>Adds an unpriced draft listing from inventory <paramref name="slot" /> (quantity <paramref name="amount" />).</summary>
    public void AddSellDraft(byte slot, int amount) => SellPage?.AddDraftListing(slot, amount);

    /// <summary>
    ///     Routes an inventory drop from <paramref name="slot" /> at screen point (<paramref name="screenX" />,
    ///     <paramref name="screenY" />) with the given <paramref name="amount" />. If the drop lands on a visible listing row
    ///     whose Sprite+Color match the item, raises <see cref="AddToListingRequested" /> via
    ///     <see cref="MarketSellControl.TryAddToExistingListing" />; otherwise falls through to <see cref="AddSellDraft" />
    ///     to create a new draft. The cap check for new drafts is inside <see cref="MarketSellControl.AddDraftListing" />.
    /// </summary>
    public void DropSellItem(byte slot, int amount, int screenX, int screenY)
    {
        if (SellPage is not null && SellPage.TryAddToExistingListing(slot, screenX, screenY, amount))
            return;

        AddSellDraft(slot, amount);
    }

    /// <summary>
    ///     Builds the real page control for a tab, sized to the shared content rect. The Search page additionally raises
    ///     <see cref="MarketSearchControl.SearchRequested" />, which flips the window to the Results tab and re-raises
    ///     <see cref="SearchRequested" /> with the criteria.
    /// </summary>
    private UIElement BuildPage(MarketTab tab, Rectangle pageRect)
    {
        switch (tab)
        {
            case MarketTab.Search:
                var searchPage = new MarketSearchControl(pageRect) { Name = "MarketPage_Search" };
                searchPage.SearchRequested += criteria =>
                {
                    ShowTab(MarketTab.Results);
                    SearchRequested?.Invoke(criteria);
                };

                return searchPage;

            case MarketTab.Results:
                var resultsPage = new MarketResultsControl(pageRect) { Name = "MarketPage_Results" };
                resultsPage.BuyRequested += (listing, quantity) => BuyRequested?.Invoke(listing, quantity);
                resultsPage.PageRequested += page => PageRequested?.Invoke(page);
                ResultsPage = resultsPage;

                return resultsPage;

            case MarketTab.Sell:
                var sellPage = new MarketSellControl(pageRect) { Name = "MarketPage_Sell" };
                sellPage.ListItemRequested += (slot, amount) => ListItemRequested?.Invoke(slot, amount);
                sellPage.SetPriceRequested += (id, price) => SetPriceRequested?.Invoke(id, price);
                sellPage.DelistRequested += (id, amount) => DelistRequested?.Invoke(id, amount);
                sellPage.CollectGoldRequested += () => CollectGoldRequested?.Invoke();
                sellPage.AddToListingRequested += (id, slot, amount) => AddToListingRequested?.Invoke(id, slot, amount);
                SellPage = sellPage;

                return sellPage;

            case MarketTab.Logs:
                var logsPage = new MarketLogsControl(pageRect) { Name = "MarketPage_Logs" };
                LogsPage = logsPage;

                return logsPage;

            default:
                throw new ArgumentOutOfRangeException(nameof(tab), tab, "Unknown market tab");
        }
    }

    /// <summary>
    ///     The usable content region inside the frame, below the tab strip. Used by every page control
    ///     (Search/Results/Sell) to size and position its children in panel-local space.
    /// </summary>
    private Rectangle ContentRect()
    {
        var top = CONTENT_TOP + TAB_HEIGHT;

        return new Rectangle(CONTENT_LEFT, top, Width - CONTENT_LEFT - CONTENT_RIGHT, Height - top - FRAME_BOTTOM);
    }

    public void ShowTab(MarketTab tab)
    {
        Current = tab;

        for (var i = 0; i < TabPages.Length; i++)
            TabPages[i].Visible = TabDefs[i].Tab == tab;

        for (var i = 0; i < Tabs.Length; i++)
            Tabs[i].IsSelected = TabDefs[i].Tab == tab;

        if (tab == MarketTab.Logs)
        {
            LogsPage?.ShowLoading();
            LogsRequested?.Invoke();
        }
    }

    public override void Show()
    {
        base.Show();
        ShowTab(Current);
    }

    public override void Hide()
    {
        base.Hide();
        Closed?.Invoke();
    }

    public override void OnKeyDown(KeyDownEvent e)
    {
        if (e.Key == Keys.Escape)
        {
            Hide();
            e.Handled = true;
        } else
            base.OnKeyDown(e); //forward Tab/etc. so the search form's textbox field cycling works
    }

    /// <summary>
    ///     A single nav tab. Draws nd_mtab.spf normal/selected background with a centered caption. Copied from
    ///     <c>MenuShopPanel.MerchantTab</c>, adapted to take a fixed caption in the constructor (Market tabs are static,
    ///     not data-driven categories).
    /// </summary>
    private sealed class TabButton : UIPanel
    {
        private readonly UIImage BackgroundImage;
        private readonly Texture2D? NormalBg;
        private readonly Texture2D? SelectedBg;

        public bool IsSelected
        {
            // ReSharper disable once UnusedMember.Local
            get;
            set
            {
                if (field == value)
                    return;

                field = value;
                BackgroundImage.Texture = value ? SelectedBg : NormalBg;
            }
        }

        public TabButton(Texture2D? normal, Texture2D? selected, string caption)
        {
            Width = TAB_WIDTH;
            Height = TAB_HEIGHT;
            NormalBg = normal;
            SelectedBg = selected;

            //background UIImage must use the texture's natural dimensions, not TAB_WIDTH/TAB_HEIGHT:
            //UIImage's Width/Height gate its ClipRect, so smaller values would self-clip the texture.
            //Named "Background" but stored in BackgroundImage to avoid shadowing UIPanel.Background (Texture2D?).
            BackgroundImage = new UIImage
            {
                Name = "Background",
                X = 0,
                Y = 0,
                Texture = NormalBg,
                Width = NormalBg?.Width ?? 0,
                Height = NormalBg?.Height ?? 0,
                IsHitTestVisible = false
            };
            AddChild(BackgroundImage);

            AddChild(
                new UILabel
                {
                    X = 0,
                    Y = (TAB_HEIGHT - TextRenderer.CHAR_HEIGHT) / 2 + 2,
                    Width = TAB_WIDTH,
                    Height = TextRenderer.CHAR_HEIGHT,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    ForegroundColor = LegendColors.White,
                    Text = caption
                });
        }

        public event ClickedHandler? Clicked;

        public override void OnClick(ClickEvent e)
        {
            Clicked?.Invoke();
            e.Handled = true;
        }
    }
}

public enum MarketTab
{
    Search = 0,
    Results = 1,
    Sell = 2,
    Logs = 3
}