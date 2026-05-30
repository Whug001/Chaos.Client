#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Utilities;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SkiaSharp;
#endregion

namespace Chaos.Client.Controls.Custom;

/// <summary>
///     A UITextBox that paints a dlgframe.epf 8-piece border over a dark fill behind the editable text.
///     The base UITextBox draws no texture background (it relies on baked panel art), so this subclass owns
///     a Texture2D it builds with SkiaSharp (DialogFrame.Composite -> TextureConverter.ToTexture2D), exactly
///     like UICheckBox/UIComboBox. Lazy-built on first Draw and rebuilt whenever Width/Height change.
/// </summary>
public sealed class CustomTextBox : UITextBox
{
    private static readonly SKColor FillColor = new(10, 8, 5, 255);

    //text inset from the frame — matches UIComboBox's INNER_PAD so a textbox and a dropdown in the same
    //column align horizontally and read at the same height (the dlgframe's visible bevel is only a few px).
    private const int INNER_PAD = 5;

    private Texture2D? FrameTexture;
    private int BuiltWidth;
    private int BuiltHeight;

    /// <summary>
    ///     Placeholder/hint text shown while the box is empty AND unfocused (e.g. "min"). It disappears the moment the
    ///     box is focused or has any text, and returns when unfocused with no text.
    /// </summary>
    public string HintText { get; set; } = string.Empty;

    /// <summary>Color of the <see cref="HintText" /> — a muted grey so it reads as a placeholder, not real input.</summary>
    public Color HintColor { get; set; } = new(110, 105, 95);

    public CustomTextBox()
    {
        //small symmetric inset; with a ~22px box this vertically centers a 12px text row inside the frame.
        PaddingLeft = INNER_PAD;
        PaddingRight = INNER_PAD;
        PaddingTop = INNER_PAD;
        PaddingBottom = INNER_PAD;
        ForegroundColor = TextColors.Default;
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        if (!Visible)
            return;

        EnsureFrameTexture();
        UpdateClipRect(); //prime ClipRect before DrawTexture (which clips to it)

        if (FrameTexture is not null)
            DrawTexture(
                spriteBatch,
                FrameTexture,
                new Vector2(ScreenX, ScreenY),
                Color.White);

        base.Draw(spriteBatch); //UITextBox.Draw paints text/cursor on top

        //placeholder/hint: shown only while empty AND unfocused (hidden once focused or once it holds any text).
        if ((HintText.Length > 0) && (Text.Length == 0) && !IsFocused)
            DrawTextClipped(spriteBatch, new Vector2(ScreenX + PaddingLeft, ScreenY + PaddingTop), HintText, HintColor);
    }

    private void EnsureFrameTexture()
    {
        if ((Width <= 0) || (Height <= 0))
            return;

        if (FrameTexture is { IsDisposed: false } && (BuiltWidth == Width) && (BuiltHeight == Height))
            return;

        FrameTexture?.Dispose();
        FrameTexture = BuildFrame(Width, Height);
        BuiltWidth = Width;
        BuiltHeight = Height;
    }

    private static Texture2D BuildFrame(int w, int h)
    {
        using var frame = DialogFrame.Composite(FillColor, w, h); //SKImage? — may be null

        var info = new SKImageInfo(
            w,
            h,
            SKColorType.Rgba8888,
            SKAlphaType.Premul);
        using var surface = SKSurface.Create(info);
        var canvas = surface.Canvas;

        if (frame is not null)
            canvas.DrawImage(frame, 0, 0);
        else
            canvas.Clear(FillColor); //fallback if dlgframe.epf failed to load

        using var snapshot = surface.Snapshot();

        return TextureConverter.ToTexture2D(snapshot); //uses static TextureConverter.Device
    }

    public override void Dispose()
    {
        FrameTexture?.Dispose();
        FrameTexture = null;
        base.Dispose();
    }
}
