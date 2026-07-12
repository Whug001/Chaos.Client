#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Models;
using Chaos.Client.Utilities;
using Chaos.DarkAges.Definitions;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.World.Popups.Market;

/// <summary>
///     The Results tab page: a master/detail split. The left column is a full-height, paginated shop-style list of
///     market listings (item icon + name + right-aligned price) with a Prev / page-label / Next footer; the right column
///     is an <see cref="ItemDetailControl" /> that fills with the full item detail when a row is <b>hovered</b>. A
///     vertical separator divides the two. There is no scrollbar on the list — navigation is strictly page-by-page via
///     the footer buttons (the mouse wheel pages too, except over the detail pane, which scrolls its own stat block).
///     Sized to the content <see cref="Rectangle" /> handed in by <see cref="MarketControl" />.
/// </summary>
/// <remarks>
///     Uses the canonical pooled-row pattern: a fixed array of <see cref="MarketListingRow" />s (one per visible slot,
///     <see cref="UIPanel.AddChild" />'d once) bound on demand from a backing <c>Listings</c> list at the current page.
///     <see cref="RefreshRows" /> re-binds the visible window. As a PAGE it owns no Show/Hide/Escape/control-stack —
///     <see cref="MarketControl" /> manages visibility. The current data is hardcoded placeholder listings until the
///     market search backend is wired (later task).
/// </remarks>
public sealed class MarketResultsControl : UIPanel
{
    private const int ROW_HEIGHT = MarketListingRow.ROW_HEIGHT;

    //listings shown per page; rows are sized so this many fit between the separator and the footer.
    private const int PAGE_SIZE = MarketConstants.PageSize;

    private const int FOOTER_HEIGHT = 26; //holds Prev / page label / Next / Ok

    //the shared content rect leaves a 4px gap below the tab-strip separator and stops ~10px short of the slack above
    //MarketControl's Close button. The Results list reclaims both so a full PAGE_SIZE of icon-height rows fits: it
    //starts flush under the separator (TOP_EXTEND) and drops its footer down into that lower slack (BOTTOM_EXTEND).
    private const int TOP_EXTEND = 4;
    private const int BOTTOM_EXTEND = 10;

    //the OK button is right-aligned to align with MarketControl's Close button, which sits ~2px past the content rect's
    //right edge; widen the panel by that much so the button isn't clipped by this control's ClipRect.
    private const int RIGHT_EXTEND = 2;

    //the listing list occupies the left column; a vertical divider then the detail pane fill the remaining width. The
    //list is wide enough for icon + name + a stack badge + a 9-digit price; the detail pane has ample padding to spare.
    private const int LIST_WIDTH = 320;

    //a thin strip above the footer (right column) reserved for the running total, which sits directly over the OK button.
    private const int TOTAL_ROW = 16;

    //the OK button reuses the wide _nbtn.spf action button (61x22): frame 3 normal, 4 pressed (5 = disabled, unused).
    private const int OK_BTN_WIDTH = 61;
    private const int OK_BTN_HEIGHT = 22;
    private const int NBTN_FRAME_OK = 3;
    private const int NBTN_FRAME_OK_PRESSED = 4;

    //the Prev/Next page buttons reuse the merchant shop browser's dedicated nav sprites (38x15 "Prev"/"Next" buttons,
    //frame 0 normal / 1 pressed / 2 disabled) so market paging matches the in-game shop.
    private const int PAGE_BTN_WIDTH = 38;
    private const int PAGE_BTN_HEIGHT = 15;
    private const string PAGE_PREV_SPF = "nd_mprev.spf";
    private const string PAGE_NEXT_SPF = "nd_mnext.spf";
    private const int PAGE_FRAME_NORMAL = 0;
    private const int PAGE_FRAME_PRESSED = 1;
    private const int PAGE_FRAME_DISABLED = 2;

    private const int SPINNER_GAP = 4; //gap between the quantity spinner and the OK button

    //base-field slots in the detail pane: level+class, weight, durability, seller.
    private const int DETAIL_BASE_LINES = 4;

    private readonly ItemDetailControl DetailPane;
    private readonly UIButton NextButton;
    private readonly UILabel PageLabel;
    private readonly UILabel PriceLabel;
    private readonly UIButton PrevButton;
    private readonly CustomNumericSpinner QuantitySpinner;
    private readonly MarketListingRow[] Rows;

    //holds only the current server page (PAGE_SIZE entries or fewer for the last page).
    private IReadOnlyList<MarketListing> Listings = [];
    private int ServerPage;
    private int TotalResults;
    private int SelectedIndex = -1;

    private int TotalPages => TotalResults > 0 ? (TotalResults + PAGE_SIZE - 1) / PAGE_SIZE : 1;

    public MarketResultsControl(Rectangle contentRect)
    {
        //raise the top flush with the separator and extend the bottom into the lower margin (see *_EXTEND above).
        X = contentRect.X;
        Y = contentRect.Y - TOP_EXTEND;
        Width = contentRect.Width + RIGHT_EXTEND;
        Height = contentRect.Height + TOP_EXTEND + BOTTOM_EXTEND;

        //the list + divider + detail share the full height above the footer band.
        var splitHeight = Height - FOOTER_HEIGHT;

        //── left column: the pooled listing rows ──
        Rows = new MarketListingRow[PAGE_SIZE];

        for (var i = 0; i < PAGE_SIZE; i++)
        {
            var row = new MarketListingRow(LIST_WIDTH)
            {
                X = 0,
                Y = i * ROW_HEIGHT,
                Width = LIST_WIDTH,
                Height = ROW_HEIGHT,
                Visible = false
            };

            var rowIndex = i;
            row.Clicked += () => Select(rowIndex);
            row.Hovered += () => HoverRow(rowIndex);
            Rows[i] = row;
            AddChild(row);
        }

        //── vertical divider between list and detail. The separator is now only as thick as its visible bevel, so we
        //   keep the original BORDER_SIZE-wide gap (detail pane stays put) and center the thin line within it. ──
        var divider = new CustomSeparator(SeparatorOrientation.Vertical, splitHeight) { Y = 0 };
        divider.X = LIST_WIDTH + (DialogFrame.BORDER_SIZE - divider.Width) / 2;
        AddChild(divider);

        //── right column: the hover-populated detail pane (shortened by TOTAL_ROW so the running total fits above OK) ──
        var detailX = LIST_WIDTH + DialogFrame.BORDER_SIZE; //keep the original gap so the detail pane doesn't move
        var detailHeight = splitHeight - TOTAL_ROW;

        //level+class / weight / durability / seller — the seller is the market's own 4th base line.
        DetailPane = new ItemDetailControl(
            Width - detailX,
            detailHeight,
            DETAIL_BASE_LINES,
            "Hover a listing to view its details.")
        {
            X = detailX,
            Y = 0
        };
        AddChild(DetailPane);

        //footer band along the bottom: Prev | Page X/Y | Next under the left list; quantity spinner + Ok under the
        //detail pane. The page nav buttons (38x15) and the spinner + Ok row (22 tall) are each centered in the band.
        var footerBandY = Height - FOOTER_HEIGHT;
        var pageBtnY = footerBandY + (FOOTER_HEIGHT - PAGE_BTN_HEIGHT) / 2;
        var okY = footerBandY + (FOOTER_HEIGHT - OK_BTN_HEIGHT) / 2;

        PrevButton = new UIButton
        {
            X = 0,
            Y = pageBtnY,
            Width = PAGE_BTN_WIDTH,
            Height = PAGE_BTN_HEIGHT,
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
            X = LIST_WIDTH - PAGE_BTN_WIDTH,
            Y = pageBtnY,
            Width = PAGE_BTN_WIDTH,
            Height = PAGE_BTN_HEIGHT,
            NormalTexture = UiRenderer.Instance!.GetSpfTexture(PAGE_NEXT_SPF, PAGE_FRAME_NORMAL),
            PressedTexture = UiRenderer.Instance!.GetSpfTexture(PAGE_NEXT_SPF, PAGE_FRAME_PRESSED),
            DisabledTexture = UiRenderer.Instance!.GetSpfTexture(PAGE_NEXT_SPF, PAGE_FRAME_DISABLED)
        };
        NextButton.Clicked += PageNext;
        AddChild(NextButton);

        //flush to the panel's right edge (RIGHT_EXTEND pushed it out to align with MarketControl's Close button).
        var okX = Width - OK_BTN_WIDTH;

        //quantity stepper sits just left of OK; sized for up to 3 digits (item counts are capped at 3 digits). It is
        //armed (max = the selected listing's AvailableCount) on Select and re-totals on value change.
        var spinnerWidth = CustomNumericSpinner.MeasureRequiredWidth(999);
        var spinnerX = okX - SPINNER_GAP - spinnerWidth;

        QuantitySpinner = new CustomNumericSpinner(spinnerWidth)
        {
            X = spinnerX,
            Y = okY
        };
        QuantitySpinner.ValueChanged += _ => UpdateBuyTotal();
        AddChild(QuantitySpinner);

        //running total (quantity × unit price), right-aligned directly above the OK button in the reserved TOTAL_ROW
        //strip. Spans the whole detail column so it has room for large totals.
        PriceLabel = new UILabel
        {
            X = detailX,
            Y = detailHeight + (TOTAL_ROW - TextRenderer.CHAR_HEIGHT) / 2,
            Width = Width - detailX,
            Height = TextRenderer.CHAR_HEIGHT,
            HorizontalAlignment = HorizontalAlignment.Right,
            ForegroundColor = LegendColors.Gold,
            Text = string.Empty
        };
        AddChild(PriceLabel);

        var okButton = new UIButton
        {
            X = okX,
            Y = okY,
            Width = OK_BTN_WIDTH,
            Height = OK_BTN_HEIGHT,
            NormalTexture = UiRenderer.Instance!.GetSpfTexture("_nbtn.spf", NBTN_FRAME_OK),
            PressedTexture = UiRenderer.Instance!.GetSpfTexture("_nbtn.spf", NBTN_FRAME_OK_PRESSED)
        };
        okButton.Clicked += OnOk;
        AddChild(okButton);
    }

    /// <summary>Raised when the user confirms a buy (clicks OK) on the selected listing; carries the chosen quantity.</summary>
    public event Action<MarketListing, int>? BuyRequested;

    /// <summary>Raised when the user navigates to a different server page; carries the zero-based page index.</summary>
    public event Action<int>? PageRequested;

    /// <summary>
    ///     Replaces the displayed page with server-supplied results. <paramref name="page" /> is zero-based;
    ///     <paramref name="total" /> is the total number of matching listings across all pages (used to compute
    ///     <see cref="TotalPages" /> and enable/disable nav buttons). <paramref name="listings" /> holds only the
    ///     entries for the current page (at most <see cref="PAGE_SIZE" /> items).
    /// </summary>
    public void SetResults(int page, int total, IReadOnlyList<MarketListing> listings)
    {
        Listings = listings;
        ServerPage = page;
        TotalResults = total;
        SelectedIndex = -1;

        DetailPane.Clear();
        PriceLabel.Text = string.Empty;
        QuantitySpinner.SetRange(1, 1); //nothing selected → spinner disabled at 1
        RefreshRows();
    }

    /// <summary>
    ///     Selects the row at the given page-local index, updates the selection highlight, and arms the quantity
    ///     stepper + running total from the selected listing (this is the buy target).
    /// </summary>
    private void Select(int rowIndex)
    {
        if ((rowIndex < 0) || (rowIndex >= Listings.Count))
            return;

        SelectedIndex = rowIndex;

        //arm the stepper from the selected listing's stack size (1 = non-stackable → spinner stays disabled at 1),
        //then refresh the running total.
        var listing = Listings[rowIndex];
        QuantitySpinner.SetRange(1, Math.Max(1, listing.AvailableCount));
        UpdateBuyTotal();

        RefreshRows();
    }

    /// <summary>Row hover handler: fills the detail pane with the hovered listing's full detail (preview only).</summary>
    private void HoverRow(int rowIndex)
    {
        if ((rowIndex < 0) || (rowIndex >= Listings.Count))
            return;

        var listing = Listings[rowIndex];

        //a null base line hides its slot; the description is deliberately not shown here (the market shows price
        //instead, in the footer).
        DetailPane.Populate(
            listing.Sprite,
            listing.Color,
            listing.Name,
            [
                listing.LevelReq > 0 ? $"Level: {listing.LevelReq}   Class: {listing.ClassReq}" : $"Class: {listing.ClassReq}",
                $"Weight: {listing.Weight}",
                listing.MaxDurability > 0 ? $"Durability: {listing.CurrentDurability}/{listing.MaxDurability}" : null,
                $"Seller: {listing.SellerName}"
            ],
            listing.Stats);
    }

    /// <summary>Refreshes the footer total (quantity × unit price) for the selected listing; clears it when none selected.</summary>
    private void UpdateBuyTotal()
    {
        if ((SelectedIndex < 0) || (SelectedIndex >= Listings.Count))
        {
            PriceLabel.Text = string.Empty;

            return;
        }

        var listing = Listings[SelectedIndex];
        var total = (long)listing.Price * QuantitySpinner.Value;
        PriceLabel.Text = $"Total: {total:N0}";
    }

    /// <summary>Re-binds each visible row from <c>Listings[i]</c> (page-local index), or clears it if past the end.</summary>
    private void RefreshRows()
    {
        for (var i = 0; i < Rows.Length; i++)
        {
            var row = Rows[i];

            if (i < Listings.Count)
            {
                var listing = Listings[i];
                var icon = UiRenderer.Instance!.GetItemIcon(listing.Sprite, listing.Color);
                row.SetEntry(icon, listing.Name, listing.Price, listing.AvailableCount);
                row.IsSelected = i == SelectedIndex;
                row.Visible = true;
            } else
            {
                row.ClearEntry();
                row.IsSelected = false;
                row.Visible = false;
            }
        }

        UpdatePageLabel();
    }

    private void UpdatePageLabel()
    {
        PageLabel.Text = $"Page {ServerPage + 1}/{TotalPages}";

        //mirror the shop browser: grey out (disable) the nav button at each end of the page range.
        PrevButton.Enabled = ServerPage > 0;
        NextButton.Enabled = ServerPage < (TotalPages - 1);
    }

    private void PagePrev()
    {
        if (ServerPage > 0)
            PageRequested?.Invoke(ServerPage - 1);
    }

    private void PageNext()
    {
        if (ServerPage < (TotalPages - 1))
            PageRequested?.Invoke(ServerPage + 1);
    }

    //buy hook: raises BuyRequested for the selected listing + chosen quantity so an upstream owner can show the
    //buy-confirm dialog. No-op when nothing is selected. The confirm dialog + actual purchase packet are handled upstream.
    private void OnOk()
    {
        if ((SelectedIndex < 0) || (SelectedIndex >= Listings.Count))
            return;

        QuantitySpinner.Commit(); //apply any typed-but-uncommitted quantity before reading it
        var listing = Listings[SelectedIndex];
        var quantity = Math.Clamp(QuantitySpinner.Value, 1, Math.Max(1, listing.AvailableCount));
        BuyRequested?.Invoke(listing, quantity);
    }

    /// <summary>
    ///     With the scrollbar gone, the mouse wheel pages the list (wheel up = previous page, wheel down = next page) so
    ///     the wheel stays useful when hovering the results area. Scrolling over the detail pane scrolls its stat block
    ///     instead — the pane consumes the wheel event so it never reaches this handler.
    /// </summary>
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

/// <summary>
///     A single market listing: the base item fields plus the per-item stat state. This mirrors the payload the market
///     backend will ultimately send with each listing's details — the base fields come from the wire <c>ItemInfo</c> /
///     item metafile, and <see cref="Stats" /> carries the per-item modifiers (which do not exist in the current
///     protocol and must be sent explicitly by the market backend). Placeholder data until that backend is wired.
/// </summary>
public readonly record struct MarketListing(
    ulong ListingId,
    ushort Sprite,
    DisplayColor Color,
    string Name,
    int Price,
    int LevelReq,
    string ClassReq,
    int Weight,
    int CurrentDurability,
    int MaxDurability,
    string Description,
    ItemStats Stats)
{
    /// <summary>How many of this item the listing has for sale (the stack size). 1 = non-stackable; caps the buy quantity.</summary>
    public int AvailableCount { get; init; } = 1;

    /// <summary>The character name of the player who listed this item.</summary>
    public string SellerName { get; init; } = string.Empty;
}
