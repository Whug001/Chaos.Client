using Chaos.Client.Definitions;
using Chaos.Client.Systems.KeyBinds;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class KeyChordTests
{
    [Test]
    public async Task Lone_modifier_chords_round_trip_through_settings()
    {
        KeyChord.TryParse("LeftShift", out var chord).Should().BeTrue();
        chord.Should().Be(new KeyChord(Scancode.LeftShift));
        chord.Modifiers.Should().Be(KeyModifiers.None);
        chord.Serialize().Should().Be("LeftShift");

        await Task.CompletedTask;
    }
}
