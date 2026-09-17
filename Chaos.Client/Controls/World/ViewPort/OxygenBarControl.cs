#region
using Chaos.Client.Collections;
using Chaos.Client.Controls.Components;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.ViewPort;

/// <summary>
///     How much air the player has left, 0-100, shown only while they are in the water. A bar with
///     "Oxygen 73/100" written inside it.
/// </summary>
/// <remarks>
///     Sits in the status strip column on the left, above the hp and mp orbs, as the top row of the stack that
///     holds the class resource strip and the bard song strip. Drowning is the one thing on screen a player must
///     not miss, and that column is where a player already looks for a resource that is running out.
///     <para />
///     Only as tall as the text it holds, which is a row of the standard 12px font plus its frame. The bar is long
///     rather than thick: length is what carries the reading, and thickness would only take viewport.
///     <para />
///     Visible whenever <see cref="WorldState.Oxygen" /> holds a value, zero included. An empty bar is the drowning
///     warning: it is the only standing sign that the health going missing is the water rather than something in the
///     room, so it must not vanish at the moment it matters most. What ends it is leaving the zone, which clears the
///     state.
/// </remarks>
public sealed class OxygenBarControl : UIElement
{
    /// <summary>A row of text and a pixel of frame above and below it. Nothing is gained by making it thicker.</summary>
    public const int TOTAL_HEIGHT = TextRenderer.CHAR_HEIGHT + 2;

    /// <summary>
    ///     The least the bar may be squeezed to: the widest label it ever draws, plus its frame.
    /// </summary>
    /// <remarks>
    ///     "Oxygen 100/100" is fourteen characters and the font is fixed-advance, so this is exact rather than an
    ///     estimate. The strip column is far wider than this today, so this is a floor that only matters if a hud
    ///     layout ever gives the strips a narrower column.
    /// </remarks>
    public const int MINIMUM_WIDTH = (14 * TextRenderer.CHAR_WIDTH) + 2;

    private const int MAX_OXYGEN = 100;

    /// <summary>
    ///     How fast the drawn fill chases the server's number, in percentage points per second.
    /// </summary>
    /// <remarks>
    ///     The server reports whole percentage points at whatever interval the effect ticks, so the raw value
    ///     arrives in visible steps. Easing toward it makes the bar read as draining rather than as jumping, and at
    ///     this rate a single point takes about a sixth of a second -- slow enough to see, fast enough that the bar
    ///     is never meaningfully behind the truth.
    /// </remarks>
    private const float DRAIN_RATE = 6f;

    private static readonly Color FrameColor = Color.Black;

    //the interior behind the fill, so a meter near empty still reads as a bar rather than as an empty outline
    private static readonly Color BackdropColor = new(0, 0, 0, 160);

    //water blue, and nowhere near any of the four class-resource colours (green, red, gold, violet)
    private static readonly Color FillColor = new(64, 148, 232, 230);

    private static readonly Color TextColor = Color.White;
    private static readonly Color ShadowColor = Color.Black;

    /// <summary>What is actually drawn, which chases <see cref="OxygenState.Amount" /> rather than matching it.</summary>
    private float DisplayedAmount;

    public OxygenBarControl()
    {
        Width = MINIMUM_WIDTH;
        Height = TOTAL_HEIGHT;
        Visible = false;
        IsHitTestVisible = false;
    }

    /// <summary>
    ///     Puts the bar in the row the caller has measured for it. Position is the caller's because it depends on
    ///     the strip column of whichever hud layout is up, and on how many of the strips below it are showing.
    /// </summary>
    public void SetBounds(int x, int y, int width)
    {
        X = x;
        Y = y;
        Width = Math.Max(width, MINIMUM_WIDTH);
    }

    /// <summary>
    ///     Settles visibility and eases the drawn fill toward the reported one.
    /// </summary>
    public override void Update(GameTime gameTime)
    {
        Visible = WorldState.Oxygen.HasValue;

        if (!Visible)
        {
            //snapped rather than eased, so surfacing and going back under starts from the real value instead of
            //animating up from wherever the bar happened to be when it was hidden
            DisplayedAmount = 0;

            return;
        }

        var target = (float)WorldState.Oxygen.Amount;
        var step = DRAIN_RATE * (float)gameTime.ElapsedGameTime.TotalSeconds;

        //a first reading has nothing to ease from, and a refill is not a drain -- both go straight to the value
        if ((DisplayedAmount <= 0f) || (target > DisplayedAmount))
        {
            DisplayedAmount = target;

            return;
        }

        DisplayedAmount = Math.Max(target, DisplayedAmount - step);
    }

    /// <inheritdoc />
    public override void Draw(SpriteBatch spriteBatch)
    {
        if (!Visible)
            return;

        var bounds = new Rectangle(
            ScreenX,
            ScreenY,
            Width,
            TOTAL_HEIGHT);

        DrawRect(spriteBatch, bounds, BackdropColor);

        var innerWidth = Width - 2;
        var fillWidth = (int)Math.Round(innerWidth * DisplayedAmount / MAX_OXYGEN);

        if (fillWidth > 0)
            DrawRect(
                spriteBatch,
                new Rectangle(
                    ScreenX + 1,
                    ScreenY + 1,
                    Math.Min(fillWidth, innerWidth),
                    TOTAL_HEIGHT - 2),
                FillColor);

        DrawBorder(spriteBatch, bounds, FrameColor);

        //the label reads the server's number, not the eased one: the bar may still be sliding down to it, but the
        //figure a player reads should be the figure they have
        var text = $"Oxygen {WorldState.Oxygen.Amount}/{MAX_OXYGEN}";
        var textWidth = TextRenderer.MeasureWidth(text);

        TextRenderer.DrawShadowedText(
            spriteBatch,
            new Vector2(ScreenX + ((Width - textWidth) / 2), ScreenY + 1),
            text,
            TextColor,
            ShadowColor);
    }
}
