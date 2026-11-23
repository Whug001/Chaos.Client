using Chaos.Client.Definitions;
using Chaos.Client.ViewModel;
using Chaos.Networking.Entities.Server;
using FluentAssertions;

namespace Chaos.Client.Tests;

public sealed class CraneRevealRulesTests
{
    private static CraneRevealState FaceDown()
    {
        var state = new CraneRevealState();
        state.Start(new CranePrizeRevealArgs { PrizeName = "Capsule" });
        state.Advance(CraneRevealState.OPEN_MS + CraneRevealState.ARRIVE_MS);

        return state;
    }

    [Test]
    [Arguments(Scancode.Up)]
    [Arguments(Scancode.Down)]
    [Arguments(Scancode.Left)]
    [Arguments(Scancode.Right)]
    public void Arrows_AreSwallowed(Scancode key)
    {
        CraneRevealRules.Classify(key, false).Should().Be(CraneRevealKey.Swallow);
        CraneRevealRules.Classify(key, true).Should().Be(CraneRevealKey.Swallow);
    }

    [Test]
    public void Space_Advances_AndItsRepeatsAreSwallowed()
    {
        CraneRevealRules.Classify(Scancode.Space, false).Should().Be(CraneRevealKey.Advance);
        CraneRevealRules.Classify(Scancode.Space, true).Should().Be(CraneRevealKey.Swallow);
    }

    [Test]
    public void Escape_Closes_AndItsRepeatsAreSwallowed()
    {
        CraneRevealRules.Classify(Scancode.Escape, false).Should().Be(CraneRevealKey.Close);
        CraneRevealRules.Classify(Scancode.Escape, true).Should().Be(CraneRevealKey.Swallow);
    }

    [Test]
    public void OtherKeys_AreNotTheReveals()
        => CraneRevealRules.Classify(Scancode.A, false).Should().Be(CraneRevealKey.None);

    [Test]
    public void Advance_FlipsThenCloses()
    {
        var state = FaceDown();

        CraneRevealRules.Apply(state, CraneRevealKey.Advance).Should().BeTrue();
        state.Phase.Should().Be(CraneRevealPhase.Flipping);

        //mid-flip a click does nothing
        CraneRevealRules.Apply(state, CraneRevealKey.Advance).Should().BeFalse();
        state.Advance(CraneRevealState.FLIP_MS);
        state.Phase.Should().Be(CraneRevealPhase.FaceUp);

        CraneRevealRules.Apply(state, CraneRevealKey.Advance).Should().BeTrue();
        state.Phase.Should().Be(CraneRevealPhase.Idle);
    }

    [Test]
    public void Escape_DoesNothingUntilFaceUp()
    {
        var state = new CraneRevealState();
        state.Start(new CranePrizeRevealArgs());

        CraneRevealRules.Apply(state, CraneRevealKey.Close).Should().BeFalse();
        state.Phase.Should().Be(CraneRevealPhase.Opening);

        state.Advance(CraneRevealState.OPEN_MS + CraneRevealState.ARRIVE_MS);
        CraneRevealRules.Apply(state, CraneRevealKey.Close).Should().BeFalse();
        state.Phase.Should().Be(CraneRevealPhase.FaceDown);

        state.Flip();
        state.Advance(CraneRevealState.FLIP_MS);
        CraneRevealRules.Apply(state, CraneRevealKey.Close).Should().BeTrue();
        state.Phase.Should().Be(CraneRevealPhase.Idle);
    }

    [Test]
    public void SwallowAndNone_ChangeNothing()
    {
        var state = FaceDown();

        CraneRevealRules.Apply(state, CraneRevealKey.Swallow).Should().BeFalse();
        CraneRevealRules.Apply(state, CraneRevealKey.None).Should().BeFalse();
        state.Phase.Should().Be(CraneRevealPhase.FaceDown);
    }

    [Test]
    public void OutcomeText_NewPrize() => CraneRevealRules.OutcomeText(true, 0).Should().Be("NEW!");

    [Test]
    public void OutcomeText_OneTicket_IsSingular()
        => CraneRevealRules.OutcomeText(false, 1).Should().Be("Duplicate: +1 Crane Ticket");

    [Test]
    public void OutcomeText_ManyTickets_IsPlural()
        => CraneRevealRules.OutcomeText(false, 25).Should().Be("Duplicate: +25 Crane Tickets");

    [Test]
    public void OutcomeText_DuplicateWithNoTickets()
        => CraneRevealRules.OutcomeText(false, 0).Should().Be("Duplicate");

    [Test]
    public void Sweep_OnlyOnGold()
    {
        CraneRevealRules.SweepProgress(3, 0).Should().Be(0);
        CraneRevealRules.SweepProgress(3, CraneRevealRules.SWEEP_MS / 2).Should().BeApproximately(0.5, 1e-9);
        CraneRevealRules.SweepProgress(3, CraneRevealRules.SWEEP_MS + 1).Should().BeNull();
        CraneRevealRules.SweepProgress(3, CraneRevealRules.SWEEP_PERIOD_MS).Should().Be(0);

        for (byte tier = 0; tier < 3; tier++)
            CraneRevealRules.SweepProgress(tier, 0).Should().BeNull();
    }

    [Test]
    public void TierColors_AreDistinct()
    {
        var colors = Enumerable.Range(0, 4).Select(t => CraneRevealRules.TierColor((byte)t)).ToList();

        colors.Should().OnlyHaveUniqueItems();
    }
}
