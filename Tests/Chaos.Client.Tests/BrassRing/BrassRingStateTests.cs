using Chaos.Client.Controls.World.Popups.BrassRing;
using Chaos.DarkAges.BrassRing;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using FluentAssertions;
using static Chaos.DarkAges.BrassRing.BrassRingRules;

namespace Chaos.Client.Tests.BrassRing;

public sealed class BrassRingStateTests
{
    private readonly BrassRingState State = new();

    private static BrassRingDisplayArgs Tick(BrassRingPose you = BrassRingPose.Idle, int stamina = MAX_STAMINA, int phaseMs = 30000, BrassRingPhase phase = BrassRingPhase.Round)
        => new()
        {
            Type = BrassRingDisplayType.Tick,
            Phase = phase,
            PhaseMs = (ushort)phaseMs,
            Round = 1,
            Fighters =
            [
                new BrassRingFighterState { Health = 100, Stamina = (byte)stamina, Pose = you, PoseMs = 300 },
                new BrassRingFighterState { Health = 80, Stamina = MAX_STAMINA, Pose = BrassRingPose.Idle }
            ]
        };

    private void InRound(int stamina = MAX_STAMINA)
    {
        State.Reset(0);
        State.ApplyTick(Tick(stamina: stamina));
    }

    [Test]
    public void A_tick_sets_what_is_drawn()
    {
        InRound();

        State.Phase.Should().Be(BrassRingPhase.Round);
        State.PhaseMs.Should().Be(30000);
        State.Fighters[1].Health.Should().Be(80);
    }

    [Test]
    public void Timers_run_down_between_ticks_and_stop_at_zero()
    {
        State.Reset(0);
        State.ApplyTick(Tick(BrassRingPose.WindUpLeft, phaseMs: 100));

        State.Advance(250);

        State.PhaseMs.Should().Be(0);
        State.Fighters[0].PoseMs.Should().Be(50);
    }

    [Test]
    public void A_pressed_punch_shows_at_once()
    {
        InRound();

        State.Preview(BrassRingMove.PunchRight);

        State.Drawn(0).Pose.Should().Be(BrassRingPose.WindUpRight);
        State.Drawn(0).PoseMs.Should().Be(WIND_UP_MS);
    }

    [Test]
    public void An_unanswered_preview_is_cancelled()
    {
        InRound();
        State.Preview(BrassRingMove.DodgeLeft);

        State.Advance(399);
        State.Drawn(0).Pose.Should().Be(BrassRingPose.DodgeLeft);

        State.Advance(1);
        State.Drawn(0).Pose.Should().Be(BrassRingPose.Idle);
    }

    [Test]
    public void The_server_pose_replaces_the_preview_when_it_changes()
    {
        InRound();
        State.Preview(BrassRingMove.PunchLeft);

        State.ApplyTick(Tick(BrassRingPose.Stunned));

        State.Drawn(0).Pose.Should().Be(BrassRingPose.Stunned);
    }

    [Test]
    public void A_tick_with_the_old_pose_keeps_the_preview()
    {
        InRound();
        State.Preview(BrassRingMove.PunchLeft);

        State.ApplyTick(Tick());

        State.Drawn(0).Pose.Should().Be(BrassRingPose.WindUpLeft);
    }

    [Test]
    public void Releasing_block_shows_idle_at_once()
    {
        State.Reset(0);
        State.ApplyTick(Tick(BrassRingPose.Block));

        State.Preview(BrassRingMove.BlockEnd);

        State.Drawn(0).Pose.Should().Be(BrassRingPose.Idle);
    }

    [Test]
    public void No_preview_without_the_stamina()
    {
        InRound(PUNCH_STAMINA - 1);

        State.Preview(BrassRingMove.PunchLeft);

        State.Drawn(0).Pose.Should().Be(BrassRingPose.Idle);
    }

    [Test]
    public void No_preview_outside_a_round()
    {
        State.Reset(0);
        State.ApplyTick(Tick(phase: BrassRingPhase.Rest));

        State.Preview(BrassRingMove.PunchLeft);

        State.Drawn(0).Pose.Should().Be(BrassRingPose.Idle);
    }

    [Test]
    public void The_other_fighter_never_shows_a_preview()
    {
        State.Reset(1);
        State.ApplyTick(Tick());

        State.Preview(BrassRingMove.PunchLeft);

        State.Drawn(0).Pose.Should().Be(BrassRingPose.Idle);
        State.Drawn(1).Pose.Should().Be(BrassRingPose.WindUpLeft);
    }

    [Test]
    public void Forfeit_needs_a_second_escape_in_time()
    {
        State.Reset(0);

        State.PressEscape().Should().BeFalse();
        State.ForfeitArmed.Should().BeTrue();
        State.Advance(3000);
        State.ForfeitArmed.Should().BeFalse();
        State.PressEscape().Should().BeFalse();
        State.PressEscape().Should().BeTrue();
    }

    [Test]
    public void Disarming_the_forfeit_asks_again()
    {
        State.Reset(0);
        State.PressEscape().Should().BeFalse();

        State.DisarmForfeit();

        State.ForfeitArmed.Should().BeFalse();
        State.PressEscape().Should().BeFalse();
    }

    [Test]
    public void A_wound_up_pose_draws_idle_outside_a_round()
    {
        State.Reset(0);
        State.ApplyTick(Tick(BrassRingPose.WindUpLeft, phase: BrassRingPhase.Rest));

        State.Drawn(0).Pose.Should().Be(BrassRingPose.Idle);
    }

    [Test]
    public void A_down_fighter_stays_down_outside_a_round()
    {
        State.Reset(0);
        State.ApplyTick(Tick(BrassRingPose.Down, phase: BrassRingPhase.Finished));

        State.Drawn(0).Pose.Should().Be(BrassRingPose.Down);
    }

    [Test]
    public void A_wound_up_pose_still_draws_during_a_round()
    {
        InRound();

        State.ApplyTick(Tick(BrassRingPose.WindUpLeft));

        State.Drawn(0).Pose.Should().Be(BrassRingPose.WindUpLeft);
    }
}