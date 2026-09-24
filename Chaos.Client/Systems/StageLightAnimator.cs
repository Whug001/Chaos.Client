#region
using Chaos.Client.Collections;
using Chaos.Client.Utilities;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Systems;

/// <summary>One spotlight as it looks this frame. <see cref="Tile" /> is a fractional map tile; whole numbers are tile centres.</summary>
public readonly record struct StageLightFrame(
    byte Id,
    Vector2 Tile,
    Color Color,
    float Strength,
    StageLightSize Size,
    bool Beam);

/// <summary>
///     Holds the Theatre lighting setup the server last sent and works out, for any moment, where each spotlight is, what
///     colour it is and how strong it is. Blends from the old look to a new setup over the setup's fade time. Plain math
///     with no drawing, so it is unit-tested directly. Times are milliseconds from any steady clock
///     (<c>Environment.TickCount64</c> in the game).
/// </summary>
public sealed class StageLightAnimator
{
    /// <summary>How long a released drag keeps its local copy while waiting for the server to match it.</summary>
    public const int RELEASE_TIMEOUT_MS = 1000;

    //speed 1..5 → cycle lengths; the spec's Speeds table
    private static readonly float[] SweepMs = [16000, 11000, 8000, 5500, 4000];
    private static readonly float[] PulseMs = [4000, 3000, 2000, 1400, 1000];
    private static readonly float[] FlickerPerSecond = [4, 7, 10, 14, 20];
    private static readonly float[] CycleMs = [24000, 16000, 10000, 6000, 3000];

    private readonly Dictionary<byte, Target> Targets = [];
    private readonly Dictionary<byte, StageLightInfo> Held = [];
    private readonly Dictionary<byte, long> Releasing = [];
    private readonly Dictionary<byte, StageLightFrame> FadeFrom = [];
    private readonly List<FadingEntry> FadingOut = [];
    private readonly List<StageLightInfo> LightList = [];

    private Func<uint, Vector2?> LastLookup = _ => null;
    private float FromHouseDarkness;
    private float ToHouseDarkness;
    private long FadeStartMs;
    private int FadeMs;

    public bool HasSetup { get; private set; }
    public Rectangle Stage { get; private set; }
    public byte HouseLevel { get; private set; } = 100;
    public bool StageGlow { get; private set; } = true;

    /// <summary>The lights in id order. A light the director is dragging shows its local copy.</summary>
    public IReadOnlyList<StageLightInfo> Lights => LightList;


    /// <summary>The frames the most recent <see cref="Evaluate" /> produced; the Stage Lighting window draws these instead of evaluating again.</summary>
    public IReadOnlyList<StageLightFrame> LatestFrames { get; private set; } = [];

    public void Apply(StageLightingStateArgs args, long nowMs)
    {
        //the look that shows right now, before anything changes: every active light's in-progress
        //blend plus every fading-out light's decayed strength. This is what the new setup fades in from.
        var snapshot = new Dictionary<byte, StageLightFrame>();

        if (HasSetup)
        {
            foreach (var frame in ComputeActiveFrames(nowMs))
                snapshot[frame.Id] = frame;

            foreach (var entry in FadingOut)
                snapshot[entry.Frame.Id] = entry.CurrentFrame(nowMs);
        }

        FromHouseDarkness = HasSetup ? CurrentHouseDarkness(nowMs) : Darkness(args.HouseLevel);

        FadeFrom.Clear();

        foreach (var (id, frame) in snapshot)
            FadeFrom[id] = frame;

        var incoming = new HashSet<byte>();

        foreach (var state in args.Lights)
        {
            var id = state.Light.Id;
            incoming.Add(id);
            Targets[id] = new Target(state.Light, nowMs - state.MotionElapsedMs, nowMs - state.EffectElapsedMs);

            //a released drag ends once the server's copy matches what the director let go of
            if (Releasing.ContainsKey(id) && Held.TryGetValue(id, out var held) && (held == state.Light))
                EndHold(id);
        }

        //a light that reappears keeps fading from its current decayed look instead of restarting from it
        FadingOut.RemoveAll(entry => incoming.Contains(entry.Frame.Id));

        foreach (var id in Targets.Keys.Where(id => !incoming.Contains(id)).ToList())
        {
            //fades out on its own clock, unaffected by whatever fade this or a later Apply carries
            if (snapshot.TryGetValue(id, out var live))
                FadingOut.Add(new FadingEntry(live, nowMs, args.FadeMs));

            Targets.Remove(id);
            EndHold(id);
        }

        Stage = new Rectangle(args.StageX, args.StageY, args.StageWidth, args.StageHeight);
        HouseLevel = args.HouseLevel;
        StageGlow = args.StageGlow;
        ToHouseDarkness = Darkness(args.HouseLevel);
        FadeStartMs = nowMs;
        FadeMs = HasSetup ? args.FadeMs : 0;
        HasSetup = true;
        RebuildLightList();
    }

    public void Clear()
    {
        Targets.Clear();
        Held.Clear();
        Releasing.Clear();
        FadeFrom.Clear();
        FadingOut.Clear();
        LightList.Clear();
        LastLookup = _ => null;
        HasSetup = false;
        Stage = Rectangle.Empty;
        HouseLevel = 100;
        StageGlow = true;
        FromHouseDarkness = 0f;
        ToHouseDarkness = 0f;
        FadeMs = 0;
        LatestFrames = [];
    }

    /// <summary>Darkness strength for the darkness layer: 0 with the house lights full, 1 with them off.</summary>
    public float CurrentHouseDarkness(long nowMs) => MathHelper.Lerp(FromHouseDarkness, ToHouseDarkness, FadeProgress(nowMs));

    /// <summary>Shows <paramref name="light" /> for its id instead of the server's copy. Used while the director drags.</summary>
    public void Hold(StageLightInfo light)
    {
        Held[light.Id] = light;
        Releasing.Remove(light.Id);
        RebuildLightList();
    }

    /// <summary>Ends a hold once the server's copy matches it, or after <see cref="RELEASE_TIMEOUT_MS" />.</summary>
    public void Release(byte id, long nowMs)
    {
        if (Held.ContainsKey(id))
            Releasing[id] = nowMs + RELEASE_TIMEOUT_MS;
    }

    /// <summary>
    ///     Fills <paramref name="frames" /> with every light as it looks at <paramref name="nowMs" />, including lights
    ///     fading out. <paramref name="followLookup" /> turns an entity id into its fractional tile, or null when it isn't
    ///     in view.
    /// </summary>
    public void Evaluate(long nowMs, Func<uint, Vector2?> followLookup, List<StageLightFrame> frames)
    {
        frames.Clear();
        LastLookup = followLookup;

        if (!HasSetup)
        {
            LatestFrames = frames;

            return;
        }

        if (Releasing.Count > 0)
            foreach (var (id, deadline) in Releasing.ToList())
                if (nowMs >= deadline)
                    EndHold(id);

        frames.AddRange(ComputeActiveFrames(nowMs));

        //each fading-out light decays on its own clock and drops off once it's done
        for (var i = FadingOut.Count - 1; i >= 0; i--)
        {
            var entry = FadingOut[i];

            if (entry.IsDone(nowMs))
            {
                FadingOut.RemoveAt(i);

                continue;
            }

            frames.Add(entry.CurrentFrame(nowMs));
        }

        LatestFrames = frames;
    }

    /// <summary>Holds a fractional tile to the stage's tile centres. Unchanged before any setup arrives.</summary>
    public Vector2 ClampToStage(Vector2 tile)
        => Stage.IsEmpty
            ? tile
            : new Vector2(Math.Clamp(tile.X, Stage.Left, Stage.Right - 1), Math.Clamp(tile.Y, Stage.Top, Stage.Bottom - 1));

    public static Vector2 ToTile(ushort x, ushort y)
        => new(x / (float)StageLightInfo.UNITS_PER_TILE, y / (float)StageLightInfo.UNITS_PER_TILE);

    /// <summary>
    ///     An entity's fractional tile, including its walking offset (world pixels). Inverts the isometric formula:
    ///     offset.X = (Δx − Δy)·28 and offset.Y = (Δx + Δy)·14.
    /// </summary>
    public static Vector2 EntityTile(int tileX, int tileY, Vector2 visualOffset)
    {
        var a = visualOffset.X / 28f;
        var b = visualOffset.Y / 14f;

        return new Vector2(tileX + ((a + b) / 2f), tileY + ((b - a) / 2f));
    }

    /// <summary>The follow lookup the game uses: an entity in view, as a fractional tile.</summary>
    public static Vector2? EntityTileOf(uint entityId)
        => WorldState.GetEntity(entityId) is { } entity ? EntityTile(entity.TileX, entity.TileY, entity.VisualOffset) : null;

    public static Vector2 SweepPosition(Vector2 a, Vector2 b, long elapsedMs, byte speed)
    {
        var period = SweepMs[SpeedIndex(speed)];
        var phase = Phase(elapsedMs, period);
        var there = phase < 0.5f ? phase * 2f : 2f - (phase * 2f);
        var eased = there * there * (3f - (2f * there));

        return Vector2.Lerp(a, b, eased);
    }

    public static Vector2 CirclePosition(Vector2 centre, Vector2 edge, long elapsedMs, byte speed)
    {
        var radius = Vector2.Distance(centre, edge);
        var angle = MathHelper.TwoPi * Phase(elapsedMs, SweepMs[SpeedIndex(speed)]);

        return centre + (new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius);
    }

    /// <summary>0.30 at the start of the cycle, 1.0 at its middle.</summary>
    public static float PulseFactor(long elapsedMs, byte speed)
    {
        var wave = 0.5f - (0.5f * MathF.Cos(MathHelper.TwoPi * Phase(elapsedMs, PulseMs[SpeedIndex(speed)])));

        return 0.30f + (0.70f * wave);
    }

    /// <summary>A new random level 0.55-1.0 on every step; the same inputs give the same level on every client.</summary>
    public static float FlickerFactor(byte id, long elapsedMs, byte speed)
    {
        var step = (long)(elapsedMs * FlickerPerSecond[SpeedIndex(speed)] / 1000f);

        return 0.55f + (0.45f * Hash01(id, step));
    }

    public static Color CycleColor(Color baseColor, long elapsedMs, byte speed)
    {
        var (hue, _, _) = HsvColor.ToHsv(baseColor);

        return HsvColor.FromHsv(hue + (360f * Phase(elapsedMs, CycleMs[SpeedIndex(speed)])), 1f, 1f);
    }

    //integer hash → [0, 1)
    private static float Hash01(byte id, long step)
    {
        unchecked
        {
            var h = (uint)(step * 374761393L) ^ (id * 668265263u);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;

            return (h & 0xFFFFFF) / (float)0x1000000;
        }
    }

    /// <summary>
    ///     Every active target's frame at <paramref name="nowMs" />, already blended from <see cref="FadeFrom" /> by the
    ///     current fade progress. The single computation shared by <see cref="Evaluate" /> (what to draw) and
    ///     <see cref="Apply" /> (what to fade the next setup in from).
    /// </summary>
    private List<StageLightFrame> ComputeActiveFrames(long nowMs)
    {
        var result = new List<StageLightFrame>(Targets.Count);
        var k = FadeProgress(nowMs);

        foreach (var target in Targets.Values)
        {
            var light = Held.GetValueOrDefault(target.Light.Id) ?? target.Light;
            var to = Compute(light, target, nowMs, LastLookup);
            var frame = k < 1f ? Blend(FadeFrom.TryGetValue(light.Id, out var from) ? from : to with { Strength = 0f }, to, k) : to;

            result.Add(frame);
        }

        return result;
    }

    private StageLightFrame Compute(StageLightInfo light, Target target, long nowMs, Func<uint, Vector2?> followLookup)
    {
        var a = ToTile(light.X, light.Y);
        var b = ToTile(light.X2, light.Y2);
        var motionMs = Math.Max(0, nowMs - target.MotionStartMs);
        var effectMs = Math.Max(0, nowMs - target.EffectStartMs);

        var tile = light.Motion switch
        {
            StageLightMotion.Sweep  => SweepPosition(a, b, motionMs, light.MotionSpeed),
            StageLightMotion.Circle => ClampToStage(CirclePosition(a, b, motionMs, light.MotionSpeed)),
            StageLightMotion.Follow => followLookup(light.FollowId) is { } seen ? ClampToStage(seen) : a,
            _                       => a
        };

        var color = new Color(light.R, light.G, light.B);
        var strength = light.Brightness / 100f;

        switch (light.Effect)
        {
            case StageLightEffect.Pulse:
                strength *= PulseFactor(effectMs, light.EffectSpeed);

                break;
            case StageLightEffect.Flicker:
                strength *= FlickerFactor(light.Id, effectMs, light.EffectSpeed);

                break;
            case StageLightEffect.ColorCycle:
                color = CycleColor(color, effectMs, light.EffectSpeed);

                break;
        }

        return new StageLightFrame(light.Id, tile, color, strength, light.Size, light.Beam);
    }

    private static StageLightFrame Blend(StageLightFrame from, StageLightFrame to, float k)
        => to with
        {
            Tile = Vector2.Lerp(from.Tile, to.Tile, k),
            Color = Color.Lerp(from.Color, to.Color, k),
            Strength = MathHelper.Lerp(from.Strength, to.Strength, k)
        };

    private void EndHold(byte id)
    {
        Releasing.Remove(id);

        if (Held.Remove(id))
            RebuildLightList();
    }

    private void RebuildLightList()
    {
        LightList.Clear();

        foreach (var target in Targets.Values.OrderBy(t => t.Light.Id))
            LightList.Add(Held.GetValueOrDefault(target.Light.Id) ?? target.Light);
    }

    private float FadeProgress(long nowMs) => FadeMs <= 0 ? 1f : Math.Clamp((nowMs - FadeStartMs) / (float)FadeMs, 0f, 1f);

    private static float Darkness(byte houseLevel) => (100 - Math.Min(houseLevel, (byte)100)) / 100f;

    private static float Phase(long elapsedMs, float periodMs) => elapsedMs % (long)periodMs / periodMs;

    private static int SpeedIndex(byte speed) => Math.Clamp((int)speed, 1, 5) - 1;

    private sealed record Target(StageLightInfo Light, long MotionStartMs, long EffectStartMs);

    /// <summary>
    ///     A light that has been removed from the setup, fading from <see cref="Frame" />'s strength to 0 over
    ///     <see cref="DurationMs" /> starting at <see cref="StartMs" />. Runs on its own clock so it is unaffected by
    ///     whatever fade a later, unrelated <see cref="Apply" /> carries.
    /// </summary>
    private sealed record FadingEntry(StageLightFrame Frame, long StartMs, int DurationMs)
    {
        public bool IsDone(long nowMs) => (DurationMs <= 0) || ((nowMs - StartMs) >= DurationMs);

        public StageLightFrame CurrentFrame(long nowMs)
        {
            var k = DurationMs <= 0 ? 1f : Math.Clamp((nowMs - StartMs) / (float)DurationMs, 0f, 1f);

            return Frame with { Strength = Frame.Strength * (1f - k) };
        }
    }
}
