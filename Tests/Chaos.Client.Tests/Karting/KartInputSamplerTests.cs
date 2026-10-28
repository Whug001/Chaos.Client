#region
using Chaos.Client.Systems.Karting;
using Chaos.Client.Definitions;
using Chaos.Client.Systems.KeyBinds;
using Chaos.DarkAges.Karting;
using FluentAssertions;
#endregion

namespace Chaos.Client.Tests.Karting;

public class KartInputSamplerTests
{
    [Test]
    public void Each_action_maps_to_its_flag()
    {
        var sampler = new KartInputSampler();
        var pairs = new[]
        {
            (GameAction.KartAccelerate, KartInputFlags.Accelerate),
            (GameAction.KartBrake, KartInputFlags.Brake),
            (GameAction.KartSteerLeft, KartInputFlags.Left),
            (GameAction.KartSteerRight, KartInputFlags.Right),
            (GameAction.KartDrift, KartInputFlags.Drift),
            (GameAction.KartUseItem, KartInputFlags.Item)
        };

        foreach (var (action, expected) in pairs)
        {
            var input = sampler.Sample(0, held => held == action, false);
            input.Flags.Should().Be(expected);
        }
    }

    [Test]
    public void All_actions_and_typing_are_sampled_correctly()
    {
        var sampler = new KartInputSampler();
        sampler.Sample(1, _ => true, false).Flags.Should().Be((KartInputFlags)0x3F);
        sampler.Sample(2, _ => true, true).Flags.Should().Be(KartInputFlags.None);
    }

    [Test]
    public void Sequence_wraps_and_left_shift_activates_drift()
    {
        var sampler = new KartInputSampler();
        sampler.Reset();
        var table = new KeyBindingTable();
        var drift = sampler.Sample(0, table, (key, modifiers) => key == Scancode.LeftShift && modifiers == KeyModifiers.Shift, false);
        drift.Flags.Should().Be(KartInputFlags.Drift);
        sampler.Reset();
        sampler.Sample(0, _ => false, false).Sequence.Should().Be(0);
        for (var i = 1; i < ushort.MaxValue; i++)
            sampler.Sample(0, _ => false, false);
        sampler.Sample(0, _ => false, false).Sequence.Should().Be(ushort.MaxValue);
        sampler.Sample(0, _ => false, false).Sequence.Should().Be(0);
    }

    [Test]
    public void Shift_drift_does_not_suppress_default_accelerate_binding()
    {
        var sampler = new KartInputSampler();
        var table = new KeyBindingTable();
        var heldKeys = new HashSet<Scancode> { Scancode.LeftShift, Scancode.Up };
        var input = sampler.Sample(
            0,
            table,
            (key, modifiers) => KartInputSampler.IsChordHeld(key, modifiers, heldKeys.Contains, KeyModifiers.Shift),
            false);

        input.Flags.Should().HaveFlag(KartInputFlags.Drift);
        input.Flags.Should().HaveFlag(KartInputFlags.Accelerate);
    }
}
