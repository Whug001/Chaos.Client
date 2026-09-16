#region
using Chaos.Client.Collections;
using Chaos.Client.Controls.Components;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.ViewPort;

/// <summary>
///     How much air the player has left, 0-100, shown only while they are in the water. A small meter rather than a
///     strip: it sits beside the class-resource strip instead of stacking with it, because oxygen is not a fifth
///     class resource -- a Berserker underwater carries Rage and air at the same time and needs to read both.
/// </summary>
/// <remarks>
///     Sized a little larger than the entity <see cref="HealthBar" /> it is modelled on (32x7 against 27x5), and
///     drawn the same way: a frame with a filled interior, no label. There is no room for text at this size and none
///     is needed -- a meter draining toward empty is the whole message.
///     <para />
///     Visible whenever <see cref="WorldState.Oxygen" /> holds a value, zero included. An empty bar is the drowning
///     warning: it is the only standing sign that the health going missing is the water rather than something in the
///     room, so it must not vanish at the moment it matters most. What ends it is leaving the zone, which clears the
///     state.
/// </remarks>
public sealed class OxygenBarControl : UIElement
{
    /// <summary>Outer size, including the frame. A little larger than the 27x5 health bar, as asked.</summary>
    public const int TOTAL_WIDTH = 32;

    public const int TOTAL_HEIGHT = 7;

    private const int INNER_WIDTH = TOTAL_WIDTH - 2;
    private const int INNER_HEIGHT = TOTAL_HEIGHT - 2;

    private static readonly Color FrameColor = Color.Black;

    //the interior behind the fill, so a meter near empty still reads as a bar rather than as an empty outline
    private static readonly Color BackdropColor = new(0, 0, 0, 128);

    //water blue, and nowhere near any of the four class-resource colours (green, red, gold, violet) -- the two
    //bars sit side by side and must never be mistaken for each other
    private static readonly Color FillColor = new(64, 148, 232, 230);

    public OxygenBarControl()
    {
        Width = TOTAL_WIDTH;
        Height = TOTAL_HEIGHT;
        Visible = false;
    }

    /// <summary>
    ///     Settles visibility for this frame from <see cref="WorldState.Oxygen" />. Positioning is the caller's --
    ///     <c>WorldScreen.DrawStatusStrips</c> places this against the active hud every frame, the same as the
    ///     strips it sits beside.
    /// </summary>
    public override void Update(GameTime gameTime) => Visible = WorldState.Oxygen.HasValue;

    /// <inheritdoc />
    public override void Draw(SpriteBatch spriteBatch)
    {
        if (!Visible)
            return;

        var bounds = new Rectangle(
            ScreenX,
            ScreenY,
            TOTAL_WIDTH,
            TOTAL_HEIGHT);

        DrawRect(spriteBatch, bounds, BackdropColor);
        DrawBorder(spriteBatch, bounds, FrameColor);

        var fillWidth = INNER_WIDTH * WorldState.Oxygen.Amount / 100;

        if (fillWidth <= 0)
            return;

        DrawRect(
            spriteBatch,
            new Rectangle(
                ScreenX + 1,
                ScreenY + 1,
                fillWidth,
                INNER_HEIGHT),
            FillColor);
    }
}
