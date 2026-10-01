#region
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Rendering;

/// <summary>One sprite in a <see cref="FlyByFlock" />. Its position is relative to the viewport's top-left.</summary>
public sealed class Flyer
{
    internal Flyer() { }

    public Vector2 Position { get; internal set; }

    /// <summary>+1 while flying right, -1 while flying left.</summary>
    public int Direction { get; internal init; }

    internal float VelocityX { get; init; }
    internal float Drift { get; init; }
    internal float WaveAmplitude { get; init; }
    internal float WaveFreq { get; init; }
    internal float WavePhase { get; init; }
    internal float FlapRate { get; init; }
    internal float FlapPhase { get; init; }
    internal float Age { get; set; }
    internal float DartTimer { get; set; }
    internal Vector2 Dart { get; set; }
}

/// <summary>
///     The flight logic behind <see cref="FlyByRenderer" />, with no graphics: while spawning, a group of flyers enters
///     from the left or right edge after a random delay, crosses with a bob and random darts, and is removed past the
///     far edge. Camera movement shifts every flyer, so they fly over the MAP. Touched only on the game-loop thread.
/// </summary>
public sealed class FlyByFlock
{
    private readonly FlyByStyle Style;
    private readonly Random Rng;
    private readonly List<Flyer> FlyerList = [];
    private float? GroupTimer; // seconds to the next group; null while not spawning

    public FlyByFlock(FlyByStyle style, Random rng)
    {
        Style = style;
        Rng = rng;
    }

    public IReadOnlyList<Flyer> Flyers => FlyerList;

    /// <summary>How many groups have spawned since this flock was made. Tests use it to time the gaps.</summary>
    public int GroupsSpawned { get; private set; }

    /// <summary>Removes every flyer and restarts the first-group delay. Used on map change.</summary>
    public void Reset()
    {
        FlyerList.Clear();
        GroupTimer = null;
    }

    /// <summary>The index into <see cref="FlyByStyle.Frames" /> that <paramref name="flyer" /> shows now.</summary>
    public int FrameOf(Flyer flyer)
    {
        var cycle = Style.FrameCycle;
        var step = (int)(((flyer.Age * flyer.FlapRate) + flyer.FlapPhase) * cycle.Length);

        return cycle[step % cycle.Length];
    }

    /// <summary>
    ///     Advances every flyer by <paramref name="dt" /> seconds. While <paramref name="spawning" /> is true, counts
    ///     down to the next group. Turning spawning off forgets the countdown, so switching back on waits the first-group
    ///     delay again. A camera shift bigger than the viewport is a teleport and is ignored.
    /// </summary>
    public void Update(float dt, Vector2 viewportSize, Vector2 cameraShift, bool spawning)
    {
        if (dt <= 0f)
            return;

        if ((MathF.Abs(cameraShift.X) > viewportSize.X) || (MathF.Abs(cameraShift.Y) > viewportSize.Y))
            cameraShift = Vector2.Zero;

        if (spawning)
        {
            GroupTimer = (GroupTimer ?? Roll(Style.FirstGroupMin, Style.FirstGroupMax)) - dt;

            if (GroupTimer <= 0f)
            {
                SpawnGroup(viewportSize);
                GroupTimer = Roll(Style.GapMin, Style.GapMax);
            }
        }
        else
            GroupTimer = null;

        for (var i = FlyerList.Count - 1; i >= 0; i--)
        {
            var flyer = FlyerList[i];
            Move(flyer, dt, cameraShift);

            if (HasLeft(flyer, viewportSize.X))
                FlyerList.RemoveAt(i);
        }
    }

    private void SpawnGroup(Vector2 viewportSize)
    {
        var direction = Rng.Next(2) == 0 ? 1 : -1;
        var entryX = direction > 0 ? -Style.EntryMargin : viewportSize.X + Style.EntryMargin;
        var lineY = Roll(Style.BandTop, MathF.Max(Style.BandTop, viewportSize.Y * Style.BandBottom));
        var speed = Roll(Style.SpeedMin, Style.SpeedMax);
        var drift = Roll(-Style.DriftMax, Style.DriftMax);
        var count = Rng.Next(Style.GroupMin, Style.GroupMax + 1);

        for (var i = 0; i < count; i++)
            FlyerList.Add(
                new Flyer
                {
                    Direction = direction,
                    Position = new Vector2(
                        entryX - (direction * Roll(0f, Style.SpreadAlong)),
                        lineY + Roll(-Style.SpreadAcross, Style.SpreadAcross)),
                    VelocityX = direction * speed * Roll(1f - Style.SpeedJitter, 1f + Style.SpeedJitter),
                    Drift = drift,
                    WaveAmplitude = Style.WaveAmplitude * Roll(0.7f, 1.3f),
                    WaveFreq = Style.WaveFreq * Roll(0.8f, 1.2f),
                    WavePhase = Roll(0f, MathF.Tau),
                    FlapRate = Roll(Style.FlapMin, Style.FlapMax),
                    FlapPhase = Roll(0f, 1f)
                });

        GroupsSpawned++;
    }

    //velocity plus the group's drift, the bob (the derivative of a sine, so the height swings by WaveAmplitude) and the
    //current dart, plus the camera shift
    private void Move(Flyer flyer, float dt, Vector2 cameraShift)
    {
        flyer.Age += dt;

        if (Style.DartIntervalMax > 0f)
        {
            flyer.DartTimer -= dt;

            if (flyer.DartTimer <= 0f)
            {
                flyer.DartTimer = Roll(Style.DartIntervalMin, Style.DartIntervalMax);
                flyer.Dart = new Vector2(Roll(-Style.DartSpeed.X, Style.DartSpeed.X), Roll(-Style.DartSpeed.Y, Style.DartSpeed.Y));
            }
        }

        var angle = (MathF.Tau * flyer.WaveFreq * flyer.Age) + flyer.WavePhase;
        var bob = flyer.WaveAmplitude * MathF.Tau * flyer.WaveFreq * MathF.Cos(angle);
        var velocity = new Vector2(flyer.VelocityX + flyer.Dart.X, flyer.Drift + flyer.Dart.Y + bob);

        flyer.Position += cameraShift + (velocity * dt);
    }

    private bool HasLeft(Flyer flyer, float viewportWidth)
        => flyer.Direction > 0 ? flyer.Position.X > viewportWidth + Style.ExitMargin : flyer.Position.X < -Style.ExitMargin;

    private float Roll(float min, float max) => min + ((float)Rng.NextDouble() * (max - min));
}
