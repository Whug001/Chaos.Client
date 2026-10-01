#region
using Chaos.DarkAges.Definitions;
using Chaos.Geometry.Abstractions.Definitions;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Systems;

/// <summary>A haunted mirror's current trick.</summary>
public enum HauntedSlip : byte
{
    None,
    Lag,
    Stare,
    Ghost
}

/// <summary>Where a double is in its life.</summary>
public enum DoublePhase : byte
{
    Climbing,
    Following,
    Fading,
    Done
}

/// <summary>
///     The mirror renderer's pure maths: reflected positions and facings, the styles' numbers, the haunted schedule, the
///     double's timeline and the render cache ids of mirrored copies. Tile positions are tile-space floats.
/// </summary>
public static class MirrorMath
{
    public const int REFLECT_DEPTH = 4;
    public const int REFLECT_MARGIN = 1;
    public const int SPRITE_CAP = 40;

    public const float GLASS_ALPHA = 0.6f;
    public const float WINDOW_ALPHA = 0.7f;
    public const double GLINT_PERIOD_SECONDS = 9;
    public const double GLINT_SWEEP_SECONDS = 3;

    public const double HAUNTED_SLOT_SECONDS = 17;
    public const double HAUNTED_SLIP_SECONDS = 3;
    public const double HAUNTED_LAG_SECONDS = 1.1;

    /// <summary>One haunted slip in this many is a fullscreen scare instead of a reflection trick.</summary>
    public const int SCARE_ONE_IN = 6;

    public const double SCARE_COOLDOWN_SECONDS = 60;
    public const int SCARE_FRAMES = 4;
    public const double SCARE_FRAME_SECONDS = 0.1;

    public const int ENDLESS_COPIES = 4;
    public const float ENDLESS_STEP_TILES = 0.9f;

    public const double DOUBLE_CLIMB_SECONDS = 0.6;
    public const double DOUBLE_FOLLOW_SECONDS = 6;
    public const double DOUBLE_FADE_SECONDS = 1;
    public const double DOUBLE_LAG_SECONDS = 0.45;
    public const float DOUBLE_MAX_ALPHA = 0.45f;

    /// <summary>The wall tile at <paramref name="index" /> along a run.</summary>
    public static Point WallTile(MirrorSegmentInfo segment, int index)
        => segment.Side == MirrorSide.North ? new Point(segment.X + index, segment.Y) : new Point(segment.X, segment.Y + index);

    /// <summary>True when tile (x, y) is 1 to <paramref name="depth" /> tiles in front of the run, within its span ± margin.</summary>
    public static bool IsInFront(MirrorSegmentInfo segment, int x, int y, int depth, int margin)
        => segment.Side == MirrorSide.North
            ? (y > segment.Y) && (y <= segment.Y + depth) && (x >= segment.X - margin) && (x <= segment.X + segment.Length - 1 + margin)
            : (x > segment.X) && (x <= segment.X + depth) && (y >= segment.Y - margin) && (y <= segment.Y + segment.Length - 1 + margin);

    /// <summary>
    ///     True when no wall stands between tile (x, y) and the run, looking straight at the glass. A tile in front of a
    ///     run but past another wall (the next corridor over) has no clear view.
    /// </summary>
    public static bool HasClearView(MirrorSegmentInfo segment, int x, int y, Func<int, int, bool> isWall)
    {
        if (segment.Side == MirrorSide.North)
        {
            for (var ty = segment.Y + 1; ty < y; ty++)
                if (isWall(x, ty))
                    return false;
        } else
            for (var tx = segment.X + 1; tx < x; tx++)
                if (isWall(tx, y))
                    return false;

        return true;
    }

    /// <summary>
    ///     True when the local player at (x, y) should see themself in the run: in front of it with a clear view. A window
    ///     shows its partner's side of the maze, so it never shows the local player. Other players see every reflection.
    /// </summary>
    public static bool ShowsLocalPlayer(MirrorSegmentInfo segment, int x, int y, Func<int, int, bool> isWall)
        => (segment.Style != MirrorStyle.Window)
           && IsInFront(segment, x, y, REFLECT_DEPTH, REFLECT_MARGIN)
           && HasClearView(segment, x, y, isWall);

    public static Vector2 ReflectPoint(MirrorSegmentInfo segment, Vector2 tile)
        => segment.Side == MirrorSide.North
            ? new Vector2(tile.X, 2 * segment.Y + 1 - tile.Y)
            : new Vector2(2 * segment.X + 1 - tile.X, tile.Y);

    public static Direction ReflectFacing(MirrorSide side, Direction facing)
        => side == MirrorSide.North
            ? facing switch
            {
                Direction.Up   => Direction.Down,
                Direction.Down => Direction.Up,
                _              => facing
            }
            : facing switch
            {
                Direction.Left  => Direction.Right,
                Direction.Right => Direction.Left,
                _               => facing
            };

    /// <summary>The facing that looks out of the glass at the viewer.</summary>
    public static Direction FacingOutOf(MirrorSide side) => side == MirrorSide.North ? Direction.Down : Direction.Right;

    /// <summary>A world-pixel offset (like <c>WorldEntity.VisualOffset</c>) in tiles.</summary>
    public static Vector2 OffsetToTiles(Vector2 worldOffset)
    {
        var a = worldOffset.X / 28f;
        var b = worldOffset.Y / 14f;

        return new Vector2((a + b) / 2f, (b - a) / 2f);
    }

    /// <summary>The world pixel at the centre of a tile-space position, as <c>Camera.TileToWorld</c> + half a tile.</summary>
    public static Vector2 TileCenterWorld(Vector2 tile, int mapHeight)
        => new((mapHeight - 1 + tile.X - tile.Y) * 28f + 28f, (tile.X + tile.Y) * 14f + 14f);

    public static Vector2 EndlessCopy(MirrorSide side, Vector2 reflected, int copy)
        => side == MirrorSide.North
            ? new Vector2(reflected.X, reflected.Y - ENDLESS_STEP_TILES * copy)
            : new Vector2(reflected.X - ENDLESS_STEP_TILES * copy, reflected.Y);

    public static float EndlessScale(int copy) => MathF.Pow(0.86f, copy);

    public static float EndlessAlpha(int copy) => GLASS_ALPHA * MathF.Pow(0.62f, copy);

    public static Direction EndlessFacing(int copy, Direction original, Direction reflected) => copy % 2 == 1 ? original : reflected;

    /// <summary>
    ///     Where a character standing at <paramref name="tile" /> in front of <paramref name="partner" /> shows in
    ///     <paramref name="self" />. The runs differ by their first wall tile, so the character is slid by that
    ///     difference. A window does not reflect.
    /// </summary>
    public static Vector2 WindowPoint(MirrorSegmentInfo self, MirrorSegmentInfo partner, Vector2 tile)
        => new(tile.X + self.X - partner.X, tile.Y + self.Y - partner.Y);

    public static (float X, float Y) FunhouseScale(MirrorFunhouse kind)
        => kind switch
        {
            MirrorFunhouse.Tall => (0.75f, 1.5f),
            MirrorFunhouse.Wide => (1.35f, 0.7f),
            _                   => (1f, 1f)
        };

    /// <summary>Sideways shift of a 2-pixel row of a funhouse reflection.</summary>
    public static float RippleOffset(double seconds, int row) => (float)(Math.Sin(seconds * 5 + row * 0.18) * 3);

    /// <summary>
    ///     How far along its run the glint is, from -0.3 to 1.3, during the first 3 s of every 9 s; null the rest of
    ///     the time.
    /// </summary>
    public static float? GlintFraction(double seconds)
    {
        var into = seconds % GLINT_PERIOD_SECONDS;

        return into >= GLINT_SWEEP_SECONDS ? null : (float)(into / GLINT_SWEEP_SECONDS * 1.6 - 0.3);
    }

    /// <summary>
    ///     The haunted schedule. Time is cut into 17 s slots (offset per run); in each slot one 3 s slip starts 2-8 s
    ///     in, so slips are 8-20 s apart. Everything comes from the run id and the clock, so every client agrees.
    /// </summary>
    public static (HauntedSlip Kind, double SecondsInto) HauntedSlipAt(string segmentId, double unixSeconds)
    {
        var clock = Clock(segmentId, unixSeconds);
        var mixed = Mix(clock.Hash, clock.Slot);
        var start = 2 + mixed % 7;

        if ((clock.Into < start) || (clock.Into >= start + HAUNTED_SLIP_SECONDS))
            return (HauntedSlip.None, 0);

        return ((HauntedSlip)(1 + mixed / 7 % 3), clock.Into - start);
    }

    /// <summary>The 17 s haunted slot containing <paramref name="unixSeconds" />. Scare cooldown remembers it.</summary>
    public static long HauntedSlot(string segmentId, double unixSeconds) => Clock(segmentId, unixSeconds).Slot;

    /// <summary>True for the whole slot when this slip is a scare instead of lag, stare, or ghost. About one slot in six.</summary>
    public static bool IsScareSlot(string segmentId, double unixSeconds)
    {
        var clock = Clock(segmentId, unixSeconds);

        return Mix(clock.Hash, clock.Slot) % SCARE_ONE_IN == 0;
    }

    /// <summary>True during the 3 s slip of a scare slot: the moment the face may lunge.</summary>
    public static bool InScareWindow(string segmentId, double unixSeconds)
        => IsScareSlot(segmentId, unixSeconds) && (HauntedSlipAt(segmentId, unixSeconds).Kind != HauntedSlip.None);

    /// <summary>Which of the four scare frames <paramref name="secondsInto" /> shows, or -1 when the scare is over.</summary>
    public static int ScareFrame(double secondsInto)
    {
        if (secondsInto < 0)
            return -1;

        var frame = (int)(secondsInto / SCARE_FRAME_SECONDS);

        return frame < SCARE_FRAMES ? frame : -1;
    }

    private readonly record struct HauntedClock(uint Hash, long Slot, double Into);

    private static HauntedClock Clock(string segmentId, double unixSeconds)
    {
        var hash = Fnv1a(segmentId);
        var shifted = unixSeconds + hash % 17;
        var slot = (long)Math.Floor(shifted / HAUNTED_SLOT_SECONDS);

        return new HauntedClock(hash, slot, shifted - slot * HAUNTED_SLOT_SECONDS);
    }

    /// <summary>The ghost slip's opacity: 0.2 to 0.5, once a second.</summary>
    public static float GhostAlpha(double seconds) => 0.35f + 0.15f * (float)Math.Sin(seconds * 2 * Math.PI);

    public static (DoublePhase Phase, float Progress, float Alpha) DoubleAt(double seconds)
    {
        if (seconds < DOUBLE_CLIMB_SECONDS)
        {
            var p = (float)(seconds / DOUBLE_CLIMB_SECONDS);

            return (DoublePhase.Climbing, p, DOUBLE_MAX_ALPHA * p);
        }

        seconds -= DOUBLE_CLIMB_SECONDS;

        if (seconds < DOUBLE_FOLLOW_SECONDS)
            return (DoublePhase.Following, (float)(seconds / DOUBLE_FOLLOW_SECONDS), DOUBLE_MAX_ALPHA);

        seconds -= DOUBLE_FOLLOW_SECONDS;

        if (seconds < DOUBLE_FADE_SECONDS)
        {
            var p = (float)(seconds / DOUBLE_FADE_SECONDS);

            return (DoublePhase.Fading, p, DOUBLE_MAX_ALPHA * (1 - p));
        }

        return (DoublePhase.Done, 1, 0);
    }

    /// <summary>
    ///     The render cache id of a mirrored copy: top bit set, so it never meets a real entity id, then the entity id,
    ///     the facing and whether it is posed idle.
    /// </summary>
    public static uint MirrorCacheId(uint entityId, Direction facing, bool idle)
        => 0x8000_0000u | ((entityId & 0x0FFF_FFFFu) << 3) | ((uint)facing << 1) | (idle ? 1u : 0u);

    public static uint Fnv1a(string text)
    {
        var hash = 2166136261u;

        foreach (var c in text.ToLowerInvariant())
            unchecked
            {
                hash ^= c;
                hash *= 16777619u;
            }

        return hash;
    }

    private static uint Mix(uint hash, long slot)
    {
        unchecked
        {
            var x = hash ^ (uint)(slot * 0x9E3779B1L);
            x ^= x >> 16;
            x *= 0x85EBCA6Bu;
            x ^= x >> 13;
            x *= 0xC2B2AE35u;
            x ^= x >> 16;

            return x;
        }
    }
}
