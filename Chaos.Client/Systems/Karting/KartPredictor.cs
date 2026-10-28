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

    public KartBody Body;
    public KartModifiers Mods;

    public int Pending => Unacked.Count;

    public (double X, double Y) DrawPosition => (Body.X + OffsetX, Body.Y + OffsetY);

    public void Reset(KartBody body, KartModifiers mods)
    {
        Body = body;
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

        KartPhysics.Step(ref Body, ref Mods, input, stats, surface);
    }

    public void Correct(ushort ackSequence, KartBody server, KartModifiers serverMods)
    {
        Unacked.RemoveAll(i => (short)(i.Sequence - ackSequence) <= 0);

        var (drawnX, drawnY) = DrawPosition;
        Body = server;
        Mods = serverMods;

        foreach (var input in Unacked)
            KartPhysics.Step(ref Body, ref Mods, input, stats, surface);

        OffsetX = drawnX - Body.X;
        OffsetY = drawnY - Body.Y;

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
