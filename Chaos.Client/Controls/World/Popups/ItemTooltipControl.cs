#region
using Chaos.Client.Controls.Components;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Popups;

/// <summary>
///     Inventory item tooltip: the item name over its durability. Follows the cursor when visible.
/// </summary>
public sealed class ItemTooltipControl : TooltipPanelBase
{
    private const int MAX_CONTENT_WIDTH = 25 * TextRenderer.CHAR_WIDTH;

    private static readonly Color DurabilityColor = new(100, 149, 237);

    private readonly UIImage PortraitImage;
    private readonly UILabel PainterLabel;

    public ItemTooltipControl()
        : base("ItemTooltip", MAX_CONTENT_WIDTH, DurabilityColor)
    {
        PortraitImage = new UIImage
        {
            Name = "PortraitImage",
            Width = 48,
            Height = 56,
            Visible = false
        };

        PainterLabel = new UILabel
        {
            Name = "PortraitPainter",
            Width = MAX_CONTENT_WIDTH,
            Height = TextRenderer.CHAR_HEIGHT,
            PaddingLeft = 0,
            PaddingTop = 0,
            ForegroundColor = DurabilityColor,
            Visible = false
        };

        AddChild(PortraitImage);
        AddChild(PainterLabel);
    }

    public void Show(
        string itemName,
        int currentDurability,
        int maxDurability,
        int mouseX,
        int mouseY)
    {
        HeadingLabel.Text = itemName;

        var hasDurability = maxDurability > 0;
        BodyLabel.Text = hasDurability ? $"{currentDurability}/{maxDurability}" : string.Empty;
        BodyLabel.Visible = hasDurability;

        PortraitImage.Visible = false;
        PortraitImage.Texture = null;
        PainterLabel.Visible = false;

        Layout();
        UpdatePosition(mouseX, mouseY);

        Visible = true;
    }

    /// <summary>
    ///     Shows the tooltip with a painted portrait picture and its painter line. A null picture draws a blank frame.
    /// </summary>
    public void Show(
        string itemName,
        int currentDurability,
        int maxDurability,
        int mouseX,
        int mouseY,
        string? portraitHash,
        string? portraitPainter,
        Texture2D? portraitTexture)
    {
        HeadingLabel.Text = itemName;

        var hasDurability = maxDurability > 0;
        BodyLabel.Text = hasDurability ? $"{currentDurability}/{maxDurability}" : string.Empty;
        BodyLabel.Visible = hasDurability;

        var hasPortrait = !string.IsNullOrEmpty(portraitHash);
        PortraitImage.Visible = hasPortrait;
        PortraitImage.Texture = hasPortrait ? portraitTexture : null;
        PainterLabel.Visible = hasPortrait;
        PainterLabel.Text = hasPortrait ? $"Painted by {portraitPainter}" : string.Empty;

        LayoutPortrait();
        UpdatePosition(mouseX, mouseY);

        Visible = true;
    }

    /// <summary>Swaps the portrait picture once it arrives, without moving the tooltip.</summary>
    public void SetPortraitTexture(Texture2D? portraitTexture)
    {
        if (PortraitImage.Visible)
            PortraitImage.Texture = portraitTexture;
    }

    private void LayoutPortrait()
    {
        var headingHeight = HeadingLabel.ContentHeight;
        HeadingLabel.Height = headingHeight;

        var y = PADDING + headingHeight;
        PortraitImage.X = PADDING;
        PortraitImage.Y = y;
        y += PortraitImage.Height;

        PainterLabel.Y = y;
        var painterHeight = PainterLabel.ContentHeight;
        PainterLabel.Height = painterHeight;
        y += painterHeight;

        var bodyHeight = BodyLabel.Visible ? BodyLabel.ContentHeight : 0;
        BodyLabel.Y = y;
        BodyLabel.Height = Math.Max(TextRenderer.CHAR_HEIGHT, bodyHeight);
        y += bodyHeight;

        var totalWidth = PADDING + ContentWidth + PADDING;
        var totalHeight = y + PADDING;

        Width = totalWidth;
        Height = totalHeight;

        BackgroundImage.Texture?.Dispose();
        BackgroundImage.Texture = CompositeBackground(totalWidth, totalHeight);
        BackgroundImage.X = 0;
        BackgroundImage.Y = 0;
        BackgroundImage.Width = totalWidth;
        BackgroundImage.Height = totalHeight;
    }
}
