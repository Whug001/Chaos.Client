#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Popups;

/// <summary>
///     The small "Move" button on a raid row of the Y window. Drawn as text in a box: the group dialog's art has no move
///     button to borrow.
/// </summary>
public sealed class RaidMoveButton : UIElement
{
    private const string LABEL = "Move";

    private static readonly Color Fill = new(54, 34, 16);
    private static readonly Color Edge = new(150, 120, 80);
    private static readonly Color LabelColor = new(232, 220, 196);

    public event ClickedHandler? Clicked;

    public override void OnClick(ClickEvent e)
    {
        if (e.Button != MouseButton.Left)
            return;

        Clicked?.Invoke();
        e.Handled = true;
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        if (!Visible)
            return;

        UpdateClipRect();

        var bounds = new Rectangle(
            ScreenX,
            ScreenY,
            Width,
            Height);

        DrawRect(spriteBatch, bounds, Fill);
        DrawBorder(spriteBatch, bounds, Edge);

        TextRenderer.DrawShadowedText(
            spriteBatch,
            new Vector2(ScreenX + ((Width - TextRenderer.MeasureWidth(LABEL)) / 2), ScreenY + ((Height - TextRenderer.CHAR_HEIGHT) / 2)),
            LABEL,
            LabelColor,
            Color.Black);
    }
}
