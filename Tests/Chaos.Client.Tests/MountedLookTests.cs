using Chaos.Client.Rendering;
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using FluentAssertions;

namespace Chaos.Client.Tests;

public sealed class MountedLookTests
{
    private static readonly AislingAppearance Base = new()
    {
        Gender = Gender.Female,
        BodySpriteId = AislingRenderer.BODY_ID,
        HeadSprite = 12,
        OvercoatSprite = 0
    };

    [Test]
    public async Task Normal_mount_rides_head_only_with_mount_overcoat_and_cloak()
    {
        var figure = MountedLook.Build(Base, new MountWindowMount { Mount = 18, Name = "White Horse", Sprite = 984 }, DisplayColor.Black);

        figure.Look.Should().NotBeNull();
        figure.Look!.Value.BodySpriteId.Should().Be(AislingRenderer.HEAD_BODY_ID);
        figure.Look.Value.OvercoatSprite.Should().Be(984);
        figure.Look.Value.OvercoatColor.Should().Be(DisplayColor.Black);
        figure.Look.Value.HeadSprite.Should().Be(12);
        figure.CreatureSprite.Should().Be(0);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Sleigh_is_a_creature_and_ignores_the_cloak()
    {
        var sleigh = new MountWindowMount { Mount = 30, Name = "Sleigh", Sprite = 738, IsSleigh = true };

        var plain = MountedLook.Build(Base, sleigh, DisplayColor.Default);
        var dyed = MountedLook.Build(Base, sleigh, DisplayColor.Black);

        plain.Look.Should().BeNull();
        plain.CreatureSprite.Should().Be(738);
        dyed.Should().Be(plain);

        await Task.CompletedTask;
    }
}
