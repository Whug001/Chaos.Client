#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Models;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.World.Popups.Market;

/// <summary>
///     The Sell tab's right-hand editor. Shows the currently-selected listing and lets the seller set/change its unit price
///     and delist units back to the bag; a persistent Collect-Gold button (independent of selection) sits at the bottom.
///     Raises intent events the container forwards to the (future) market networking layer; it performs no mutation itself.
/// </summary>
public sealed class MarketSellEditorControl : UIPanel
{
    private const int HEADER_TOP = 2;
    private const int ICON = 32;
    private const int FIELD_H = TextRenderer.CHAR_HEIGHT + 10; //~22, matches CustomTextBox inset / CustomButton.HEIGHT
    private const int PRICE_FIELD_W = 70;
    private const int SET_BTN_W = 66;
    private const int DELIST_BTN_W = 56;
    private const int SPINNER_GAP = 4; //gap between the delist spinner and the Delist button

    private static readonly Color GoldColor = LegendColors.Gold;

    private readonly UILabel HeaderLabel;
    private readonly UILabel EmptyHint;
    private readonly UIImage Icon;
    private readonly CustomTextBox PriceField;
    private readonly UILabel GoldSuffix;
    private readonly CustomButton SetPriceButton;
    private readonly CustomSeparator Divider;
    private readonly CustomNumericSpinner DelistSpinner;
    private readonly CustomButton DelistButton;
    private readonly CustomButton CollectButton;

    private MarketSellListing? Current;

    public MarketSellEditorControl(int width, int height)
    {
        Width = width;
        Height = height;

        Icon = new UIImage { X = 0, Y = HEADER_TOP, Width = ICON, Height = ICON, IsHitTestVisible = false };

        //two-line, vertically-centered name box beside the 32px icon — wraps like the Results detail-pane header.
        var nameX = ICON + 6;

        HeaderLabel = new UILabel
        {
            X = nameX, Y = HEADER_TOP + (ICON - TextRenderer.CHAR_HEIGHT * 2) / 2,
            Width = width - nameX, Height = TextRenderer.CHAR_HEIGHT * 2,
            WordWrap = true, VerticalAlignment = VerticalAlignment.Center,
            PaddingLeft = 0, PaddingRight = 0, PaddingTop = 0, PaddingBottom = 0,
            ForegroundColor = LegendColors.White
        };

        var priceRowY = HEADER_TOP + ICON + 8;

        PriceField = new CustomTextBox
        {
            X = 0, Y = priceRowY, Width = PRICE_FIELD_W, Height = FIELD_H, MaxLength = 9, HintText = "price"
        };

        GoldSuffix = new UILabel
        {
            X = PRICE_FIELD_W + 4, Y = priceRowY + (FIELD_H - TextRenderer.CHAR_HEIGHT) / 2,
            Width = 12, Height = TextRenderer.CHAR_HEIGHT, ForegroundColor = GoldColor, Text = "g"
        };

        //CustomButton is the standard custom-control height (CustomButton.HEIGHT == FIELD_H), so it lines up flush with
        //the price field / delist spinner on its row — no per-button vertical centering needed.
        SetPriceButton = new CustomButton("Set Price", SET_BTN_W)
        {
            X = width - SET_BTN_W, Y = priceRowY
        };
        SetPriceButton.Clicked += OnSetPrice;

        var dividerY = priceRowY + FIELD_H + 6;
        Divider = new CustomSeparator(SeparatorOrientation.Horizontal, width) { X = 0, Y = dividerY };

        var delistRowY = dividerY + 8;

        //spinner sits directly left of the Delist button (no "Delist" caption label).
        var delistButtonX = width - DELIST_BTN_W;
        var spinnerWidth = CustomNumericSpinner.MeasureRequiredWidth(999);

        DelistSpinner = new CustomNumericSpinner(spinnerWidth)
        {
            X = delistButtonX - SPINNER_GAP - spinnerWidth, Y = delistRowY
        };

        DelistButton = new CustomButton("Delist", DELIST_BTN_W)
        {
            X = delistButtonX, Y = delistRowY
        };
        DelistButton.Clicked += OnDelist;

        CollectButton = new CustomButton("Collect 0g", width) { X = 0, Y = height - CustomButton.HEIGHT, Enabled = false };
        CollectButton.Clicked += () => CollectClicked?.Invoke();

        EmptyHint = new UILabel
        {
            X = 4, Y = height / 3, Width = width - 8, Height = TextRenderer.CHAR_HEIGHT * 4,
            //match the Results detail pane's empty-state hint.
            ForegroundColor = LegendColors.Gray, WordWrap = true,
            Text = "Select a listing to price or delist it.\nDrag items from your inventory to list them."
        };

        AddChild(Icon);
        AddChild(HeaderLabel);
        AddChild(PriceField);
        AddChild(GoldSuffix);
        AddChild(SetPriceButton);
        AddChild(Divider);
        AddChild(DelistSpinner);
        AddChild(DelistButton);
        AddChild(CollectButton);
        AddChild(EmptyHint);

        ShowEmpty();
    }

    public event Action<ulong, int>? SetPriceClicked;
    public event Action<ulong, int>? DelistClicked;
    public event Action? CollectClicked;

    /// <summary>Populates the editor from <paramref name="listing" /> (pre-fills the price, arms the delist spinner).</summary>
    public void Show(MarketSellListing listing)
    {
        Current = listing;

        Icon.Texture = UiRenderer.Instance!.GetItemIcon(listing.Sprite, listing.Color);
        Icon.Visible = true;
        HeaderLabel.Text = listing.Name;
        PriceField.Text = listing.UnitPrice?.ToString() ?? string.Empty;
        DelistSpinner.SetRange(1, Math.Max(1, listing.Quantity));

        SetBodyVisible(true);
    }

    /// <summary>Clears the selection: hides the editor body and shows the empty hint (Collect stays visible).</summary>
    public void ShowEmpty()
    {
        Current = null;
        Icon.Visible = false;
        SetBodyVisible(false);
        EmptyHint.Visible = true;
    }

    /// <summary>Updates the Collect button's caption + enabled state from the pending payout total.</summary>
    public void SetPendingPayout(int gold)
    {
        CollectButton.Caption = $"Collect {gold:N0}g";
        CollectButton.Enabled = gold > 0;
    }

    private void SetBodyVisible(bool visible)
    {
        Icon.Visible = visible && (Current is not null);
        HeaderLabel.Visible = visible;
        PriceField.Visible = visible;
        GoldSuffix.Visible = visible;
        SetPriceButton.Visible = visible;
        Divider.Visible = visible;
        DelistSpinner.Visible = visible;
        DelistButton.Visible = visible;
        EmptyHint.Visible = !visible;
    }

    private void OnSetPrice()
    {
        if (Current is null)
            return;

        if (int.TryParse(PriceField.Text, out var price) && (price > 0))
            SetPriceClicked?.Invoke(Current.ListingId, price);
    }

    private void OnDelist()
    {
        if (Current is null)
            return;

        DelistSpinner.Commit(); //apply any typed-but-uncommitted amount before reading it
        var amount = Math.Clamp(DelistSpinner.Value, 1, Math.Max(1, Current.Quantity));
        DelistClicked?.Invoke(Current.ListingId, amount);
    }
}
