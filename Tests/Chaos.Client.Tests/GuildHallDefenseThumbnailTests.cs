using Chaos.Client.Controls.World.Popups.GuildHall;
using Chaos.DarkAges.Definitions;
using FluentAssertions;

namespace Chaos.Client.Tests;

public sealed class GuildHallDefenseThumbnailTests
{
    [Test]
    public async Task Monster_uses_creature_frame()
    {
        GuildHallThumbnails.UsesEffectFrame(GuildHallDefenseKind.Monster).Should().BeFalse();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Trap_and_blocker_use_effect_frame()
    {
        GuildHallThumbnails.UsesEffectFrame(GuildHallDefenseKind.Trap).Should().BeTrue();
        GuildHallThumbnails.UsesEffectFrame(GuildHallDefenseKind.Blocker).Should().BeTrue();

        await Task.CompletedTask;
    }
}
