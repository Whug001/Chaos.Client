#region
using Chaos.Client.Controls.Components;
using Chaos.DarkAges.Definitions;
#endregion

namespace Chaos.Client.Controls.World.Popups.Exchange;

/// <summary>
///     A single item row in the exchange panel. Displays a 32x32 icon on the left and the item name (with count for
///     stackables) vertically centered beside it.
/// </summary>
public sealed class ExchangeItemControl : UIPanel
{
    private const int ICON_SIZE = 32;
    private const int TEXT_OFFSET_X = 36;
    private int BaseX;
    private readonly UIImage Icon;

    private readonly UILabel NameLabel;

    /// <summary>
    ///     The painted portrait hash for the hover tooltip, when this row shows a painted portrait.
    /// </summary>
    public string? PortraitHash { get; private set; }

    /// <summary>
    ///     The portrait painter for the hover tooltip, when this row shows a painted portrait.
    /// </summary>
    public string? PortraitPainter { get; private set; }

    /// <summary>
    ///     The displayed item name, for the hover tooltip.
    /// </summary>
    public string ItemName => NameLabel.Text;

    /// <summary>
    ///     Fired when a painted portrait row is hovered.
    /// </summary>
    public event Action<ExchangeItemControl>? PortraitHovered;

    /// <summary>
    ///     Fired when a painted portrait row is unhovered.
    /// </summary>
    public event Action? PortraitUnhovered;

    //true while this row has reported a portrait hover that has not been unreported yet. The row can be
    //repopulated or cleared while hovered (trade update, scroll, completion), which drops the hash that
    //OnMouseLeave checks - so the reported state is tracked separately and any path that ends it reports.
    private bool PortraitHoverReported;

    /// <summary>
    ///     Horizontal pixel offset applied to the entire entry. Set by ExchangeControl when the horizontal scrollbar moves.
    /// </summary>
    public int HorizontalOffset
    {
        get;
        set
        {
            field = value;
            X = BaseX - value;
        }
    }

    /// <summary>
    ///     Full pixel width of this entry (icon + padding + text). Used to compute horizontal scrollbar MaxValue.
    /// </summary>
    public int EntryWidth { get; private set; }

    public ExchangeItemControl()
    {
        Width = TEXT_OFFSET_X + 200;
        Height = ICON_SIZE;
        Visible = false;

        Icon = new UIImage
        {
            Name = "Icon",
            IsHitTestVisible = false,
            Width = ICON_SIZE,
            Height = ICON_SIZE
        };

        AddChild(Icon);

        NameLabel = new UILabel
        {
            Name = "ItemName",
            IsHitTestVisible = false,
            X = TEXT_OFFSET_X,
            Y = (Height - TextRenderer.CHAR_HEIGHT) / 2,
            Width = 200,
            Height = TextRenderer.CHAR_HEIGHT,
            PaddingLeft = 0,
            PaddingRight = 0
        };

        AddChild(NameLabel);
    }

    /// <summary>
    ///     Sets the base X position used as the anchor before horizontal offset is applied.
    /// </summary>
    public void SetBaseX(int x)
    {
        BaseX = x;
        X = x - HorizontalOffset;
    }

    public void ClearItem()
    {
        ClearPortraitHover();
        Icon.Texture = null;
        EntryWidth = 0;
        PortraitHash = null;
        PortraitPainter = null;
        Visible = false;
    }

    public void SetItem(ushort sprite, DisplayColor color, string name, string? portraitHash = null, string? portraitPainter = null)
    {
        ClearPortraitHover();
        Icon.Texture = UiRenderer.Instance!.GetItemIcon(sprite, color);
        var textWidth = TextRenderer.MeasureWidth(name);
        NameLabel.Width = textWidth;
        NameLabel.Text = name;
        PortraitHash = portraitHash;
        PortraitPainter = portraitPainter;
        EntryWidth = TEXT_OFFSET_X + textWidth;
        Visible = true;
    }

    public override void OnMouseEnter()
    {
        if (string.IsNullOrEmpty(PortraitHash))
            return;

        PortraitHoverReported = true;
        PortraitHovered?.Invoke(this);
    }

    public override void OnMouseLeave() => ClearPortraitHover();

    private void ClearPortraitHover()
    {
        if (!PortraitHoverReported)
            return;

        PortraitHoverReported = false;
        PortraitUnhovered?.Invoke();
    }
}