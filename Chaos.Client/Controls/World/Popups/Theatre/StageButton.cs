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
///     A compact button for the Stage Lighting window (16 px high by default). Can show a colour <see cref="Dot" />
///     before its caption, be filled with a <see cref="Fill" /> colour (a swatch), or show rainbow stripes (the custom
///     colour swatch).
/// </summary>
public sealed class StageButton : UIElement
{
    public const int HEIGHT = 16;

    private static readonly Color Face = new(42, 34, 26);
    private static readonly Color FacePressed = new(58, 47, 34);
    private static readonly Color FaceSelected = new(138, 109, 59);
    private static readonly Color FaceDisabled = new(24, 20, 16);
    private static readonly Color Edge = new(138, 109, 59);

    private bool Hovered;
    private bool Pressed;

    public StageButton(string caption, int width)
    {
        Caption = caption;
        Width = width;
        Height = HEIGHT;
    }

    public string Caption { get; set; }
    public bool Selected { get; set; }
    public Color? Dot { get; set; }
    public Color? Fill { get; set; }
    public bool Rainbow { get; set; }

    public event Action? Clicked;

    public override void Draw(SpriteBatch spriteBatch)
    {
        if (!Visible)
            return;

        base.Draw(spriteBatch);

        var bounds = ScreenBounds;

        if (Rainbow)
        {
            var stripe = Math.Max(1, Width / 6);

            for (var i = 0; i < 6; i++)
                DrawRectClipped(
                    spriteBatch,
                    new Rectangle(bounds.X + (i * stripe), bounds.Y, i == 5 ? Width - (5 * stripe) : stripe, Height),
                    HsvColor.FromHsv(i * 60f, 1f, 1f));
        } else
        {
            var face = Fill ?? (!Enabled ? FaceDisabled : Selected ? FaceSelected : Pressed ? FacePressed : Face);
            DrawRectClipped(spriteBatch, bounds, face);
        }

        var edge = (Fill.HasValue || Rainbow) && Selected ? LegendColors.Gold : Hovered && Enabled ? LegendColors.Silver : Edge;
        DrawBorder(spriteBatch, bounds, edge);

        if ((Fill.HasValue || Rainbow) && Selected)
            DrawBorder(spriteBatch, new Rectangle(bounds.X + 1, bounds.Y + 1, Width - 2, Height - 2), LegendColors.Gold);

        if (string.IsNullOrEmpty(Caption))
            return;

        var textColor = !Enabled ? LegendColors.Gray : Selected && !Fill.HasValue ? new Color(28, 24, 20) : Hovered ? Color.White : LegendColors.Silver;
        var dotSpace = Dot.HasValue ? 10 : 0;
        var textWidth = TextRenderer.MeasureWidth(Caption);
        var x = bounds.X + ((Width - textWidth - dotSpace) / 2);

        if (Dot is { } dot)
        {
            DrawRectClipped(spriteBatch, new Rectangle(x, bounds.Y + ((Height - 8) / 2), 8, 8), dot);
            x += dotSpace;
        }

        DrawTextClipped(spriteBatch, new Vector2(x, bounds.Y + ((Height - TextRenderer.CHAR_HEIGHT) / 2)), Caption, textColor, false);
    }

    public override void OnMouseEnter() => Hovered = true;

    public override void OnMouseLeave()
    {
        Hovered = false;
        Pressed = false;
    }

    public override void OnMouseDown(MouseDownEvent e)
    {
        if ((e.Button != MouseButton.Left) || !Enabled)
            return;

        Pressed = true;
        e.Handled = true;
    }

    public override void OnMouseUp(MouseUpEvent e) => Pressed = false;

    public override void OnClick(ClickEvent e)
    {
        if (!Enabled)
            return;

        Clicked?.Invoke();
        e.Handled = true;
    }

    public override void ResetInteractionState()
    {
        Hovered = false;
        Pressed = false;
    }
}
