#region
using Chaos.Client.Controls.Components;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Popups.Market;

/// <summary>
///     A single row in the market results listing: an item icon, a name, and a right-aligned price. Copied from
///     <c>MenuShopPanel.MerchantListingPanel</c> (the shop list row template) — same icon size, row height, red-text
///     selection highlight, and right-aligned cost. A metadata subline is intentionally omitted for the first pass.
/// </summary>
/// <remarks>
///     The icon/name/cost children are display-only (<see cref="UIElement.IsHitTestVisible" /> = false) so the row panel
///     itself stays the deepest hit-test target — clicks land on the row, not a child label. The row never disposes its
///     icon: item icons are shared cached textures owned by <see cref="UiRenderer" />.
/// </remarks>
public sealed class MarketListingRow : UIPanel
{
    public const int ICON_SIZE = 32;
    public const int ROW_HEIGHT = 32; //flush to the icon — no vertical padding around the row

    private const int ICON_X = 4;
    private const int ICON_TEXT_GAP = 8;
    private const int MAX_NAME_CHARS = 40;

    //horizontal space kept clear on the right for the price, so a long name ellipsizes instead of running under it.
    //Sized to fit a 9-digit grouped price ("999,999,999") with margin, which also yields a gap before the count badge.
    private const int PRICE_RESERVE = 78;

    //space reserved (left of the price) for the "[ N ]" stack-quantity badge — counts are capped at 3 digits.
    private const int COUNT_RESERVE = 48;

    private static readonly Color SELECTED_TEXT_COLOR = LegendColors.Scarlet;

    private readonly UILabel CostLabel;
    private readonly UILabel CountLabel;
    private readonly UIImage IconImage;
    private readonly UILabel NameLabel;

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
            CountLabel.ForegroundColor = color;
            CostLabel.ForegroundColor = color;
        }
    }

    public MarketListingRow(int contentWidth)
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
            Width = contentWidth - (ICON_X + ICON_SIZE + ICON_TEXT_GAP) - PRICE_RESERVE - COUNT_RESERVE,
            Height = TextRenderer.CHAR_HEIGHT,
            ForegroundColor = LegendColors.White,
            IsHitTestVisible = false
        };

        //stack-quantity badge, right-aligned in its own column just left of the price.
        CountLabel = new UILabel
        {
            X = contentWidth - PRICE_RESERVE - COUNT_RESERVE,
            Y = (ROW_HEIGHT - TextRenderer.CHAR_HEIGHT) / 2,
            Width = COUNT_RESERVE,
            Height = TextRenderer.CHAR_HEIGHT,
            HorizontalAlignment = HorizontalAlignment.Right,
            ForegroundColor = LegendColors.White,
            IsHitTestVisible = false
        };

        CostLabel = new UILabel
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
        AddChild(CountLabel);
        AddChild(CostLabel);
    }

    public event ClickedHandler? Clicked;

    //fired when the cursor enters the row — the results control populates the detail pane from this.
    public event ClickedHandler? Hovered;

    public void ClearEntry()
    {
        IconImage.Texture = null;
        IconImage.Visible = false;
        NameLabel.Text = string.Empty;
        CountLabel.Text = string.Empty;
        CostLabel.Text = string.Empty;
    }

    public void SetEntry(Texture2D? icon, string name, int cost, int count)
    {
        IconImage.Texture = icon;
        IconImage.Visible = icon is not null;

        //show the stack quantity badge only when there's more than one (a single item is the implicit default).
        CountLabel.Text = count > 1 ? $"[ {count} ]" : string.Empty;

        //truncate at first newline, then clamp to a generous max so the name doesn't run under the price
        var newlineIndex = name.IndexOf('\n');

        if (newlineIndex >= 0)
            name = name[..newlineIndex];

        if (name.Length > MAX_NAME_CHARS)
            name = name[..(MAX_NAME_CHARS - 3)] + "...";

        NameLabel.Text = name;
        CostLabel.Text = cost.ToString("N0");
    }

    public override void OnClick(ClickEvent e)
    {
        Clicked?.Invoke();
        e.Handled = true;
    }

    public override void OnMouseEnter() => Hovered?.Invoke();
}
