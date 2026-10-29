#region
using Chaos.Client.Rendering;
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Karting;
using FluentAssertions;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Tests.Karting;

public sealed class KartLayerTests
{
    //formatter:off
    [Test]
    [Arguments(KartLayer.Base)]
    [Arguments(KartLayer.Shadow)]
    [Arguments(KartLayer.Effect)]
    //formatter:on
    public void Only_the_driver_layer_takes_the_racer_dye(KartLayer layer)
    {
        KartLayerTint.For(layer, Color.Red).Should().Be(Color.White);
        KartLayerTint.For(KartLayer.Driver, Color.Red).Should().Be(Color.Red);
        KartLayerTint.For(KartLayer.Driver, Color.Cyan).Should().Be(Color.Cyan);
    }

    [Test]
    public void Every_heading_uses_matching_base_and_driver_frame_geometry()
    {
        var body = KartArtAssets.ReadManifest()["kart_base"];
        var driver = KartArtAssets.ReadManifest()["kart_driver"];

        for (var frame = 0; frame < 16; frame++)
        {
            body.Frame(frame).Should().Be(driver.Frame(frame));
            body.Frame(frame).Right.Should().BeLessThanOrEqualTo(320);
            body.Frame(frame).Bottom.Should().BeLessThanOrEqualTo(240);
        }

        (body.AnchorX, body.AnchorY).Should().Be((40, 45));
        (driver.AnchorX, driver.AnchorY).Should().Be((40, 45));
    }

    [Test]
    public void Roulette_icons_do_not_need_or_reveal_the_hidden_roll_result()
    {
        KartItemSlot.ShownKind(KartItemKind.None, 20, 0).Should().Be(KartItemKind.EmberBolt);
        KartItemSlot.ShownKind(KartItemKind.None, 20, 2).Should().Be(KartItemKind.EmberBolt);
        KartItemSlot.ShownKind(KartItemKind.None, 20, 50).Should().Be(KartItemKind.HuntingRaven);
        KartItemSlot.ShownKind(KartItemKind.None, 20, 350).Should().Be(KartItemKind.EmberBolt);
        KartItemSlot.ShownKind(KartItemKind.Gloaming, 0, 220).Should().Be(KartItemKind.Gloaming);
        KartItemSlot.ShownKind(KartItemKind.None, 0, 220).Should().Be(KartItemKind.None);
        KartItemArt.IconKey(KartItemKind.None).Should().BeNull();
    }

    //formatter:off
    [Test]
    [Arguments(0, null)]
    [Arguments(1, "medal_gold")]
    [Arguments(2, "medal_silver")]
    [Arguments(3, "medal_bronze")]
    [Arguments(4, null)]
    //formatter:on
    public void Only_podium_medals_select_art(int medal, string? key)
        => KartItemArt.MedalKey(medal).Should().Be(key);
    [Test]
    public void All_racer_body_colours_map_without_wrapping_the_legacy_palette()
    {
        KartLayerTint.Dye((byte)Chaos.DarkAges.Definitions.BodyColor.White).Should().Be(Color.White);
        KartLayerTint.Dye((byte)Chaos.DarkAges.Definitions.BodyColor.Green).Should().Be(Color.Green);
        KartLayerTint.Dye((byte)Chaos.DarkAges.Definitions.BodyColor.Orange).Should().Be(Color.Orange);
        KartLayerTint.Dye((byte)Chaos.DarkAges.Definitions.BodyColor.Purple).Should().Be(Color.Purple);
        KartLayerTint.Dye(255).Should().Be(Color.White);
    }
}
