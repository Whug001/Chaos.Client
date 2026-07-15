#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Utilities;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SkiaSharp;
#endregion

namespace Chaos.Client.Controls.World.Popups;

/// <summary>
///     A black dlgframe panel that follows the cursor: a heading line over a body line. Subclasses fill the two labels and
///     call <see cref="Layout" />.
/// </summary>
public abstract class TooltipPanelBase : UIPanel
{
    protected const int PADDING = 6;

    protected UILabel BodyLabel { get; }
    protected UILabel HeadingLabel { get; }
    private UIImage BackgroundImage { get; }

    /// <summary>The content width the labels wrap at. Fixed for the life of the tooltip.</summary>
    private int ContentWidth { get; }

    protected TooltipPanelBase(string name, int contentWidth, Color bodyColor)
    {
        Name = name;
        ContentWidth = contentWidth;
        Visible = false;

        //a tooltip explains what is under the cursor; it must never become what is under the cursor
        IsHitTestVisible = false;

        BackgroundImage = new UIImage
        {
            Name = $"{name}Content"
        };

        HeadingLabel = new UILabel
        {
            X = PADDING,
            Y = PADDING,
            Width = contentWidth,
            Height = TextRenderer.CHAR_HEIGHT,
            WordWrap = true,
            PaddingLeft = 0,
            PaddingTop = 0,
            ForegroundColor = LegendColors.White
        };

        BodyLabel = new UILabel
        {
            X = PADDING,
            Width = contentWidth,
            Height = TextRenderer.CHAR_HEIGHT,
            WordWrap = true,
            PaddingLeft = 0,
            PaddingTop = 0,
            ForegroundColor = bodyColor
        };

        AddChild(BackgroundImage);
        AddChild(HeadingLabel);
        AddChild(BodyLabel);
    }

    private static Texture2D CompositeBackground(int totalWidth, int totalHeight)
    {
        var black = new SKColor(
            0,
            0,
            0,
            255);

        using var background = DialogFrame.Composite(black, totalWidth, totalHeight);

        if (background is not null)
            return TextureConverter.ToTexture2D(background);

        var info = new SKImageInfo(
            totalWidth,
            totalHeight,
            SKColorType.Rgba8888,
            SKAlphaType.Premul);
        using var surface = SKSurface.Create(info);
        surface.Canvas.Clear(black);

        using var snapshot = surface.Snapshot();

        return TextureConverter.ToTexture2D(snapshot);
    }

    public void Hide() => Visible = false;

    /// <summary>
    ///     Sizes the panel to its two labels and rebuilds the frame behind them. Call after setting the label text; the
    ///     body may be hidden by setting <see cref="UIElement.Visible" /> false on it first.
    /// </summary>
    protected void Layout()
    {
        var headingHeight = HeadingLabel.ContentHeight;
        HeadingLabel.Height = headingHeight;

        var bodyHeight = BodyLabel.Visible ? BodyLabel.ContentHeight : 0;
        BodyLabel.Y = PADDING + headingHeight;
        BodyLabel.Height = Math.Max(TextRenderer.CHAR_HEIGHT, bodyHeight);

        var totalWidth = PADDING + ContentWidth + PADDING;
        var totalHeight = PADDING + headingHeight + bodyHeight + PADDING;

        Width = totalWidth;
        Height = totalHeight;

        BackgroundImage.Texture?.Dispose();
        BackgroundImage.Texture = CompositeBackground(totalWidth, totalHeight);
        BackgroundImage.X = 0;
        BackgroundImage.Y = 0;
        BackgroundImage.Width = totalWidth;
        BackgroundImage.Height = totalHeight;
    }

    /// <summary>
    ///     Positions the tooltip beside the cursor, flipping to its left and clamping vertically so it stays on screen.
    /// </summary>
    public void UpdatePosition(int mouseX, int mouseY)
    {
        var rightX = mouseX + 15;

        X = (rightX + Width) <= ChaosGame.VIRTUAL_WIDTH ? rightX : mouseX - Width;

        //body text is arbitrary length, so the panel can be taller than the screen -- a negative maximum would throw
        Y = Math.Clamp(mouseY + 15, 0, Math.Max(0, ChaosGame.VIRTUAL_HEIGHT - Height));
    }
}
