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

    /// <summary>Where a character in front of <paramref name="partner" /> shows in <paramref name="self" />.</summary>
    public static Vector2 WindowPoint(MirrorSegmentInfo self, MirrorSegmentInfo partner, Vector2 tile)
        => ReflectPoint(partner, tile) + new Vector2(self.X - partner.X, self.Y - partner.Y);

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
        var hash = Fnv1a(segmentId);
        var shifted = unixSeconds + hash % 17;
        var slot = (long)Math.Floor(shifted / HAUNTED_SLOT_SECONDS);
        var into = shifted - slot * HAUNTED_SLOT_SECONDS;
        var mixed = Mix(hash, slot);
        var start = 2 + mixed % 7;

        if ((into < start) || (into >= start + HAUNTED_SLIP_SECONDS))
            return (HauntedSlip.None, 0);

        return ((HauntedSlip)(1 + mixed / 7 % 3), into - start);
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
