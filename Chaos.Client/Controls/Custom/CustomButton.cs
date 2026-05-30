#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Utilities;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using SkiaSharp;
#endregion

namespace Chaos.Client.Controls.Custom;

/// <summary>
///     A captioned, framed click button (UIPanel composite) drawn from dlgframe.epf — the same border source and fixed
///     <see cref="HEIGHT" /> as <see cref="CustomTextBox" /> / <see cref="CustomNumericSpinner" />, so it lines up in a row
///     beside them. The caption is centered; the frame dims while the button is disabled or pressed, and
///     <see cref="Clicked" /> fires on release.
/// </summary>
public sealed class CustomButton : UIPanel
{
    /// <summary>The shared custom-control height: CHAR_HEIGHT plus a 5px top/bottom inset, matching CustomTextBox/CustomNumericSpinner.</summary>
    public const int HEIGHT = TextRenderer.CHAR_HEIGHT + 10;

    private static readonly SKColor FillColor = new(10, 8, 5, 255);

    private readonly UILabel Label;
    private Texture2D? Frame;
    private Texture2D? FrameDim;
    private bool Pressed;

    public CustomButton(string caption, int width)
    {
        Width = width;
        Height = HEIGHT;

        Label = new UILabel
        {
            X = 0, Y = (HEIGHT - TextRenderer.CHAR_HEIGHT) / 2, Width = width, Height = TextRenderer.CHAR_HEIGHT,
            HorizontalAlignment = HorizontalAlignment.Center, ForegroundColor = TextColors.Default,
            IsHitTestVisible = false, Text = caption
        };
        AddChild(Label);

        Frame = BuildFrame(width, HEIGHT, false);
        FrameDim = BuildFrame(width, HEIGHT, true);
        Background = Frame;
    }

    public event ClickedHandler? Clicked;

    public string Caption { get => Label.Text; set => Label.Text = value; }

    public override void OnClick(ClickEvent e)
    {
        if (!Enabled)
            return;

        Clicked?.Invoke();
        e.Handled = true;
    }

    public override void OnMouseDown(MouseDownEvent e)
    {
        if (Enabled && (e.Button == MouseButton.Left))
            Pressed = true;
    }

    public override void OnMouseUp(MouseUpEvent e) => Pressed = false;
    public override void OnMouseLeave() => Pressed = false;

    public override void Draw(SpriteBatch spriteBatch)
    {
        if (!Visible)
            return;

        var bg = Enabled && !Pressed ? Frame : FrameDim;

        if (Background != bg)
            Background = bg;

        var col = Enabled ? TextColors.Default : Dim(TextColors.Default);

        if (Label.ForegroundColor != col)
            Label.ForegroundColor = col;

        base.Draw(spriteBatch);
    }

    public override void Dispose()
    {
        Frame?.Dispose();
        FrameDim?.Dispose();
        Frame = null;
        FrameDim = null;
        Background = null; //detach so base doesn't double-dispose
        base.Dispose();
    }

    private static Color Dim(Color c) => new((byte)(c.R / 2), (byte)(c.G / 2), (byte)(c.B / 2), c.A);

    private static Texture2D BuildFrame(int w, int h, bool dim)
    {
        using var frame = DialogFrame.Composite(FillColor, w, h);

        var info = new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info);

        if (frame is not null)
            surface.Canvas.DrawImage(frame, 0, 0);
        else
            surface.Canvas.Clear(FillColor);

        using var snapshot = surface.Snapshot();

        if (!dim)
            return TextureConverter.ToTexture2D(snapshot);

        using var dimSurface = SKSurface.Create(info);
        using var dimPaint = new SKPaint();

        //@formatter:off
        dimPaint.ColorFilter = SKColorFilter.CreateColorMatrix([
            0.5f, 0f, 0f, 0f, 0f,
            0f, 0.5f, 0f, 0f, 0f,
            0f, 0f, 0.5f, 0f, 0f,
            0f, 0f, 0f, 1f, 0f
        ]);
        //@formatter:on

        dimSurface.Canvas.DrawImage(snapshot, 0, 0, dimPaint);

        using var dimSnapshot = dimSurface.Snapshot();

        return TextureConverter.ToTexture2D(dimSnapshot);
    }
}
