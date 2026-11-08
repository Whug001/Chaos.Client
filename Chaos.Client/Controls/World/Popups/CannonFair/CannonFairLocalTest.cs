using Chaos.CannonFair;
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;

namespace Chaos.Client.Controls.World.Popups.CannonFair;

public sealed class CannonFairLocalTest(CannonFairSession session)
{
    private const int BOTS = 7;
    private static readonly FortState Fort = FortState.From(FortLayouts.FeelTest);
    private readonly Random Rng = new();
    private ushort VolleyId;
    private VolleyResult? Finished;

    public void Start()
    {
        session.Apply(new CannonFairDisplayArgs { Kind = CannonFairMessageKind.Open, OwnSlot = 0 }, Environment.TickCount64);
        BeginAiming();
    }

    public void Stop()
    {
        Finished = null;
        session.Clear();
    }

    public void OnLockIn(float angle, float power) => _ = Fire(angle, power);

    public void Update()
    {
        var now = Environment.TickCount64;

        if (Finished is not null)
        {
            var result = Finished;
            Finished = null;
            VolleyId++;
            session.Apply(new CannonFairDisplayArgs { Kind = CannonFairMessageKind.RecordingPiece, VolleyId = VolleyId, PieceIndex = 0, PieceCount = 1, Data = RecordingCodec.Encode(result) }, now);
            session.Apply(new CannonFairDisplayArgs { Kind = CannonFairMessageKind.ReplayStart, VolleyId = VolleyId }, now);
        } else if (session.IsReplaying && (session.Player is not null) && (session.ReplayMs(now) >= session.Player.DurationMs + CannonFairRules.REPLAY_HOLD_MS))
            BeginAiming();
    }

    private void BeginAiming()
        => session.Apply(
            new CannonFairDisplayArgs
            {
                Kind = CannonFairMessageKind.Aiming,
                Shot = (byte)Rng.Next(5),
                Cannon = (byte)(1 + Rng.Next(BOTS + 1)),
                CannonCount = BOTS + 1,
                AimMs = CannonFairRules.AIM_MS,
                Pieces = CannonFairPieces.From(Fort)
            },
            Environment.TickCount64);

    private async Task Fire(float angle, float power)
    {
        var shots = new List<AimedShot> { new(0, session.Cannon, angle, power, session.Shot) };

        for (var bot = 1; bot <= BOTS; bot++)
            shots.Add(new AimedShot(bot, bot == session.Cannon ? BOTS + 1 : bot, 30f + (35f * Rng.NextSingle()), 0.6f + (0.35f * Rng.NextSingle()), (ShotKind)Rng.Next(5)));

        var result = await Task.Run(() => VolleySimulator.Run(Fort, shots, TimeSpan.FromSeconds(1), CannonFairRules.STEPS_PER_RECORD));
        Finished = result;
    }
}
