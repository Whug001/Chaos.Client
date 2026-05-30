#region
using Chaos.Client.Controls.Components;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Popups.Market;

/// <summary>
///     A single row on the Sell tab's "My Listings" list: item icon, name, a right-aligned "[ N ]" quantity badge, and a
///     right-aligned white price — or a muted "unpriced" label when the listing has no price yet. Modeled on
///     <see cref="MarketListingRow" /> (same icon size / row height / red selection highlight / white price / "[ N ]"
///     count badge) but the right side shows seller state (quantity + price-or-unpriced) instead of a buy price.
/// </summary>
/// <remarks>
///     Children are display-only (<see cref="UIElement.IsHitTestVisible" /> = false) so the row panel stays the deepest
///     hit-test target. Icons are shared cached textures owned by <see cref="UiRenderer" /> — the row never disposes them.
/// </remarks>
public sealed class MarketSellListingRow : UIPanel
{
    public const int ICON_SIZE = 32;
    public const int ROW_HEIGHT = 32;

    private const int ICON_X = 4;
    private const int ICON_TEXT_GAP = 8;
    private const int MAX_NAME_CHARS = 40;

    //right column for a 9-digit grouped price ("999,999,999") or the word "unpriced".
    private const int PRICE_RESERVE = 78;

    //column for the "[ N ]" quantity badge, left of the price.
    private const int QTY_RESERVE = 48;

    private static readonly Color SELECTED_TEXT_COLOR = LegendColors.Scarlet;

    private readonly UILabel NameLabel;
    private readonly UILabel PriceLabel;
    private readonly UILabel QtyLabel;
    private readonly UIImage IconImage;

    private bool Priced; //tracks whether the price label currently shows a price (drives deselect recolor)

    public bool IsSelected
    {
        // ReSharper disable once UnusedMember.Local
        get;
        set
        {
            if (field == value)
                return;

            field = value;

            var color = value ? SELECTED_TEXT_COLOR : LegendColors.White;
            NameLabel.ForegroundColor = color;
            QtyLabel.ForegroundColor = color;
            PriceLabel.ForegroundColor = value ? SELECTED_TEXT_COLOR : (Priced ? LegendColors.White : TextColors.Default);
        }
    }

    public MarketSellListingRow(int contentWidth)
    {
        Width = contentWidth;
        Height = ROW_HEIGHT;

        IconImage = new UIImage
        {
            X = ICON_X,
            Y = (ROW_HEIGHT - ICON_SIZE) / 2,
            Width = ICON_SIZE,
            Height = ICON_SIZE,
            IsHitTestVisible = false
        };

        NameLabel = new UILabel
        {
            X = ICON_X + ICON_SIZE + ICON_TEXT_GAP,
            Y = (ROW_HEIGHT - TextRenderer.CHAR_HEIGHT) / 2,
            Width = contentWidth - (ICON_X + ICON_SIZE + ICON_TEXT_GAP) - PRICE_RESERVE - QTY_RESERVE,
            Height = TextRenderer.CHAR_HEIGHT,
            ForegroundColor = LegendColors.White,
            IsHitTestVisible = false
        };

        //stack-quantity badge, right-aligned in its own column just left of the price.
        QtyLabel = new UILabel
        {
            X = contentWidth - PRICE_RESERVE - QTY_RESERVE,
            Y = (ROW_HEIGHT - TextRenderer.CHAR_HEIGHT) / 2,
            Width = QTY_RESERVE,
            Height = TextRenderer.CHAR_HEIGHT,
            HorizontalAlignment = HorizontalAlignment.Right,
            ForegroundColor = LegendColors.White,
            IsHitTestVisible = false
        };

        PriceLabel = new UILabel
        {
            X = 0,
            Y = (ROW_HEIGHT - TextRenderer.CHAR_HEIGHT) / 2,
            Width = contentWidth - 4,
            Height = TextRenderer.CHAR_HEIGHT,
            HorizontalAlignment = HorizontalAlignment.Right,
            ForegroundColor = LegendColors.White,
            IsHitTestVisible = false
        };

        AddChild(IconImage);
        AddChild(NameLabel);
        AddChild(QtyLabel);
        AddChild(PriceLabel);
    }

    public event ClickedHandler? Clicked;

    public void ClearEntry()
    {
        IconImage.Texture = null;
        IconImage.Visible = false;
        NameLabel.Text = string.Empty;
        QtyLabel.Text = string.Empty;
        PriceLabel.Text = string.Empty;
    }

    public void SetEntry(Texture2D? icon, string name, int quantity, int? unitPrice)
    {
        IconImage.Texture = icon;
        IconImage.Visible = icon is not null;

        //show the "[ N ]" badge only when there's more than one unit — matches the Results row.
        QtyLabel.Text = quantity > 1 ? $"[ {quantity} ]" : string.Empty;

        var newlineIndex = name.IndexOf('\n');

        if (newlineIndex >= 0)
            name = name[..newlineIndex];

        if (name.Length > MAX_NAME_CHARS)
            name = name[..(MAX_NAME_CHARS - 3)] + "...";

        NameLabel.Text = name;

        Priced = unitPrice.HasValue;

        if (unitPrice.HasValue)
        {
            PriceLabel.Text = unitPrice.Value.ToString("N0");

            if (!IsSelected)
                PriceLabel.ForegroundColor = LegendColors.White;
        } else
        {
            PriceLabel.Text = "unpriced";

            if (!IsSelected)
                PriceLabel.ForegroundColor = TextColors.Default;
        }
    }

    public override void OnClick(ClickEvent e)
    {
        Clicked?.Invoke();
        e.Handled = true;
    }
}
