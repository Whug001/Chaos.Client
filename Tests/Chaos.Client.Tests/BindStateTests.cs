using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class BindStateTests
{
    private static readonly TimeSpan Start = TimeSpan.FromSeconds(10);

    private static SetBindStateArgs Preview(string name, string grade, byte chance)
        => new()
        {
            Mode = BindMeterMode.Preview,
            Grade = grade,
            MonsterName = name,
            Chance = chance
        };

    private static SetBindStateArgs Throw(byte shakes, BindOutcome outcome = BindOutcome.Pending)
        => new()
        {
            Mode = BindMeterMode.Throw,
            Grade = "Clay",
            MonsterName = "Goblin",
            Chance = 48,
            Shakes = shakes,
            Outcome = outcome
        };

    private static SetBindStateArgs Hidden() => new() { Mode = BindMeterMode.Hidden };

    [Test]
    public void StartsHidden()
    {
        var bind = new BindState();

        bind.Visible.Should().BeFalse();
        bind.ShownMode.Should().Be(BindMeterMode.Hidden);
    }

    [Test]
    public void Preview_SetsLabelAndFill()
    {
        var bind = new BindState();

        bind.Apply(Preview("Goblin", "Clay", 48), Start);

        bind.Visible.Should().BeTrue();
        bind.ShownMode.Should().Be(BindMeterMode.Preview);
        bind.Label.Should().Be("Goblin — Clay 48%");
        bind.Fill.Should().BeApproximately(0.48f, 0.0001f);
        bind.SegmentsLit.Should().Be(0);
    }

    [Test]
    public void Preview_NoMonster_ShowsOrbReady()
    {
        var bind = new BindState();

        bind.Apply(Preview(string.Empty, "Silver", 0), Start);

        bind.Visible.Should().BeTrue();
        bind.Label.Should().Be("Silver orb ready");
        bind.Fill.Should().Be(0f);
    }

    [Test]
    public void Hidden_HidesAPreview()
    {
        var bind = new BindState();
        bind.Apply(Preview("Goblin", "Clay", 48), Start);

        bind.Apply(Hidden(), Start);

        bind.Visible.Should().BeFalse();
        bind.ShownMode.Should().Be(BindMeterMode.Hidden);
    }

    [Test]
    public void Throw_StepsSegments()
    {
        var bind = new BindState();

        for (byte shakes = 0; shakes <= 3; shakes++)
        {
            bind.Apply(Throw(shakes), Start + TimeSpan.FromMilliseconds(800 * shakes));

            bind.Visible.Should().BeTrue();
            bind.ShownMode.Should().Be(BindMeterMode.Throw);
            bind.ShownOutcome.Should().Be(BindOutcome.Pending);
            bind.Label.Should().Be("Goblin — Clay 48%");
            bind.SegmentsLit.Should().Be(shakes);
            bind.Fill.Should().BeApproximately(shakes / 3f, 0.0001f);
        }
    }

    [Test]
    public void Throw_Outcome_HoldsFor1500ms_ThenHides()
    {
        var bind = new BindState();
        bind.Apply(Throw(0), Start);
        bind.Apply(Throw(3, BindOutcome.Bound), Start);

        bind.Label.Should().Be("BOUND");
        bind.ShownOutcome.Should().Be(BindOutcome.Bound);
        bind.SegmentsLit.Should().Be(3);

        bind.Update(Start + TimeSpan.FromMilliseconds(1499));
        bind.Visible.Should().BeTrue();
        bind.Label.Should().Be("BOUND");

        bind.Update(Start + TimeSpan.FromMilliseconds(1500));
        bind.Visible.Should().BeFalse();
        bind.ShownMode.Should().Be(BindMeterMode.Hidden);
    }

    [Test]
    public void Throw_BrokeFree_ShowsBrokeFree()
    {
        var bind = new BindState();
        bind.Apply(Throw(1, BindOutcome.BrokeFree), Start);

        bind.Label.Should().Be("BROKE FREE");
        bind.ShownOutcome.Should().Be(BindOutcome.BrokeFree);
        bind.SegmentsLit.Should().Be(1);
    }

    [Test]
    public void PreviewDuringThrow_IsDeferred_UntilHoldEnds()
    {
        var bind = new BindState();
        bind.Apply(Throw(0), Start);

        bind.Apply(Preview("Kobold", "Clay", 20), Start + TimeSpan.FromMilliseconds(100));
        bind.Apply(Preview("Goblin", "Gold", 90), Start + TimeSpan.FromMilliseconds(200));

        bind.ShownMode.Should().Be(BindMeterMode.Throw);
        bind.Label.Should().Be("Goblin — Clay 48%");

        bind.Apply(Throw(2, BindOutcome.BrokeFree), Start + TimeSpan.FromMilliseconds(1600));
        bind.Update(Start + TimeSpan.FromMilliseconds(3000));
        bind.ShownMode.Should().Be(BindMeterMode.Throw);
        bind.Label.Should().Be("BROKE FREE");

        bind.Update(Start + TimeSpan.FromMilliseconds(3100));

        bind.Visible.Should().BeTrue();
        bind.ShownMode.Should().Be(BindMeterMode.Preview);
        bind.Label.Should().Be("Goblin — Gold 90%");
        bind.Fill.Should().BeApproximately(0.9f, 0.0001f);
    }

    [Test]
    public void HiddenDuringThrow_IsDeferred()
    {
        var bind = new BindState();
        bind.Apply(Preview("Goblin", "Clay", 48), Start);
        bind.Apply(Throw(0), Start);

        bind.Apply(Preview("Goblin", "Clay", 60), Start + TimeSpan.FromMilliseconds(100));
        bind.Apply(Hidden(), Start + TimeSpan.FromMilliseconds(200));

        bind.Visible.Should().BeTrue();
        bind.ShownMode.Should().Be(BindMeterMode.Throw);

        bind.Apply(Throw(3, BindOutcome.Bound), Start + TimeSpan.FromMilliseconds(2400));
        bind.Update(Start + TimeSpan.FromMilliseconds(3900));

        //the last stored packet was Hidden, so the preview stored before it is not brought back
        bind.Visible.Should().BeFalse();
        bind.ShownMode.Should().Be(BindMeterMode.Hidden);
    }

    [Test]
    public void ANewThrowDuringTheHold_TakesOver()
    {
        var bind = new BindState();
        bind.Apply(Throw(1, BindOutcome.BrokeFree), Start);

        bind.Apply(Throw(0), Start + TimeSpan.FromMilliseconds(1000));
        bind.Update(Start + TimeSpan.FromMilliseconds(2000));

        bind.Visible.Should().BeTrue();
        bind.ShownOutcome.Should().Be(BindOutcome.Pending);
        bind.Label.Should().Be("Goblin — Clay 48%");
        bind.SegmentsLit.Should().Be(0);
    }

    [Test]
    public void Reset_Hides()
    {
        var bind = new BindState();
        bind.Apply(Throw(2), Start);
        bind.Apply(Preview("Goblin", "Clay", 48), Start);

        bind.Reset();

        bind.Visible.Should().BeFalse();
        bind.ShownMode.Should().Be(BindMeterMode.Hidden);
        bind.Label.Should().BeEmpty();
        bind.Fill.Should().Be(0f);
        bind.SegmentsLit.Should().Be(0);

        //nothing stored from before the reset comes back later
        bind.Update(Start + TimeSpan.FromSeconds(10));
        bind.Visible.Should().BeFalse();
    }
}
