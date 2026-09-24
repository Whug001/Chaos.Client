#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Definitions;
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.Definitions;
using Chaos.Client.Utilities;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Popups.Theatre;

/// <summary>
///     The custom colour picker: a saturation/value square for the current hue, a hue strip, a preview and OK. Raises
///     <see cref="ColorChanged" /> live while dragging, <see cref="DragEnded" /> on release, and <see cref="Closed" /> on
///     OK.
/// </summary>
public sealed class StageColorPicker : UIPanel
{
    private const int PAD = 6;
    private const int SQUARE = 96;
    private const int STRIP_WIDTH = 12;
    private const int STRIP_X = PAD + SQUARE + PAD;

    private Dragging Part;
    private float Hue;
    private float Saturation = 1f;
    private float Value = 1f;
    private Texture2D? SquareTexture;
    private float SquareHue = -1f;
    private Texture2D? StripTexture;

    public StageColorPicker()
    {
        Width = STRIP_X + STRIP_WIDTH + PAD;
        Height = PAD + SQUARE + 4 + StageButton.HEIGHT + PAD;
        BackgroundColor = new Color(28, 24, 20);
        BorderColor = new Color(138, 109, 59);
        Visible = false;

        var ok = new StageButton("OK", 32)
        {
            X = Width - PAD - 32,
            Y = PAD + SQUARE + 4
        };

        ok.Clicked += () =>
        {
            Visible = false;
            Closed?.Invoke();
        };

        AddChild(ok);
    }

    public Color Current => HsvColor.FromHsv(Hue, Saturation, Value);

    public event Action<Color>? ColorChanged;
    public event Action? DragEnded;
    public event Action? Closed;

    public void Open(Color color)
    {
        (Hue, Saturation, Value) = HsvColor.ToHsv(color);
        Visible = true;
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        if (!Visible)
            return;

        base.Draw(spriteBatch);
        EnsureTextures();

        var left = ScreenX;
        var top = ScreenY;
        DrawTexture(spriteBatch, SquareTexture, new Vector2(left + PAD, top + PAD), Color.White);
        DrawTexture(spriteBatch, StripTexture, new Vector2(left + STRIP_X, top + PAD), Color.White);

        //markers: a small box on the square, a bar on the strip
        var mx = left + PAD + (int)(Saturation * (SQUARE - 1));
        var my = top + PAD + (int)((1f - Value) * (SQUARE - 1));
        DrawBorder(spriteBatch, new Rectangle(mx - 3, my - 3, 7, 7), Color.White);
        var hy = top + PAD + (int)(Hue / 360f * (SQUARE - 1));
        DrawRectClipped(spriteBatch, new Rectangle(left + STRIP_X - 2, hy - 1, STRIP_WIDTH + 4, 3), Color.White);

        //preview and hex
        var previewY = top + PAD + SQUARE + 4;
        DrawRectClipped(spriteBatch, new Rectangle(left + PAD, previewY + 2, 24, 12), Current);
        var c = Current;
        DrawTextClipped(spriteBatch, new Vector2(left + PAD + 30, previewY + 2), $"#{c.R:X2}{c.G:X2}{c.B:X2}", LegendColors.Silver, false);
    }

    public override void OnMouseDown(MouseDownEvent e)
    {
        e.Handled = true;

        if (e.Button != MouseButton.Left)
            return;

        var x = e.ScreenX - ScreenX;
        var y = e.ScreenY - ScreenY;

        if ((y < PAD) || (y >= PAD + SQUARE))
            return;

        if ((x >= PAD) && (x < PAD + SQUARE))
            Part = Dragging.Square;
        else if ((x >= STRIP_X) && (x < STRIP_X + STRIP_WIDTH))
            Part = Dragging.Strip;
        else
            return;

        SetFromMouse(e.ScreenX, e.ScreenY);
    }

    public override void OnMouseMove(MouseMoveEvent e)
    {
        if (Part == Dragging.None)
            return;

        SetFromMouse(e.ScreenX, e.ScreenY);
        e.Handled = true;
    }

    public override void OnMouseUp(MouseUpEvent e)
    {
        if (Part == Dragging.None)
            return;

        Part = Dragging.None;
        DragEnded?.Invoke();
        e.Handled = true;
    }

    public override void ResetInteractionState()
    {
        base.ResetInteractionState();

        if (Part != Dragging.None)
        {
            Part = Dragging.None;
            DragEnded?.Invoke();
        }
    }

    public override void Dispose()
    {
        SquareTexture?.Dispose();
        StripTexture?.Dispose();
        base.Dispose();
    }

    private void SetFromMouse(int screenX, int screenY)
    {
        var y = Math.Clamp((screenY - ScreenY - PAD) / (float)(SQUARE - 1), 0f, 1f);

        if (Part == Dragging.Square)
        {
            Saturation = Math.Clamp((screenX - ScreenX - PAD) / (float)(SQUARE - 1), 0f, 1f);
            Value = 1f - y;
        } else
            Hue = y * 359.9f;

        ColorChanged?.Invoke(Current);
    }

    //UI textures are premultiplied; these are opaque, so plain colours are already correct
    private void EnsureTextures()
    {
        if (StripTexture is null)
        {
            var strip = new Color[STRIP_WIDTH * SQUARE];

            for (var py = 0; py < SQUARE; py++)
                for (var px = 0; px < STRIP_WIDTH; px++)
                    strip[(py * STRIP_WIDTH) + px] = HsvColor.FromHsv(py / (float)(SQUARE - 1) * 359.9f, 1f, 1f);

            StripTexture = new Texture2D(TextureConverter.Device, STRIP_WIDTH, SQUARE);
            StripTexture.SetData(strip);
        }

        if (SquareTexture is not null && (Math.Abs(SquareHue - Hue) < 0.01f))
            return;

        var pixels = new Color[SQUARE * SQUARE];

        for (var py = 0; py < SQUARE; py++)
            for (var px = 0; px < SQUARE; px++)
                pixels[(py * SQUARE) + px] = HsvColor.FromHsv(Hue, px / (float)(SQUARE - 1), 1f - (py / (float)(SQUARE - 1)));

        SquareTexture ??= new Texture2D(TextureConverter.Device, SQUARE, SQUARE);
        SquareTexture.SetData(pixels);
        SquareHue = Hue;
    }

    private enum Dragging
    {
        None,
        Square,
        Strip
    }
}
