using Chaos.Client.Collections;
using Chaos.Client.Models;
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.Definitions;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using FluentAssertions;

namespace Chaos.Client.Tests;

/// <summary>
///     An over-hair helm (a crown or the Pilgrim Hat) is drawn on its own layer above the three accessories, so a player
///     wearing three accessories keeps all of them (Mimo's report, 2026-11-24).
/// </summary>
public class OverHairLayerTests
{
    [Test]
    public void TheLookKeepsTheHelmApartFromTheAccessories()
    {
        var entity = new WorldEntity { Id = 1 };

        WorldState.ApplyAislingLook(
            entity,
            new DisplayAislingArgs
            {
                Id = 1,
                BodySprite = BodySprite.Female,
                HeadSprite = 33,
                AccessorySprite3 = 800,
                OverHairSprite = 361,
                OverHairColor = DisplayColor.Lime,
                Name = "Mimo"
            });

        entity.Appearance!.Value.Accessory3Sprite.Should().Be(800);
        entity.Appearance!.Value.OverHairSprite.Should().Be(361);
        entity.Appearance!.Value.OverHairColor.Should().Be(DisplayColor.Lime);
    }

    [Test]
    public void TheHelmDrawsAboveTheThirdAccessoryFacingEitherWay()
    {
        foreach (var order in new[] { AislingRenderer.FRONT_ORDER, AislingRenderer.BACK_ORDER })
        {
            Array.IndexOf(order, LayerSlot.Acc4C).Should().BeGreaterThan(Array.IndexOf(order, LayerSlot.Acc3C));
            Array.IndexOf(order, LayerSlot.Acc4G).Should().BeGreaterThan(Array.IndexOf(order, LayerSlot.Acc3G));
        }
    }
}
