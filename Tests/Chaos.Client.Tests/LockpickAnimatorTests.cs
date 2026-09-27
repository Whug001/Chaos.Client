using Chaos.Client.Controls.World.Popups.Lockpicking;
using Chaos.DarkAges.Definitions;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class LockpickAnimatorTests
{
    private static void Run(LockpickAnimator animator, float seconds)
    {
        for (var t = 0f; t < seconds; t += 0.01f)
            animator.Update(0.01f);
    }

    [Test]
    public async Task A_jam_strains_returns_and_goes_idle()
    {
        var a = new LockpickAnimator();

        a.PressTurn(false).Should().BeTrue();
        a.State.Should().Be(LockpickAnimState.Turning);

        a.ApplyResult(LockpickTurnOutcome.Jammed, 40);
        Run(a, 0.2f);
        a.State.Should().Be(LockpickAnimState.Straining);
        a.CylinderDegrees.Should().BeApproximately(36f, 0.5f);

        Run(a, LockpickAnimator.STRAIN_SECONDS);
        a.State.Should().Be(LockpickAnimState.Returning);

        Run(a, LockpickAnimator.RETURN_SECONDS + 0.05f);
        a.State.Should().Be(LockpickAnimState.Idle);
        a.CylinderDegrees.Should().Be(0f);

        await Task.CompletedTask;
    }

    [Test]
    public async Task An_open_turns_fully_then_finishes()
    {
        var a = new LockpickAnimator();
        a.PressTurn(false);
        a.ApplyResult(LockpickTurnOutcome.Opened, 100);

        Run(a, 0.3f);
        a.State.Should().Be(LockpickAnimState.Done);
        a.CylinderDegrees.Should().Be(LockpickAnimator.FULL_TURN_DEGREES);

        Run(a, LockpickAnimator.CLOSE_DELAY_SECONDS + 0.05f);
        a.State.Should().Be(LockpickAnimState.Finished);

        await Task.CompletedTask;
    }

    [Test]
    public async Task A_break_strains_then_snaps_then_finishes()
    {
        var a = new LockpickAnimator();
        a.PressTurn(false);
        a.ApplyResult(LockpickTurnOutcome.Broke, 10);

        Run(a, 0.1f);
        a.State.Should().Be(LockpickAnimState.Straining);

        Run(a, LockpickAnimator.STRAIN_SECONDS);
        a.State.Should().Be(LockpickAnimState.Snapped);

        Run(a, LockpickAnimator.CLOSE_DELAY_SECONDS + 0.05f);
        a.State.Should().Be(LockpickAnimState.Finished);

        await Task.CompletedTask;
    }

    [Test]
    public async Task It_waits_at_20_degrees_for_the_reply_then_times_out()
    {
        var a = new LockpickAnimator();
        a.PressTurn(false);

        Run(a, 1f);
        a.State.Should().Be(LockpickAnimState.Turning);
        a.CylinderDegrees.Should().Be(LockpickAnimator.WAIT_AT_DEGREES);

        Run(a, LockpickAnimator.REPLY_TIMEOUT_SECONDS);
        a.TimedOut.Should().BeTrue();

        Run(a, LockpickAnimator.RETURN_SECONDS + 0.05f);
        a.State.Should().Be(LockpickAnimState.Idle);

        await Task.CompletedTask;
    }

    [Test]
    public async Task A_held_key_starts_only_one_turn()
    {
        var a = new LockpickAnimator();
        a.PressTurn(false).Should().BeTrue();
        a.ApplyResult(LockpickTurnOutcome.Jammed, 40);
        Run(a, 1f);
        a.State.Should().Be(LockpickAnimState.Idle);

        a.PressTurn(true).Should().BeFalse();
        a.PressTurn(false).Should().BeFalse();

        a.ReleaseTurn();
        a.PressTurn(false).Should().BeTrue();

        await Task.CompletedTask;
    }

    [Test]
    public async Task The_pick_only_moves_while_idle_and_stays_in_range()
    {
        var a = new LockpickAnimator();
        a.SetPick(250f);
        a.PickDegrees.Should().Be(180f);

        a.NudgePick(-LockpickAnimator.PICK_STEP_DEGREES);
        a.PickDegrees.Should().Be(178f);

        a.PressTurn(false);
        a.SetPick(10f);
        a.PickDegrees.Should().Be(178f);

        await Task.CompletedTask;
    }

    [Test]
    public async Task A_result_outside_a_turn_is_ignored()
    {
        var a = new LockpickAnimator();
        a.ApplyResult(LockpickTurnOutcome.Opened, 100);
        a.State.Should().Be(LockpickAnimState.Idle);
        await Task.CompletedTask;
    }
}
