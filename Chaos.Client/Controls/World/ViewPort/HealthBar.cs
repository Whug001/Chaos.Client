#region
using Chaos.Client.Collections;
using Chaos.Client.Controls.Components;
using Chaos.Client.Definitions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.ViewPort;

public sealed class HealthBar : UIElement
{
    private const int TOTAL_WIDTH = 27;
    private const int TOTAL_HEIGHT = 5;
    private const int INNER_WIDTH = TOTAL_WIDTH - 2;
    private const int INNER_HEIGHT = TOTAL_HEIGHT - 2;

    //barrier sub-bar: a shorter second bar stacked above the health bar, overlapping it by one row so the two share
    //a frame line and read as one widget. Only drawn while the entity actually has a barrier.
    private const int BARRIER_TOTAL_HEIGHT = 4;
    private const int BARRIER_INNER_HEIGHT = BARRIER_TOTAL_HEIGHT - 2;

    private const float DURATION_MS = 2000f;

    private static readonly Color FrameColor = Color.Black;
    private static readonly Color HighColor = new(0, 97, 0);
    private static readonly Color MidColor = new(247, 142, 24);
    private static readonly Color LowColor = new(206, 0, 16);

    private float ElapsedMs;
    public byte HealthPercent { get; set; }

    public uint EntityId { get; }
    public bool IsExpired => ElapsedMs >= DURATION_MS;

    public HealthBar(uint entityId, byte healthPercent)
    {
        EntityId = entityId;
        HealthPercent = healthPercent;
        Width = TOTAL_WIDTH;

        //covers the health bar only. The barrier sub-bar hangs above ScreenY, outside these bounds, and so is drawn
        //with the unclipped statics — see Draw.
        Height = TOTAL_HEIGHT;
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        if (!Visible)
            return;

        UpdateClipRect();

        if ((ClipRect.Width <= 0) || (ClipRect.Height <= 0))
            return;

        var innerX = ScreenX + 1;
        var innerY = ScreenY + 1;
        var fillWidth = (int)(INNER_WIDTH * (HealthPercent / 100f));

        //border only — unfilled area is transparent
        DrawBorder(
            spriteBatch,
            new Rectangle(
                ScreenX,
                ScreenY,
                TOTAL_WIDTH,
                TOTAL_HEIGHT),
            FrameColor);

        //fill
        if (fillWidth > 0)
        {
            var fillColor = HealthPercent switch
            {
                > 52 => HighColor,
                > 24 => MidColor,
                _    => LowColor
            };

            DrawRectClipped(
                spriteBatch,
                new Rectangle(
                    innerX,
                    innerY,
                    fillWidth,
                    INNER_HEIGHT),
                fillColor);
        }

        //barrier sub-bar — its own bar above the health bar rather than an overlay on top of it, so health and
        //barrier are both readable at once. Scaled against the barrier's own peak rather than the entity's max
        //health, so a freshly granted barrier spans the full width and drains from there.
        //read at draw time rather than cached: the barrier packet and the health bar packet arrive in the same
        //batch with no guaranteed ordering, so anything captured when this bar was created could be a frame stale
        var barrierFraction = WorldState.GetEntity(EntityId)
                                        ?.Barrier.Fraction
                              ?? 0f;

        if (barrierFraction <= 0f)
            return;

        //sits above ScreenY, overlapping the health bar's top border row so the two frames merge into a single
        //divider line. Drawn with the unclipped statics (as the health bar's own frame already is) because it falls
        //outside this element's bounds, and therefore outside its clip rect — the viewport scissor still contains it.
        var barrierY = (ScreenY - BARRIER_TOTAL_HEIGHT) + 1;

        DrawBorder(
            spriteBatch,
            new Rectangle(
                ScreenX,
                barrierY,
                TOTAL_WIDTH,
                BARRIER_TOTAL_HEIGHT),
            FrameColor);

        //ceiling, floored at one pixel — a nearly-spent barrier is still a barrier and must not blink out early
        var barrierWidth = Math.Clamp((int)MathF.Ceiling(INNER_WIDTH * barrierFraction), 1, INNER_WIDTH);

        DrawRect(
            spriteBatch,
            new Rectangle(
                innerX,
                barrierY + 1,
                barrierWidth,
                BARRIER_INNER_HEIGHT),
            Constants.BarrierColor);
    }

    public void Reset(byte healthPercent)
    {
        HealthPercent = healthPercent;
        ElapsedMs = 0;
    }

    public override void Update(GameTime gameTime) => ElapsedMs += (float)gameTime.ElapsedGameTime.TotalMilliseconds;
}