using Chaos.Client.Controls.World.Popups.TurkeyShoot;
using FluentAssertions;

namespace Chaos.Client.Tests.TurkeyShoot;

public sealed class BiscuitAnimatorTests
{
    private static BiscuitAnimator Biscuit(int roll = 0) => new(_ => roll);

    [Test]
    public async Task Starts_napping_and_breathes()
    {
        var biscuit = Biscuit();

        biscuit.Act.Should().Be(BiscuitAct.Nap);
        biscuit.Frame.Should().Be(0);
        biscuit.Tick(600);
        biscuit.Frame.Should().Be(1);
        biscuit.Tick(600);
        biscuit.Frame.Should().Be(0);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Takeoff_wakes_him_and_he_stays_alert()
    {
        var biscuit = Biscuit();

        biscuit.Cue(TurkeyShootCue.FlockTakeoff);
        biscuit.Act.Should().Be(BiscuitAct.Wake);
        biscuit.Frame.Should().Be(2);

        biscuit.Tick(400);
        biscuit.Act.Should().Be(BiscuitAct.Alert);
        biscuit.Frame.Should().Be(3);

        biscuit.Tick(10_000);
        biscuit.Act.Should().Be(BiscuitAct.Alert);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Golden_takeoff_makes_him_spin()
    {
        var biscuit = Biscuit();

        biscuit.Cue(TurkeyShootCue.GoldenTakeoff);
        biscuit.Act.Should().Be(BiscuitAct.Spin);
        biscuit.Frame.Should().Be(16);
        biscuit.Tick(1_200);
        biscuit.Act.Should().Be(BiscuitAct.Alert);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Hit_makes_him_bound_then_shake_off_feathers()
    {
        var biscuit = Biscuit();

        biscuit.Cue(TurkeyShootCue.TurkeyHit);
        biscuit.Frame.Should().Be(4);
        biscuit.Tick(120);
        biscuit.Frame.Should().Be(5);
        biscuit.Tick(360);
        biscuit.Act.Should().Be(BiscuitAct.Feathers);
        biscuit.Frame.Should().Be(8);
        biscuit.Tick(1_500);
        biscuit.Act.Should().Be(BiscuitAct.Alert);

        await Task.CompletedTask;
    }

    //formatter:off
    [Test]
    [Arguments(0, BiscuitAct.ChaseTail)]
    [Arguments(1, BiscuitAct.Flop)]
    //formatter:on
    public async Task Escaped_birds_make_him_chase_his_tail_or_flop(int roll, BiscuitAct expected)
    {
        var biscuit = Biscuit(roll);

        biscuit.Cue(TurkeyShootCue.FlockEscaped);
        biscuit.Act.Should().Be(expected);
        biscuit.Tick(1_600);
        biscuit.Act.Should().Be(BiscuitAct.Nap);

        await Task.CompletedTask;
    }

    [Test]
    public async Task A_miss_changes_nothing()
    {
        var biscuit = Biscuit();
        biscuit.Cue(TurkeyShootCue.FlockTakeoff);

        biscuit.Cue(TurkeyShootCue.Miss);

        biscuit.Act.Should().Be(BiscuitAct.Wake);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Cleared_flock_lets_him_finish_then_nap()
    {
        var biscuit = Biscuit();
        biscuit.Cue(TurkeyShootCue.TurkeyHit);

        biscuit.Cue(TurkeyShootCue.FlockCleared);
        biscuit.Act.Should().Be(BiscuitAct.Bound);

        biscuit.Tick(480);
        biscuit.Tick(1_500);
        biscuit.Act.Should().Be(BiscuitAct.Nap);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Results_make_him_wag_for_good()
    {
        var biscuit = Biscuit();

        biscuit.Cue(TurkeyShootCue.Results);
        biscuit.Tick(100_000);

        biscuit.Act.Should().Be(BiscuitAct.Wag);
        biscuit.Frame.Should().BeOneOf(18, 19);

        await Task.CompletedTask;
    }

    [Test]
    public async Task No_cue_moves_him_once_he_wags()
    {
        var biscuit = Biscuit();

        biscuit.Cue(TurkeyShootCue.Results);
        biscuit.Act.Should().Be(BiscuitAct.Wag);

        biscuit.Cue(TurkeyShootCue.FlockTakeoff);
        biscuit.Cue(TurkeyShootCue.GoldenTakeoff);
        biscuit.Cue(TurkeyShootCue.TurkeyHit);
        biscuit.Cue(TurkeyShootCue.GoldenHit);
        biscuit.Cue(TurkeyShootCue.Miss);
        biscuit.Cue(TurkeyShootCue.FlockEscaped);
        biscuit.Cue(TurkeyShootCue.FlockCleared);
        biscuit.Cue(TurkeyShootCue.Results);
        biscuit.Tick(100_000);

        biscuit.Act.Should().Be(BiscuitAct.Wag);
        biscuit.Frame.Should().BeOneOf(18, 19);

        await Task.CompletedTask;
    }
}
