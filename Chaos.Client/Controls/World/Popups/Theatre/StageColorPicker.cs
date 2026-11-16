#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
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
    private const int HEX_X = PAD + 30;
    private const int HEX_WIDTH = 50;

    private readonly CustomTextBox HexBox;
    private string LastHexText = string.Empty;
    private Color? ExactColor;
    private bool HexStrokeOpen;
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

        HexBox = new CustomTextBox
        {
            X = HEX_X,
            Y = PAD + SQUARE + 4,
            Width = HEX_WIDTH,
            Height = StageButton.HEIGHT,
            MaxLength = 7,
            HexOnly = true
        };

        //a 16px row fits one 12px text line with a 2px top/bottom inset
        HexBox.PaddingLeft = 4;
        HexBox.PaddingRight = 4;
        HexBox.PaddingTop = 2;
        HexBox.PaddingBottom = 2;
        HexBox.LostFocus += OnHexLostFocus;
        AddChild(HexBox);

        var ok = new StageButton("OK", 32)
        {
            X = Width - PAD - 32,
            Y = PAD + SQUARE + 4
        };

        ok.Clicked += PressOk;
        AddChild(ok);
    }

    //A parsed hex code round-trips through HSV floats, which can drift by 1 per channel, so the last
    //exactly-known colour wins until the next drag. Drags are inherently float and clear the override.
    public Color Current => ExactColor ?? HsvColor.FromHsv(Hue, Saturation, Value);

    public event Action<Color>? ColorChanged;
    public event Action? DragEnded;
    public event Action? Closed;

    public void Open(Color color)
    {
        (Hue, Saturation, Value) = HsvColor.ToHsv(color);
        ExactColor = color;
        SyncHexText();
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

        //preview swatch; the hex code lives in the editable box beside it
        var previewY = top + PAD + SQUARE + 4;
        DrawRectClipped(spriteBatch, new Rectangle(left + PAD, previewY + 2, 24, 12), Current);
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        if (Visible)
            ApplyHexEdit();
    }

    //the box has no text-changed event, so poll it. Programmatic sets go through SetHexBoxText,
    //which moves LastHexText along so they never look like user edits.
    private void ApplyHexEdit()
    {
        if (HexBox.Text == LastHexText)
            return;

        LastHexText = HexBox.Text;

        if (!HsvColor.TryParseHex(HexBox.Text, out var color))
            return;

        (Hue, Saturation, Value) = HsvColor.ToHsv(color);
        ExactColor = color;
        ColorChanged?.Invoke(Current);

        //one undo stroke per typed code: the drag end comes when the edit commits (blur or Enter/OK),
        //not per keystroke, so cloak/emblem/lighting record a single entry
        HexStrokeOpen = true;
    }

    public override void OnKeyDown(KeyDownEvent e)
    {
        //single-line Enter is left unhandled by the box and bubbles here (phase 1.5); Escape is left
        //unhandled too, and must keep bubbling to the parent window, which closes the picker
        if (HexBox.IsFocused && (e.Keycode == Keycode.Enter))
        {
            if (HsvColor.TryParseHex(HexBox.Text, out _))
                PressOk();
            else
                SetHexBoxText(HexOf(Current));

            e.Handled = true;
        } else if (HexBox.IsFocused && e.Accelerator && (e.Keycode is Keycode.Z or Keycode.Y))
            //belt-and-braces: the focused box already consumes these in phase 1, so canvas/portrait
            //undo never sees them, but swallowing here covers any path that reaches the picker
            e.Handled = true;
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

        //the drag's own end covers any open hex stroke, so clear it without a second DragEnded
        HexStrokeOpen = false;
        DragEnded?.Invoke();
        e.Handled = true;
    }

    public override void ResetInteractionState()
    {
        //same as above: a drag in flight covers the stroke, so clear it before the base call blurs
        //the box (whose LostFocus would otherwise commit the stroke and double-fire DragEnded)
        if (Part != Dragging.None)
            HexStrokeOpen = false;

        //hiding the picker blurs the box via the base call, which commits or reverts the typed text
        base.ResetInteractionState();

        if (Part != Dragging.None)
        {
            Part = Dragging.None;
            DragEnded?.Invoke();
        }
    }

    public override void Dispose()
    {
        HexBox.LostFocus -= OnHexLostFocus;
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

        ExactColor = null;
        SyncHexText();
        ColorChanged?.Invoke(Current);
    }

    private void PressOk()
    {
        //an edit and Enter can land in the same frame, before Update has polled the box
        ApplyHexEdit();
        CommitHexStroke();
        Visible = false;
        Closed?.Invoke();
    }

    private void CommitHexStroke()
    {
        if (!HexStrokeOpen)
            return;

        HexStrokeOpen = false;
        DragEnded?.Invoke();
    }

    private void OnHexLostFocus(UITextBox box)
    {
        if (!HsvColor.TryParseHex(HexBox.Text, out _))
            SyncHexText();
        else
            CommitHexStroke();
    }

    private static string HexOf(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";

    //Refreshes the box from Current, but never while the user is typing in it
    private void SyncHexText()
    {
        if (HexBox.IsFocused)
            return;

        SetHexBoxText(HexOf(Current));
    }

    private void SetHexBoxText(string text)
    {
        HexBox.Text = text;
        HexBox.CursorPosition = text.Length;
        HexBox.ClearSelection();
        LastHexText = text;
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
