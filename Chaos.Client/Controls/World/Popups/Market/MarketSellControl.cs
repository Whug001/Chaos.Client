#region
using Chaos.Client.Collections;
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Models;
using Chaos.Client.Utilities;
using Chaos.Client.ViewModel;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.World.Popups.Market;

/// <summary>
///     The Sell tab page: a master/detail split where the left column is the player's own market listings (drop target +
///     paginated, shop-style rows) and the right column is a <see cref="MarketSellEditorControl" /> that prices/delists the
///     selected listing and collects pending gold. Items are added by dragging from the inventory HUD onto the left column
///     (routed by <c>WorldScreen.HandleInventoryDropInViewport</c> → <see cref="AddDraftListing" />). All visible state is
///     driven by server snapshots pushed via <see cref="SetListings" />/<see cref="SetPendingPayout" />; the four intent
///     events (<see cref="ListItemRequested" />/<see cref="SetPriceRequested" />/<see cref="DelistRequested" />/
///     <see cref="CollectGoldRequested" />, mirroring the Results tab's <c>BuyRequested</c> seam) request changes from the
///     server, which replies with a fresh snapshot.
/// </summary>
public sealed class MarketSellControl : UIPanel
{
    public const int MAX_LISTINGS = 50; //mirrors server MarketOptions.MaxListingsPerPlayer

    private const int ROW_HEIGHT = MarketSellListingRow.ROW_HEIGHT;
    private const int PAGE_SIZE = 6; //fit 6 listings like the Results page

    //TOP_EXTEND raises the page up to the tab-strip separator; the count sits flush against it (local Y 0) in the
    //HEADER_HEIGHT band, and the rows begin at HEADER_HEIGHT so they land at the SAME screen Y as the Results rows.
    private const int HEADER_HEIGHT = 16;
    private const int FOOTER_HEIGHT = 26; //match MarketResultsControl so the Prev/page/Next footer lines up across tabs
    private const int TOP_EXTEND = 20;
    private const int BOTTOM_EXTEND = 10;
    private const int RIGHT_EXTEND = 2;
    private const int LIST_WIDTH = 320; //match MarketResultsControl so the left/right divider lines up across tabs

    private const int PAGE_BTN_WIDTH = 38;
    private const int PAGE_BTN_HEIGHT = 15;
    private const string PAGE_PREV_SPF = "nd_mprev.spf";
    private const string PAGE_NEXT_SPF = "nd_mnext.spf";
    private const int PAGE_FRAME_NORMAL = 0;
    private const int PAGE_FRAME_PRESSED = 1;
    private const int PAGE_FRAME_DISABLED = 2;

    private readonly MarketSellEditorControl Editor;
    private readonly UILabel CapLabel;
    private readonly UILabel PageLabel;
    private readonly UIButton PrevButton;
    private readonly UIButton NextButton;
    private readonly MarketSellListingRow[] Rows;

    private readonly List<MarketSellListing> Listings = [];
    private int CurrentPage;
    private int SelectedIndex = -1;

    private int TotalPages => Listings.Count > 0 ? (Listings.Count + PAGE_SIZE - 1) / PAGE_SIZE : 1;
    private int PageStart => CurrentPage * PAGE_SIZE;

    public MarketSellControl(Rectangle contentRect)
    {
        X = contentRect.X;
        Y = contentRect.Y - TOP_EXTEND;
        Width = contentRect.Width + RIGHT_EXTEND;
        Height = contentRect.Height + TOP_EXTEND + BOTTOM_EXTEND;

        var splitHeight = Height - FOOTER_HEIGHT;

        //── left column header: the "N / 20" listing count, centered just below the top separator ──
        CapLabel = new UILabel
        {
            X = 0, Y = 4, Width = LIST_WIDTH, Height = TextRenderer.CHAR_HEIGHT,
            HorizontalAlignment = HorizontalAlignment.Center, ForegroundColor = LegendColors.White
        };
        AddChild(CapLabel);

        //── left column: pooled listing rows ──
        Rows = new MarketSellListingRow[PAGE_SIZE];

        for (var i = 0; i < PAGE_SIZE; i++)
        {
            var row = new MarketSellListingRow(LIST_WIDTH)
            {
                X = 0,
                Y = HEADER_HEIGHT + i * ROW_HEIGHT,
                Width = LIST_WIDTH,
                Height = ROW_HEIGHT,
                Visible = false
            };

            var rowIndex = i;
            row.Clicked += () => Select(rowIndex);
            Rows[i] = row;
            AddChild(row);
        }

        //── vertical divider, aligned with the editor top (Y = HEADER_HEIGHT) so its top/bottom match the Results divider ──
        var divider = new CustomSeparator(SeparatorOrientation.Vertical, splitHeight - HEADER_HEIGHT) { Y = HEADER_HEIGHT };
        divider.X = LIST_WIDTH + (DialogFrame.BORDER_SIZE - divider.Width) / 2;
        AddChild(divider);

        var detailX = LIST_WIDTH + DialogFrame.BORDER_SIZE;

        //offset the editor down by HEADER_HEIGHT so its content keeps the same screen position despite the raised page top.
        Editor = new MarketSellEditorControl(Width - detailX, Height - HEADER_HEIGHT)
        {
            X = detailX,
            Y = HEADER_HEIGHT
        };
        Editor.SetPriceClicked += OnEditorSetPrice;
        Editor.DelistClicked += OnEditorDelist;
        Editor.CollectClicked += OnEditorCollect;
        AddChild(Editor);

        //── footer band under the left list: Prev | Page X/Y | Next ──
        var footerBandY = Height - FOOTER_HEIGHT;
        var pageBtnY = footerBandY + (FOOTER_HEIGHT - PAGE_BTN_HEIGHT) / 2;

        PrevButton = new UIButton
        {
            X = 0, Y = pageBtnY, Width = PAGE_BTN_WIDTH, Height = PAGE_BTN_HEIGHT,
            NormalTexture = UiRenderer.Instance!.GetSpfTexture(PAGE_PREV_SPF, PAGE_FRAME_NORMAL),
            PressedTexture = UiRenderer.Instance!.GetSpfTexture(PAGE_PREV_SPF, PAGE_FRAME_PRESSED),
            DisabledTexture = UiRenderer.Instance!.GetSpfTexture(PAGE_PREV_SPF, PAGE_FRAME_DISABLED)
        };
        PrevButton.Clicked += PagePrev;
        AddChild(PrevButton);

        PageLabel = new UILabel
        {
            X = PAGE_BTN_WIDTH + 6,
            Y = footerBandY + (FOOTER_HEIGHT - TextRenderer.CHAR_HEIGHT) / 2,
            Width = LIST_WIDTH - 2 * (PAGE_BTN_WIDTH + 6),
            Height = TextRenderer.CHAR_HEIGHT,
            HorizontalAlignment = HorizontalAlignment.Center,
            ForegroundColor = LegendColors.White,
            Text = "Page 1/1"
        };
        AddChild(PageLabel);

        NextButton = new UIButton
        {
            X = LIST_WIDTH - PAGE_BTN_WIDTH, Y = pageBtnY, Width = PAGE_BTN_WIDTH, Height = PAGE_BTN_HEIGHT,
            NormalTexture = UiRenderer.Instance!.GetSpfTexture(PAGE_NEXT_SPF, PAGE_FRAME_NORMAL),
            PressedTexture = UiRenderer.Instance!.GetSpfTexture(PAGE_NEXT_SPF, PAGE_FRAME_PRESSED),
            DisabledTexture = UiRenderer.Instance!.GetSpfTexture(PAGE_NEXT_SPF, PAGE_FRAME_DISABLED)
        };
        NextButton.Clicked += PageNext;
        AddChild(NextButton);

        Editor.SetPendingPayout(0);
        RefreshRows();
    }

    public event Action<byte, int>? ListItemRequested;
    public event Action<ulong, int>? SetPriceRequested;
    public event Action<ulong, int>? DelistRequested;
    public event Action? CollectGoldRequested;

    /// <summary>
    ///     Raised when the user drops an inventory item onto a visible listing row whose Sprite+Color match the dropped item.
    ///     Arguments: (listingId, inventorySlot, quantity). The server re-validates true item identity.
    /// </summary>
    public event Action<ulong, byte, int>? AddToListingRequested;

    public bool IsFull => Listings.Count >= MAX_LISTINGS;

    /// <summary>
    ///     Replaces the visible listing set with the server's authoritative snapshot. Preserves selection by
    ///     <see cref="MarketSellListing.ListingId" /> if the previously-selected listing is still present, re-showing it in
    ///     the editor; otherwise clears the selection and calls <see cref="MarketSellEditorControl.ShowEmpty" />.
    /// </summary>
    public void SetListings(IReadOnlyList<MarketSellListing> listings)
    {
        var previousId = (SelectedIndex >= 0) && (SelectedIndex < Listings.Count)
            ? Listings[SelectedIndex].ListingId
            : (ulong?)null;

        Listings.Clear();
        Listings.AddRange(listings);

        var newIndex = previousId is null ? -1 : Listings.FindIndex(l => l.ListingId == previousId.Value);

        if (newIndex >= 0)
        {
            SelectedIndex = newIndex;
            CurrentPage = newIndex / PAGE_SIZE;
            Editor.Show(Listings[newIndex]);
        } else
        {
            SelectedIndex = -1;

            if ((CurrentPage > 0) && (PageStart >= Listings.Count))
                CurrentPage = Math.Max(0, (Listings.Count - 1) / PAGE_SIZE);

            Editor.ShowEmpty();
        }

        RefreshRows();
    }

    /// <summary>Updates the pending gold payout displayed by the editor.</summary>
    public void SetPendingPayout(int gold) => Editor.SetPendingPayout(gold);

    /// <summary>True if <paramref name="screenX" />/<paramref name="screenY" /> falls over the left listings column.</summary>
    public bool DropZoneContains(int screenX, int screenY)
        => (screenX >= ScreenX) && (screenX < ScreenX + LIST_WIDTH) && (screenY >= ScreenY) && (screenY < ScreenY + Height);

    /// <summary>
    ///     Validates that the inventory slot is occupied and the quantity is in range, then raises
    ///     <see cref="ListItemRequested" /> so the server can create the listing. The server's sell snapshot
    ///     (pushed via <see cref="SetListings" />) drives all visible changes — nothing is mutated locally.
    ///     No-op if the slot is empty or the seller is already at the cap.
    /// </summary>
    public void AddDraftListing(byte slot, int amount)
    {
        if (IsFull)
        {
            //only the new-draft path reaches here (add-to-existing routes via TryAddToExistingListing) — give the
            //seller orange-bar feedback so a drop at the cap isn't a silent no-op.
            WorldState.Chat.AddOrangeBarMessage($"You can have at most {MAX_LISTINGS} market listings.");

            return;
        }

        ref readonly var data = ref WorldState.Inventory.GetSlot(slot);

        if (!data.IsOccupied)
            return;

        //can't list more units than the stack holds; non-stackables are always a single unit.
        ListItemRequested?.Invoke(slot, ClampToSlot(in data, amount));
    }

    /// <summary>
    ///     Returns the <see cref="MarketSellListing" /> whose row contains the given screen point, or <see langword="null" />
    ///     if the point is on an empty row or outside the list area.
    /// </summary>
    private MarketSellListing? ListingAt(int screenX, int screenY)
    {
        for (var i = 0; i < Rows.Length; i++)
        {
            var row = Rows[i];

            if (!row.Visible)
                continue;

            if ((screenX >= row.ScreenX)
                && (screenX < row.ScreenX + row.Width)
                && (screenY >= row.ScreenY)
                && (screenY < row.ScreenY + row.Height))
            {
                var index = PageStart + i;

                return index < Listings.Count ? Listings[index] : null;
            }
        }

        return null;
    }

    /// <summary>
    ///     If <paramref name="screenX" />/<paramref name="screenY" /> hits a visible listing row whose Sprite and Color match
    ///     the dropped inventory slot, raises <see cref="AddToListingRequested" /> and returns <see langword="true" />.
    ///     Name is intentionally excluded from the match because stackable items append a "[ N ]" count suffix that
    ///     diverges from the bare server-side listing name — the server performs the authoritative identity check.
    ///     Returns <see langword="false" /> if the point misses, the row is empty, or the Sprite/Color differ.
    /// </summary>
    public bool TryAddToExistingListing(byte slot, int screenX, int screenY, int amount)
    {
        var listing = ListingAt(screenX, screenY);

        if (listing is null)
            return false;

        ref readonly var data = ref WorldState.Inventory.GetSlot(slot);

        if (!data.IsOccupied)
            return false;

        // sprite + color is a cheap intent check; name excluded (stackables suffix "[ N ]"). server re-validates.
        if ((data.Sprite != listing.Sprite) || (data.Color != listing.Color))
            return false;

        AddToListingRequested?.Invoke(listing.ListingId, slot, ClampToSlot(in data, amount));

        return true;
    }

    //clamp a requested quantity to what the inventory slot can supply: non-stackables are always a single unit;
    //stackables clamp to the actual stack count.
    private static int ClampToSlot(in Inventory.InventorySlotData data, int amount)
        => Math.Clamp(amount, 1, data.Stackable ? Math.Max(1, (int)data.Count) : 1);

    private void Select(int rowIndex) => SelectIndex(PageStart + rowIndex);

    private void SelectIndex(int index)
    {
        if ((index < 0) || (index >= Listings.Count))
            return;

        SelectedIndex = index;
        Editor.Show(Listings[index]);
        RefreshRows();
    }

    private void OnEditorSetPrice(ulong listingId, int price) => SetPriceRequested?.Invoke(listingId, price);

    private void OnEditorDelist(ulong listingId, int amount) => DelistRequested?.Invoke(listingId, amount);

    private void OnEditorCollect() => CollectGoldRequested?.Invoke();

    private void RefreshRows()
    {
        for (var i = 0; i < Rows.Length; i++)
        {
            var index = PageStart + i;
            var row = Rows[i];

            if (index < Listings.Count)
            {
                var listing = Listings[index];
                var icon = UiRenderer.Instance!.GetItemIcon(listing.Sprite, listing.Color);
                row.SetEntry(icon, listing.Name, listing.Quantity, listing.UnitPrice);
                row.IsSelected = index == SelectedIndex;
                row.Visible = true;
            } else
            {
                row.ClearEntry();
                row.IsSelected = false;
                row.Visible = false;
            }
        }

        CapLabel.Text = $"{Listings.Count} / {MAX_LISTINGS}";
        PageLabel.Text = $"Page {CurrentPage + 1}/{TotalPages}";
        PrevButton.Enabled = CurrentPage > 0;
        NextButton.Enabled = CurrentPage < (TotalPages - 1);
    }

    private void PagePrev()
    {
        if (CurrentPage <= 0)
            return;

        CurrentPage--;
        RefreshRows();
    }

    private void PageNext()
    {
        if (CurrentPage >= TotalPages - 1)
            return;

        CurrentPage++;
        RefreshRows();
    }

    public override void OnMouseScroll(MouseScrollEvent e)
    {
        if (TotalPages <= 1)
            return;

        if (e.Delta > 0)
            PagePrev();
        else
            PageNext();

        e.Handled = true;
    }

}