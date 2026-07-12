#region
using Chaos.Client.Collections;
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Controls.World.Popups.Dialog;
using Chaos.Client.Definitions;
using Chaos.Client.Extensions;
using Chaos.Client.Models;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.World.Popups.Bank;

/// <summary>
///     The bank window: a search box above a three-column body (category rail | item list | item detail) and a footer
///     holding the bank's gold, the page nav, an amount field and the Withdraw button. Mounted on WorldScreen.Root.
/// </summary>
/// <remarks>
///     A pure view: it holds no <c>ConnectionManager</c> and sends nothing. It raises <see cref="SearchSubmitted" /> /
///     <see cref="CategorySelected" /> / <see cref="WithdrawRequested" />, and <c>WorldScreen</c> owns the refresh
///     protocol behind them. All displayed data comes from <see cref="WorldState.Bank" />, repainted on its
///     <see cref="ViewModel.BankState.Changed" /> event.
///     <para>
///         "The category is the page": the server sends the rail, and clicking a rail row loads that one category's
///         items in full. Paging <b>within</b> the loaded category is therefore purely client-side — the items are
///         already here, so no page click ever hits the network.
///     </para>
///     <para>
///         Like <see cref="Market.MarketControl" />, it borrows the <c>_nsett</c> prefab (there is no DAT-backed bank
///         control file) purely for its <c>"OK"</c> button, then overrides Width/Height — the frame art is drawn
///         programmatically by <see cref="FramedDialogPanelBase" /> from the live size.
///     </para>
/// </remarks>
public sealed class BankControl : FramedDialogPanelBase, IInventoryDropTarget
{
    private const int PANEL_WIDTH = 560;
    private const int PANEL_HEIGHT = 300;

    //the panel is centered horizontally but pinned this far from the top of the screen (not vertically centered).
    private const int TOP_MARGIN = 15;

    private const int OK_RIGHT_MARGIN = 15;
    private const int OK_BOTTOM_MARGIN = 3;

    //frame insets: usable content begins after the ornate border.
    private const int CONTENT_LEFT = 17;
    private const int CONTENT_RIGHT = 17;
    private const int SEARCH_TOP = 8;
    private const int BODY_TOP = 34;

    //the title shares the top row with the search box, and always reads "Bank"
    private const int TITLE_WIDTH = 66;

    //rail + list + detail + gaps == the 526px content width (560 - 17 - 17). The list absorbs whatever is left over, so
    //the detail pane keeps a width the shared stat block wraps sanely at.
    private const int RAIL_WIDTH = 100;
    private const int LIST_WIDTH = 232;

    //both column boundaries carry a divider, so the gap has to hold one with air on either side — a bevel flush against
    //the columns reads as a rendering artifact rather than a divider
    private const int COLUMN_GAP = 7;

    //base-field slots in the detail pane: level+class, weight, durability
    private const int DETAIL_BASE_LINES = 3;

    private const int ROWS_PER_PAGE = 6;
    private const int BODY_HEIGHT = ROWS_PER_PAGE * BankItemRow.ROW_HEIGHT;

    //the footer band is icon-tall so the gold bag fits; it drops a little into the bottom border art (which is mostly
    //decorative) the same way MarketResultsControl's footer does, staying clear of the Close button below it. The gap
    //absorbs the body's 5px shift upward so the band keeps its distance from the bottom edge rather than riding the body.
    private const int FOOTER_GAP = 7;
    private const int FOOTER_TOP = BODY_TOP + BODY_HEIGHT + FOOTER_GAP;
    private const int FOOTER_HEIGHT = 32;

    private const ushort GOLD_SPRITE = 136; //the gold-bag icon, same sprite the inventory panel's gold slot uses

    //the Prev/Next page buttons reuse the merchant shop browser's nav sprites (38x15, frame 0/1/2 = normal/pressed/disabled).
    private const int PAGE_BTN_WIDTH = 38;
    private const int PAGE_BTN_HEIGHT = 15;
    private const int WITHDRAW_WIDTH = 72;
    private const int FIELD_GAP = 4;

    //a bank stack can run to five digits; size the amount field for that.
    private const int MAX_STACK = 99999;

    private readonly CustomNumericSpinner AmountSpinner;
    private readonly ItemDetailControl Detail;
    private readonly GoldBagIcon GoldIcon;
    private readonly UILabel GoldLabel;
    private readonly UIPanel ListPanel;
    private readonly UIButton NextButton;
    private readonly UILabel PageLabel;
    private readonly UIButton PrevButton;
    private readonly BankCategoryRail Rail;
    private readonly BankItemRow[] Rows;
    private readonly CustomTextBox SearchBox;
    private readonly CustomButton WithdrawButton;

    private int Page;

    //the entry the detail pane currently shows. BankState raises Changed for both the categories and the items, so a
    //single interaction refreshes twice — and repainting the pane means re-running its whole stat-block build.
    private BankItemEntry? PopulatedEntry;

    //the selected item, held by name — the bank's own key — so the highlight survives an item-list rebuild.
    private string SelectedName = string.Empty;

    private static IReadOnlyList<BankItemEntry> Items => WorldState.Bank.Items;

    /// <summary>
    ///     What the player typed. The only place a query lives on the client; the server holds the filter for the
    ///     session, so this is read solely to re-send it on a search.
    /// </summary>
    public string Query => SearchBox.Text;

    private int TotalPages => Math.Max(1, (Items.Count + ROWS_PER_PAGE - 1) / ROWS_PER_PAGE);

    public BankControl()
        : base("_nsett", false)
    {
        Name = "Bank";
        Visible = false;
        UsesControlStack = true; //inherited Show/Hide push/pop the InputDispatcher stack

        Width = PANEL_WIDTH;
        Height = PANEL_HEIGHT;
        this.CenterOnScreen();
        Y = TOP_MARGIN;

        OkButton = CreateButton("OK"); //the prefab's button (correctly sized + wired into the frame); re-skinned as Close

        if (OkButton is not null)
        {
            OkButton.NormalTexture = UiRenderer.Instance!.GetSpfTexture("_nbtn.spf");
            OkButton.PressedTexture = UiRenderer.Instance!.GetSpfTexture("_nbtn.spf", 1);
            OkButton.HoverTexture = null;
            OkButton.SelectedTexture = null;
            OkButton.DisabledTexture = null;

            OkButton.Clicked += Hide;
            OkButton.X = Width - OkButton.Width - OK_RIGHT_MARGIN;
            OkButton.Y = Height - OkButton.Height - OK_BOTTOM_MARGIN;
        }

        var contentWidth = Width - CONTENT_LEFT - CONTENT_RIGHT;
        var listX = CONTENT_LEFT + RAIL_WIDTH + COLUMN_GAP;
        var detailX = listX + LIST_WIDTH + COLUMN_GAP;
        var detailWidth = Width - CONTENT_RIGHT - detailX;

        //── title + search box share the top row. Enter submits; typing sends nothing. ──
        var titleLabel = new UILabel
        {
            X = CONTENT_LEFT,
            Y = SEARCH_TOP + (CustomButton.HEIGHT - TextRenderer.CHAR_HEIGHT) / 2,
            Width = TITLE_WIDTH,
            Height = TextRenderer.CHAR_HEIGHT,
            ForegroundColor = LegendColors.White,
            IsHitTestVisible = false,
            Text = "Bank"
        };
        AddChild(titleLabel);

        SearchBox = new CustomTextBox
        {
            X = CONTENT_LEFT + TITLE_WIDTH,
            Y = SEARCH_TOP,
            Width = contentWidth - TITLE_WIDTH,
            Height = CustomButton.HEIGHT, //the shared custom-control height (frame inset + one text line)
            MaxLength = 32,
            HintText = "Search"
        };
        AddChild(SearchBox);

        //── vertical dividers on both column boundaries: rail | list | detail ──
        AddChild(BuildColumnDivider(CONTENT_LEFT + RAIL_WIDTH));
        AddChild(BuildColumnDivider(listX + LIST_WIDTH));

        //── category rail ──
        Rail = new BankCategoryRail(RAIL_WIDTH, BODY_HEIGHT)
        {
            X = CONTENT_LEFT,
            Y = BODY_TOP
        };
        Rail.CategorySelected += OnRailCategorySelected;
        AddChild(Rail);

        //── item list: pooled rows, one page at a time ──
        ListPanel = new UIPanel
        {
            X = listX,
            Y = BODY_TOP,
            Width = LIST_WIDTH,
            Height = BODY_HEIGHT,
            IsPassThrough = true //the rows are the hit-test targets; the panel is just a frame for them
        };
        AddChild(ListPanel);

        Rows = new BankItemRow[ROWS_PER_PAGE];

        for (var i = 0; i < ROWS_PER_PAGE; i++)
        {
            var row = new BankItemRow(LIST_WIDTH)
            {
                X = 0,
                Y = i * BankItemRow.ROW_HEIGHT,
                Visible = false
            };

            var slot = i;
            row.Clicked += () => SelectSlot(slot);
            row.Hovered += () => HoverSlot(slot);
            row.WithdrawGestured += (name, count) => WithdrawGestured?.Invoke(name, count);
            Rows[i] = row;
            ListPanel.AddChild(row);
        }

        //── detail pane: built at its final size (it seeds its scroll viewport in its ctor and never re-lays-out) ──
        Detail = new ItemDetailControl(detailWidth, BODY_HEIGHT, DETAIL_BASE_LINES, "Hover an item to view its details.")
        {
            X = detailX,
            Y = BODY_TOP
        };
        AddChild(Detail);

        //── footer: gold bag + gold | page nav | amount + Withdraw ──
        var fieldY = FOOTER_TOP + (FOOTER_HEIGHT - CustomButton.HEIGHT) / 2;
        var pageBtnY = FOOTER_TOP + (FOOTER_HEIGHT - PAGE_BTN_HEIGHT) / 2;
        var textY = FOOTER_TOP + (FOOTER_HEIGHT - TextRenderer.CHAR_HEIGHT) / 2;

        GoldIcon = new GoldBagIcon
        {
            X = CONTENT_LEFT,
            Y = FOOTER_TOP,
            Width = BankItemRow.ICON_SIZE,
            Height = BankItemRow.ICON_SIZE,
            Texture = UiRenderer.Instance!.GetItemIcon(GOLD_SPRITE) //shared cache — never disposed
        };
        GoldIcon.WithdrawRequested += () => GoldWithdrawRequested?.Invoke();
        AddChild(GoldIcon);

        GoldLabel = new UILabel
        {
            X = CONTENT_LEFT + BankItemRow.ICON_SIZE + 2,
            Y = textY,
            Width = 90,
            Height = TextRenderer.CHAR_HEIGHT,
            ForegroundColor = LegendColors.Gold,
            IsHitTestVisible = false,
            Text = "0"
        };
        AddChild(GoldLabel);

        //page nav, centered under the item list column.
        var navWidth = PAGE_BTN_WIDTH * 2 + 70;
        var navX = listX + (LIST_WIDTH - navWidth) / 2;

        PrevButton = new UIButton
        {
            X = navX,
            Y = pageBtnY,
            Width = PAGE_BTN_WIDTH,
            Height = PAGE_BTN_HEIGHT,
            NormalTexture = UiRenderer.Instance!.GetSpfTexture("nd_mprev.spf"),
            PressedTexture = UiRenderer.Instance!.GetSpfTexture("nd_mprev.spf", 1),
            DisabledTexture = UiRenderer.Instance!.GetSpfTexture("nd_mprev.spf", 2)
        };
        PrevButton.Clicked += PagePrev;
        AddChild(PrevButton);

        PageLabel = new UILabel
        {
            X = navX + PAGE_BTN_WIDTH,
            Y = textY,
            Width = 70,
            Height = TextRenderer.CHAR_HEIGHT,
            HorizontalAlignment = HorizontalAlignment.Center,
            ForegroundColor = LegendColors.White,
            IsHitTestVisible = false,
            Text = "1/1"
        };
        AddChild(PageLabel);

        NextButton = new UIButton
        {
            X = navX + navWidth - PAGE_BTN_WIDTH,
            Y = pageBtnY,
            Width = PAGE_BTN_WIDTH,
            Height = PAGE_BTN_HEIGHT,
            NormalTexture = UiRenderer.Instance!.GetSpfTexture("nd_mnext.spf"),
            PressedTexture = UiRenderer.Instance!.GetSpfTexture("nd_mnext.spf", 1),
            DisabledTexture = UiRenderer.Instance!.GetSpfTexture("nd_mnext.spf", 2)
        };
        NextButton.Clicked += PageNext;
        AddChild(NextButton);

        //amount + Withdraw, right-aligned to the content edge.
        var withdrawX = Width - CONTENT_RIGHT - WITHDRAW_WIDTH;
        var spinnerWidth = CustomNumericSpinner.MeasureRequiredWidth(MAX_STACK);

        AmountSpinner = new CustomNumericSpinner(spinnerWidth)
        {
            X = withdrawX - FIELD_GAP - spinnerWidth,
            Y = fieldY
        };
        AddChild(AmountSpinner);

        WithdrawButton = new CustomButton("Withdraw", WITHDRAW_WIDTH)
        {
            X = withdrawX,
            Y = fieldY
        };
        WithdrawButton.Clicked += OnWithdraw;
        AddChild(WithdrawButton);

        WorldState.Bank.Changed += Refresh;
        Refresh();
    }

    /// <summary>Raised with the query text when the user presses Enter in the search box. Typing alone raises nothing.</summary>
    public event Action<string>? SearchSubmitted;

    /// <summary>Raised with the category name when a rail row is clicked; the owner loads that category's items.</summary>
    public event Action<string>? CategorySelected;

    /// <summary>Raised with (item display name, amount) when the user confirms a withdraw. No-op while nothing is selected.</summary>
    public event Action<string, int>? WithdrawRequested;

    /// <summary>
    ///     Raised with (item display name, banked stack count) when a row is taken out by gesture — double-clicked, or
    ///     dragged onto the inventory. Unlike <see cref="WithdrawRequested" /> no amount is chosen yet: the owner prompts
    ///     for a stack.
    /// </summary>
    public event Action<string, int>? WithdrawGestured;

    /// <summary>
    ///     Raised when the footer gold icon is clicked, or dragged onto the inventory. Gold is the one thing the Withdraw
    ///     button can't take out (it works off the item selection), so the icon is its own channel — the owner prompts for
    ///     an amount and withdraws.
    /// </summary>
    public event Action? GoldWithdrawRequested;

    //the bank is the one drop target that ACCEPTS slot 0 (the gold bag) — dragging gold in is how a deposit is made.
    public bool AcceptsInventoryDrop(byte slot, int screenX, int screenY) => Visible && ContainsPoint(screenX, screenY);

    /// <summary>Repaints everything from <see cref="WorldState.Bank" />: rail, gold, the current page, and the detail pane.</summary>
    private void Refresh()
    {
        var bank = WorldState.Bank;

        Rail.SetCategories(bank.Categories, bank.SelectedCategory);
        GoldLabel.Text = bank.Gold.ToString("N0");

        //the item count can shrink under the current page (a withdraw emptying the tail), so clamp before binding rows.
        Page = Math.Clamp(Page, 0, TotalPages - 1);

        var selected = FindSelected();

        if (selected is null)
            ClearSelection();
        else
        {
            PopulateDetail(selected);

            //only re-arm the spinner when the stack actually changed, so an unrelated refresh can't stomp a typed amount.
            if (AmountSpinner.Max != selected.Count)
                AmountSpinner.SetRange(1, Math.Max(1, selected.Count));
        }

        RefreshRows();
    }

    /// <summary>
    ///     A body-height divider centered in the <see cref="COLUMN_GAP" /> that starts at <paramref name="gapX" />. The
    ///     separator crops itself to its visible bevel, so its thickness is only known once it exists.
    /// </summary>
    private static CustomSeparator BuildColumnDivider(int gapX)
    {
        var divider = new CustomSeparator(SeparatorOrientation.Vertical, BODY_HEIGHT)
        {
            Y = BODY_TOP
        };

        divider.X = gapX + (COLUMN_GAP - divider.Width) / 2;

        return divider;
    }

    //an items display always deserializes fresh entries, so a genuine data change is a different instance — reference
    //equality is the "nothing to repaint" test.
    private void PopulateDetail(BankItemEntry entry)
    {
        if (ReferenceEquals(entry, PopulatedEntry))
            return;

        //a null base line hides its slot without moving the ones below it
        Detail.Populate(
            entry.Sprite,
            entry.Color,
            entry.Name,
            [
                entry.LevelReq > 0 ? $"Level: {entry.LevelReq}   Class: {entry.ClassReq}" : $"Class: {entry.ClassReq}",
                $"Weight: {entry.Weight}",
                entry.MaxDurability > 0 ? $"Durability: {entry.CurrentDurability}/{entry.MaxDurability}" : null
            ],
            new ItemStats(
                entry.Hp,
                entry.Mp,
                entry.Str,
                entry.Int,
                entry.Wis,
                entry.Con,
                entry.Dex,
                entry.Ac,
                entry.Hit,
                entry.Dmg,
                entry.AtkSpeed,
                entry.FlatSkillDmg,
                entry.FlatSpellDmg,
                entry.SkillDmgPct,
                entry.SpellDmgPct,
                entry.Cdr,
                entry.HealBonus,
                entry.HealBonusPct,
                entry.MagicResist));

        PopulatedEntry = entry;
    }

    private BankItemEntry? FindSelected()
        => SelectedName.Length == 0
            ? null
            : Items.FirstOrDefault(item => string.Equals(item.Name, SelectedName, StringComparison.Ordinal));

    private void ClearSelection()
    {
        SelectedName = string.Empty;
        Detail.Clear();
        PopulatedEntry = null;
        AmountSpinner.SetRange(1, 1);
    }

    /// <summary>Re-binds the visible rows to the current page's slice of the loaded category, then updates the page nav.</summary>
    private void RefreshRows()
    {
        var items = Items;
        var start = Page * ROWS_PER_PAGE;

        for (var i = 0; i < Rows.Length; i++)
        {
            var row = Rows[i];
            var index = start + i;

            if (index >= items.Count)
            {
                row.ClearEntry();
                row.IsSelected = false;
                row.Visible = false;

                continue;
            }

            var entry = items[index];
            row.SetEntry(entry);
            row.IsSelected = string.Equals(entry.Name, SelectedName, StringComparison.Ordinal);
            row.Visible = true;
        }

        PageLabel.Text = $"{Page + 1}/{TotalPages}";
        PrevButton.Enabled = Page > 0;
        NextButton.Enabled = Page < (TotalPages - 1);
        WithdrawButton.Enabled = SelectedName.Length > 0;
    }

    private BankItemEntry? EntryAt(int slot)
    {
        var index = Page * ROWS_PER_PAGE + slot;
        var items = Items;

        return index < items.Count ? items[index] : null;
    }

    private void SelectSlot(int slot)
    {
        var entry = EntryAt(slot);

        if (entry is null)
            return;

        SelectedName = entry.Name;
        PopulateDetail(entry);
        AmountSpinner.SetRange(1, Math.Max(1, entry.Count));
        RefreshRows();
    }

    //hover previews an item in the detail pane without changing the selection — the data is already loaded, no round-trip.
    private void HoverSlot(int slot)
    {
        var entry = EntryAt(slot);

        if (entry is not null)
            PopulateDetail(entry);
    }

    //another category is another result set, so it starts at its first page — same rule the search handler applies.
    private void OnRailCategorySelected(string category)
    {
        Page = 0;
        CategorySelected?.Invoke(category);
    }

    //paging stays inside the loaded category: the whole category is already here, so this sends nothing.
    private void PagePrev()
    {
        if (Page == 0)
            return;

        Page--;
        RefreshRows();
    }

    private void PageNext()
    {
        if (Page >= (TotalPages - 1))
            return;

        Page++;
        RefreshRows();
    }

    private void OnWithdraw()
    {
        var entry = FindSelected();

        if (entry is null)
            return;

        AmountSpinner.Commit(); //apply any typed-but-uncommitted amount before reading it
        var amount = Math.Clamp(AmountSpinner.Value, 1, Math.Max(1, entry.Count));
        WithdrawRequested?.Invoke(entry.Name, amount);
    }

    public override void Show()
    {
        //reset even when already visible: an Open can retarget the window at the other bank, and the server opens it
        //unfiltered — a surviving search box would show text that filters nothing until the next refresh sends it.
        Page = 0;
        SearchBox.Text = string.Empty;
        ClearSelection();

        base.Show();
        Refresh();
    }

    /// <summary>
    ///     Raised whenever the window closes, by any path (the close button, Escape, a map change). Prompts that act on
    ///     the bank must not outlive it: <see cref="Hide" /> clears <see cref="BankState" />, so a bank prompt confirmed
    ///     afterwards would act on a window that is gone.
    /// </summary>
    public event Action? Closed;

    public override void Hide()
    {
        var wasVisible = Visible;

        base.Hide();
        WorldState.Bank.Clear(); //the next open re-requests everything; nothing stale survives a close

        if (wasVisible)
            Closed?.Invoke();
    }

    public override void OnKeyDown(KeyDownEvent e)
    {
        switch (e.Keycode)
        {
            case Keycode.Escape:
                Hide();
                e.Handled = true;

                break;

            //single-line boxes let Enter bubble (UITextBox only consumes it when multiline), so submit lands here.
            case Keycode.Enter when SearchBox.IsFocused:
                SearchBox.IsFocused = false;

                //a new search is a new result set, so start at its first page. A post-mutation refresh is NOT a search
                //and deliberately keeps the page the player was on.
                Page = 0;
                SearchSubmitted?.Invoke(SearchBox.Text);
                e.Handled = true;

                break;

            default:
                base.OnKeyDown(e);

                break;
        }
    }

    //the rail and the detail pane consume the wheel themselves; over the item list it pages instead of scrolling. The
    //bounds check keeps a wheel over the search box or the footer from paging — but the window still swallows it, or an
    //unhandled wheel bubbles to WorldScreen.Root and scrolls the chat log behind the open bank.
    public override void OnMouseScroll(MouseScrollEvent e)
    {
        e.Handled = true;

        if (!ListPanel.ScreenBounds.Contains(e.ScreenX, e.ScreenY))
            return;

        if (e.Delta > 0)
            PagePrev();
        else
            PageNext();
    }

    public override void Dispose()
    {
        //WorldState.Bank is a process-lifetime static — a missed unsubscribe leaks this control forever.
        WorldState.Bank.Changed -= Refresh;
        base.Dispose();
    }

    //the gold bag is a gesture surface, not decoration: a click withdraws gold, a drag onto the inventory does the same.
    //A bare UIImage raises neither, hence the subclass — and being hit-test visible makes it the captured element, so the
    //dispatcher hands it OnDragStart directly.
    private sealed class GoldBagIcon : UIImage
    {
        public event ClickedHandler? WithdrawRequested;

        //an empty bag has nothing to take out: prompting for an amount would only earn a refusal from the server.
        public override void OnClick(ClickEvent e)
        {
            if ((e.Button != MouseButton.Left) || (WorldState.Bank.Gold <= 0))
                return;

            WithdrawRequested?.Invoke();
            e.Handled = true;
        }

        public override void OnDragStart(DragStartEvent e)
        {
            //the drag threshold is button-agnostic; only a left drag withdraws.
            if ((e.Button != MouseButton.Left) || (Texture is null) || (WorldState.Bank.Gold <= 0))
                return;

            e.Payload = new BankDragPayload
            {
                GhostTexture = Texture //no ItemName == the gold bag
            };
        }
    }
}
