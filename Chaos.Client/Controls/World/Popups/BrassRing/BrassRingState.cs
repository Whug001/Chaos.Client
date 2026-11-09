using Chaos.DarkAges.BrassRing;
using Chaos.Networking.Entities.Server;
using static Chaos.DarkAges.BrassRing.BrassRingRules;

namespace Chaos.Client.Controls.World.Popups.BrassRing;

/// <summary>What the fight window draws, built from the server's messages and the fighter's own key presses.</summary>
public sealed class BrassRingState
{
    public const int PREVIEW_HOLD_MS = 400;
    public const int FORFEIT_CONFIRM_MS = 3000;
    public const int MAX_REACTIONS = 12;
    public const int REACTION_MS = 1000;

    public sealed class BrassRingReaction(BrassRingCheer kind, int x)
    {
        public BrassRingCheer Kind { get; } = kind;
        public int X { get; } = x;
        public int RemainingMs { get; set; } = REACTION_MS;
    }

    private readonly BrassRingFighter PreviewFighter = new();
    private int ForfeitMs;
    private int PreviewMs;
    private BrassRingPose PoseWhenPressed;

    public BrassRingFighter[] Fighters { get; } = [new(), new()];
    public BrassRingPhase Phase { get; private set; } = BrassRingPhase.Intro;
    public int PhaseMs { get; private set; } = INTRO_MS;
    public int Round { get; private set; } = 1;
    public int You { get; private set; }
    public bool ForfeitArmed => ForfeitMs > 0;
    public bool IsSpectator { get; private set; }
    public List<BrassRingReaction> Reactions { get; } = [];

    public void Reset(int you, bool spectator = false)
    {
        You = you;
        IsSpectator = spectator;
        Phase = BrassRingPhase.Intro;
        PhaseMs = INTRO_MS;
        Round = 1;
        Fighters[0] = new BrassRingFighter();
        Fighters[1] = new BrassRingFighter();
        PreviewMs = 0;
        ForfeitMs = 0;
        Reactions.Clear();
    }

    public void SwitchSide()
    {
        if (IsSpectator)
            You = 1 - You;
    }

    public void AddReaction(BrassRingCheer kind, int x)
    {
        if (Reactions.Count >= MAX_REACTIONS)
            Reactions.RemoveAt(0);

        Reactions.Add(new BrassRingReaction(kind, x));
    }

    public void ApplyTick(BrassRingDisplayArgs args)
    {
        Phase = args.Phase;
        PhaseMs = args.PhaseMs;
        Round = args.Round;

        for (var i = 0; i < Fighters.Length; i++)
        {
            var state = args.Fighters[i];
            var fighter = Fighters[i];
            fighter.Health = state.Health;
            fighter.StaminaMilli = state.Stamina * 1000;
            fighter.Pose = state.Pose;
            fighter.PoseMs = state.PoseMs;
            fighter.RoundWins = state.RoundWins;
        }

        foreach (var fightEvent in args.Events)
        {
            if ((fightEvent.Type == BrassRingEventType.PunchStarted) && (fightEvent.Fighter == You) && (PreviewMs > 0))
            {
                if (PreviewFighter.Pose is BrassRingPose.WindUpLeft or BrassRingPose.WindUpRight)
                {
                    PreviewFighter.PoseMs = fightEvent.Ms;
                    PoseWhenPressed = Fighters[You].Pose;
                }
            }
        }
        if ((PreviewMs > 0) && (Fighters[You].Pose != PoseWhenPressed))
            PreviewMs = 0;
    }

    public void Advance(int ms)
    {
        PhaseMs = Math.Max(0, PhaseMs - ms);
        ForfeitMs = Math.Max(0, ForfeitMs - ms);
        PreviewMs = Math.Max(0, PreviewMs - ms);
        PreviewFighter.PoseMs = Math.Max(0, PreviewFighter.PoseMs - ms);

        foreach (var reaction in Reactions)
            reaction.RemainingMs -= ms;

        Reactions.RemoveAll(reaction => reaction.RemainingMs <= 0);

        foreach (var fighter in Fighters)
            if (fighter.Pose is not (BrassRingPose.Idle or BrassRingPose.Down or BrassRingPose.Block))
                fighter.PoseMs = Math.Max(0, fighter.PoseMs - ms);
    }

    /// <summary>Shows the fighter's own move before the server answers, when the state says it could be legal.</summary>
    public void Preview(BrassRingMove move)
    {
        if (IsSpectator)
            return;

        if (Phase != BrassRingPhase.Round)
            return;

        var you = Fighters[You];
        var pose = PreviewPose(move, you);

        if (pose is null)
            return;

        PoseWhenPressed = you.Pose;
        PreviewMs = PREVIEW_HOLD_MS;
        PreviewFighter.Pose = pose.Value.Pose;
        PreviewFighter.PoseMs = pose.Value.Ms;
    }

    private readonly BrassRingFighter IdleFighter = new();

    public BrassRingFighter Drawn(int index)
    {
        var fighter = ((index == You) && (PreviewMs > 0)) ? PreviewFighter : Fighters[index];

        return ((Phase != BrassRingPhase.Round) && (fighter.Pose != BrassRingPose.Down)) ? IdleFighter : fighter;
    }

    /// <summary>Cancels an armed forfeit, so the next Escape asks again.</summary>
    public void DisarmForfeit() => ForfeitMs = 0;

    /// <summary>True on the press that confirms a forfeit.</summary>
    public bool PressEscape()
    {
        if (ForfeitArmed)
        {
            ForfeitMs = 0;

            return true;
        }

        ForfeitMs = FORFEIT_CONFIRM_MS;

        return false;
    }

    private static (BrassRingPose Pose, int Ms)? PreviewPose(BrassRingMove move, BrassRingFighter you)
    {
        if (move == BrassRingMove.BlockEnd)
            return you.Pose == BrassRingPose.Block ? (BrassRingPose.Idle, 0) : null;

        if (you.Pose != BrassRingPose.Idle)
            return null;

        return move switch
        {
            BrassRingMove.PunchLeft when you.Stamina >= PUNCH_STAMINA => (BrassRingPose.WindUpLeft, WIND_UP_MS),
            BrassRingMove.PunchRight when you.Stamina >= PUNCH_STAMINA => (BrassRingPose.WindUpRight, WIND_UP_MS),
            BrassRingMove.DodgeLeft when you.Stamina >= DODGE_STAMINA => (BrassRingPose.DodgeLeft, DODGE_ACTIVE_MS),
            BrassRingMove.DodgeRight when you.Stamina >= DODGE_STAMINA => (BrassRingPose.DodgeRight, DODGE_ACTIVE_MS),
            BrassRingMove.BlockStart when you.Stamina > 0 => (BrassRingPose.Block, 0),
            _ => null
        };
    }
}
