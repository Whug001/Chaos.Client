#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Definitions;
using Chaos.Client.Rendering.Definitions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Popups.Beauty;

/// <summary>
///     One dot per page, the current one gold. Each dot's click target is its whole <see cref="SPACING" />-wide slot.
///     Clicking a dot other than the current one raises <see cref="PageChosen" />.
/// </summary>
public sealed class PageDots : UIElement
{
    public const int DOT = 8;
    public const int SPACING = 16;

    private readonly int Count;
    private int Current;
    private int? Hovered;

    public event Action<int>? PageChosen;

    public PageDots(int count)
    {
        Count = count;
        Width = count * SPACING;
        Height = SPACING;
    }

    public void SetCurrent(int index) => Current = index;

    private int? DotAt(int localX) => (localX >= 0) && (localX < Width) ? localX / SPACING : null;

    public override void OnMouseMove(MouseMoveEvent e) => Hovered = DotAt(e.ScreenX - ScreenX);

    public override void OnMouseLeave() => Hovered = null;

    public override void ResetInteractionState() => Hovered = null;

    public override void OnClick(ClickEvent e)
    {
        e.Handled = true;

        if (DotAt(e.ScreenX - ScreenX) is { } index && (index != Current))
            PageChosen?.Invoke(index);
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        if (!Visible)
            return;

        //primes ClipRect, which hit-testing (ContainsPoint) depends on
        base.Draw(spriteBatch);

        for (var i = 0; i < Count; i++)
        {
            var x = ScreenX + (i * SPACING) + ((SPACING - DOT) / 2);
            var y = ScreenY + ((SPACING - DOT) / 2);

            var fill = i == Current ? LegendColors.Gold
                : i == Hovered ? LegendColors.Silver
                : LegendColors.DarkGray;

            //two overlapping rects with the corners left out read as a round dot at this size
            DrawRect(spriteBatch, new Rectangle(x + 1, y, DOT - 2, DOT), fill);
            DrawRect(spriteBatch, new Rectangle(x, y + 1, DOT, DOT - 2), fill);
        }
    }
}
