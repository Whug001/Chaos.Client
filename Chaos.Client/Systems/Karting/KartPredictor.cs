#region
using Chaos.DarkAges.Karting;
#endregion

namespace Chaos.Client.Systems.Karting;

/// <summary>
///     Runs the local kart ahead of the server with the shared physics. Each snapshot rewinds to the server's state and
///     replays the inputs the server has not used yet; the visible jump is spread over a short blend.
/// </summary>
public sealed class KartPredictor(KartStats stats, TrackSurface surface)
{
    public const int HISTORY = 64;
    public const double BLEND_SECONDS = 0.1;
    public const double SNAP_TILES = 3;

    private readonly List<KartInput> Unacked = [];
    private double OffsetX;
    private double OffsetY;
    private KartBody Previous;

    public KartBody Body;
    public KartModifiers Mods;

    public int Pending => Unacked.Count;

    /// <summary>How far the drawn kart is from the step before <see cref="Body" /> to <see cref="Body" />, 0 to 1.</summary>
    /// <remarks>Physics steps 30 times a second; drawing every frame at the latest step makes the camera jump in steps
    /// while the other karts glide, so they shake on screen.</remarks>
    public double Alpha
    {
        get;
        set => field = Math.Clamp(value, 0, 1);
    } = 1;

    public (double X, double Y) DrawPosition
        => (Previous.X + (Body.X - Previous.X) * Alpha + OffsetX, Previous.Y + (Body.Y - Previous.Y) * Alpha + OffsetY);

    public void Reset(KartBody body, KartModifiers mods)
    {
        Body = body;
        Previous = body;
        Mods = mods;
        Unacked.Clear();
        OffsetX = 0;
        OffsetY = 0;
    }

    public void Step(KartInput input)
    {
        Unacked.Add(input);

        if (Unacked.Count > HISTORY)
            Unacked.RemoveAt(0);

        Previous = Body;
        KartPhysics.Step(ref Body, ref Mods, input, stats, surface);
    }

    public void Correct(ushort ackSequence, KartBody server, KartModifiers serverMods)
    {
        Unacked.RemoveAll(i => (short)(i.Sequence - ackSequence) <= 0);

        var (drawnX, drawnY) = DrawPosition;
        Body = server;
        Previous = server;
        Mods = serverMods;

        foreach (var input in Unacked)
        {
            Previous = Body;
            KartPhysics.Step(ref Body, ref Mods, input, stats, surface);
        }

        OffsetX = 0;
        OffsetY = 0;
        var (blendedX, blendedY) = DrawPosition;
        OffsetX = drawnX - blendedX;
        OffsetY = drawnY - blendedY;

        if (OffsetX * OffsetX + OffsetY * OffsetY > SNAP_TILES * SNAP_TILES)
        {
            OffsetX = 0;
            OffsetY = 0;
        }
    }

    public void Decay(double seconds)
    {
        var keep = Math.Max(0, 1 - seconds / BLEND_SECONDS);
        OffsetX *= keep;
        OffsetY *= keep;
    }
}
