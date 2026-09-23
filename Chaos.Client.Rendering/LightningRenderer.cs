#region
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Rendering;

/// <summary>
///     Ambient lightning overlay for maps carrying <c>MapFlags.Lightning</c>. While active, a strike fires every
///     <see cref="STRIKE_INTERVAL_MIN" />–<see cref="STRIKE_INTERVAL_MAX" /> seconds at a random point inside
///     the viewport. Each strike draws a brief full-screen white-blue flash; most also draw a procedurally
///     generated jagged bolt (midpoint-displacement polyline with forked branches) — no sprites. The rest are
///     flash-only, reading as distant lightning behind the clouds. Bolts are stored in WORLD space so they stay
///     anchored to the map while the camera pans. Everything draws additively so the scene brightens like real
///     lightning. Rain is separate: pair the flag with <c>MapFlags.Rain</c> for a thunderstorm. Touched only on
///     the game-loop thread.
/// </summary>
public sealed class LightningRenderer : IAmbientOverlay
{
    // ============================================================
    // Tunables — adjust for feel, rebuild to apply.
    // ============================================================

    // Seconds between strikes, rolled uniformly per strike.
    private const float STRIKE_INTERVAL_MIN = 4f;
    private const float STRIKE_INTERVAL_MAX = 12f;

    // Chance a strike is flash-only (distant lightning, no visible bolt).
    private const float SHEET_LIGHTNING_CHANCE = 0.35f;

    // Bolt on-screen lifetime and flash decay time (seconds).
    private const float BOLT_LIFETIME = 0.16f;
    private const float FLASH_DECAY   = 0.22f;

    // Flash: additive white-blue, peak strength [0..1]. Flash-only strikes use a dimmer peak.
    private static readonly Color FlashColor = new(150, 185, 255);
    private const float FLASH_PEAK       = 0.55f;
    private const float SHEET_FLASH_PEAK = 0.35f;

    // Bolt: a thick faint glow behind a thin bright core.
    private static readonly Color GlowColor = new(120, 160, 255);
    private static readonly Color CoreColor = new(235, 245, 255);
    private const float GLOW_THICKNESS = 4.5f;
    private const float CORE_THICKNESS = 1.6f;

    // Bolt geometry (WORLD pixels — the bolt drops from high above the strike point down onto it).
    private const float BOLT_HEIGHT   = 340f; // vertical distance from sky origin to the strike point
    private const float START_JITTER  = 80f;  // max horizontal offset of the sky origin from the strike point
    private const int   SUBDIVISIONS  = 6;    // midpoint passes (2^6 = 64 segments)
    private const float JAGGED_OFFSET = 55f;  // initial perpendicular displacement (px); halves each pass
    private const float BRANCH_CHANCE = 0.5f; // chance per interior point to sprout a fork
    private const int   MAX_BRANCHES  = 2;
    private const float BRANCH_LEN_MIN = 55f; // fork length range (world px)
    private const float BRANCH_LEN_MAX = 130f;

    // Strike points land inside this fraction of the viewport, so the bolt's top stays mostly on screen.
    private const float STRIKE_MARGIN_X   = 0.1f; // kept clear on the left and right edges
    private const float STRIKE_MIN_DEPTH  = 0.4f; // strikes land no higher than this far down the viewport
    private const float STRIKE_MARGIN_BOT = 0.1f; // kept clear at the bottom edge

    // ============================================================

    private readonly Random Rng = new();
    private readonly List<Vector2[]> Bolts = []; // polylines in WORLD coordinates

    private bool Active;
    private float Flash;        // current flash strength [0..1]
    private float FlashPeak;    // peak of the current flash, so decay time is the same for dim and bright flashes
    private float BoltAge;      // seconds since the current bolts were generated
    private float NextStrikeIn; // seconds until the next strike while active

    /// <inheritdoc />
    public BlendState BlendState => BlendState.Additive;

    /// <summary>True while active or a strike is still visible/fading — i.e. the screen should draw lightning.</summary>
    public bool IsActive => Active || (Flash > 0f) || (Bolts.Count > 0);

    /// <summary>
    ///     Arms/disarms the renderer. Disarming lets any in-flight strike finish, then goes idle; with
    ///     <paramref name="immediate" /> an in-flight strike is cleared too (used on map change). Arming rolls a
    ///     fresh delay, so the first strike never lands the instant the map loads.
    /// </summary>
    public void SetActive(bool on, bool immediate = false)
    {
        if (on && !Active)
            NextStrikeIn = RollInterval();

        Active = on;

        if (immediate && !on)
        {
            Flash = 0f;
            Bolts.Clear();
        }
    }

    /// <summary>
    ///     Advances the strike timer, flash decay and bolt age. Call once per frame. <paramref name="viewport" />
    ///     bounds where a new strike may land; <paramref name="worldOrigin" /> is the SCREEN position of world
    ///     pixel (0,0) (<c>Camera.WorldToScreen(Vector2.Zero)</c>), used to store the strike in world space.
    /// </summary>
    public void Update(GameTime gameTime, Rectangle viewport, Vector2 worldOrigin)
    {
        var dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        if (dt <= 0f)
            return;

        if (Flash > 0f)
            Flash = MathF.Max(0f, Flash - dt / FLASH_DECAY * FlashPeak);

        if (Bolts.Count > 0)
        {
            BoltAge += dt;

            if (BoltAge >= BOLT_LIFETIME)
                Bolts.Clear();
        }

        if (!Active || (viewport.Width <= 0) || (viewport.Height <= 0))
            return;

        NextStrikeIn -= dt;

        if (NextStrikeIn > 0f)
            return;

        NextStrikeIn = RollInterval();
        Strike(viewport, worldOrigin);
    }

    /// <summary>
    ///     Draws the flash + active bolts inside <paramref name="viewport" />.
    ///     <paramref name="worldOrigin" /> is the SCREEN position of world pixel (0,0)
    ///     (<c>Camera.WorldToScreen(Vector2.Zero)</c>) — bolts are stored in world space and converted
    ///     here, so they stay anchored to the map while the camera pans. The caller owns the surrounding
    ///     <c>SpriteBatch.Begin</c>/<c>End</c> and blend state (<see cref="BlendState.Additive" />).
    /// </summary>
    public void Draw(SpriteBatch spriteBatch, Rectangle viewport, Vector2 worldOrigin)
    {
        if ((viewport.Width <= 0) || (viewport.Height <= 0))
            return;

        if (Flash > 0f)
        {
            var flashA = (byte)Math.Clamp((int)(255f * Flash), 0, 255);
            RenderHelper.DrawRect(spriteBatch, viewport, new Color(FlashColor.R, FlashColor.G, FlashColor.B, flashA));
        }

        if ((Bolts.Count == 0) || (BoltAge >= BOLT_LIFETIME))
            return;

        var fade  = 1f - BoltAge / BOLT_LIFETIME;
        var alpha = (byte)Math.Clamp((int)(255f * fade), 0, 255);
        var glow  = new Color(GlowColor.R, GlowColor.G, GlowColor.B, alpha);
        var core  = new Color(CoreColor.R, CoreColor.G, CoreColor.B, alpha);

        // all glow segments first, then all cores, so bright cores sit on top of every glow
        foreach (var bolt in Bolts)
            for (var i = 0; i < bolt.Length - 1; i++)
                RenderHelper.DrawLine(spriteBatch, bolt[i] + worldOrigin, bolt[i + 1] + worldOrigin, glow, GLOW_THICKNESS);

        foreach (var bolt in Bolts)
            for (var i = 0; i < bolt.Length - 1; i++)
                RenderHelper.DrawLine(spriteBatch, bolt[i] + worldOrigin, bolt[i + 1] + worldOrigin, core, CORE_THICKNESS);
    }

    /// <inheritdoc />
    public void Dispose() { } // no textures of its own — draws with RenderHelper's shared pixel

    // ============================================================

    private float RollInterval()
        => STRIKE_INTERVAL_MIN + (float)Rng.NextDouble() * (STRIKE_INTERVAL_MAX - STRIKE_INTERVAL_MIN);

    // Fires one strike: always a flash; a bolt too unless this roll is sheet lightning.
    private void Strike(Rectangle viewport, Vector2 worldOrigin)
    {
        Bolts.Clear();
        BoltAge = 0f;

        if (Rng.NextDouble() < SHEET_LIGHTNING_CHANCE)
        {
            FlashPeak = SHEET_FLASH_PEAK;
            Flash = FlashPeak;

            return;
        }

        FlashPeak = FLASH_PEAK;
        Flash = FlashPeak;

        var minX = viewport.X + viewport.Width * STRIKE_MARGIN_X;
        var maxX = viewport.Right - viewport.Width * STRIKE_MARGIN_X;
        var minY = viewport.Y + viewport.Height * STRIKE_MIN_DEPTH;
        var maxY = viewport.Bottom - viewport.Height * STRIKE_MARGIN_BOT;

        var screenPoint = new Vector2(
            minX + (float)Rng.NextDouble() * (maxX - minX),
            minY + (float)Rng.NextDouble() * (maxY - minY));

        AddBoltAt(screenPoint - worldOrigin);
    }

    // Builds the main bolt from a jittered sky origin down to the strike point, plus hanging forks.
    // All coordinates are WORLD pixels; Draw converts to screen via the camera's world origin.
    private void AddBoltAt(Vector2 end)
    {
        var start = end + new Vector2(((float)Rng.NextDouble() * 2f - 1f) * START_JITTER, -BOLT_HEIGHT);

        var main = GenerateBolt(start, end, JAGGED_OFFSET, SUBDIVISIONS);
        Bolts.Add(main);

        var branches = 0;

        for (var i = 1; (i < main.Length - 1) && (branches < MAX_BRANCHES); i++)
        {
            if (Rng.NextDouble() >= BRANCH_CHANCE)
                continue;

            var origin = main[i];
            var dir    = main[i] - main[i - 1];
            var angle  = MathF.Atan2(dir.Y, dir.X) + ((float)Rng.NextDouble() * 2f - 1f) * 0.7f;
            var len    = BRANCH_LEN_MIN + (float)Rng.NextDouble() * (BRANCH_LEN_MAX - BRANCH_LEN_MIN);

            // bias the fork downward so branches hang off the bolt naturally
            var tip = origin + new Vector2(MathF.Cos(angle), MathF.Abs(MathF.Sin(angle))) * len;

            Bolts.Add(GenerateBolt(origin, tip, JAGGED_OFFSET * 0.5f, SUBDIVISIONS - 2));
            branches++;
        }
    }

    // Recursive midpoint displacement: returns a jagged polyline from start to end.
    private Vector2[] GenerateBolt(Vector2 start, Vector2 end, float offset, int subdivisions)
    {
        var points = new List<Vector2> { start, end };

        for (var s = 0; s < subdivisions; s++)
        {
            var next = new List<Vector2>(points.Count * 2);

            for (var i = 0; i < points.Count - 1; i++)
            {
                var a    = points[i];
                var b    = points[i + 1];
                var seg  = b - a;
                var perp = new Vector2(-seg.Y, seg.X);

                if (perp.Length() > 0.001f)
                    perp.Normalize();

                var mid = (a + b) * 0.5f + perp * (((float)Rng.NextDouble() * 2f - 1f) * offset);

                next.Add(a);
                next.Add(mid);
            }

            next.Add(points[^1]);
            points = next;
            offset *= 0.5f;
        }

        return points.ToArray();
    }
}
