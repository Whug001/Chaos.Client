using Chaos.Client.Systems;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class TownBallotLookTests
{
    [Test]
    public async Task Every_layer_is_mapped()
    {
        var look = TownBallotLook.ToAppearance(
            new TownBallotLookInfo
            {
                BodySprite = BodySprite.Female,
                BodyColor = (BodyColor)2,
                HeadSprite = 12,
                HeadColor = (DisplayColor)5,
                FaceSprite = 3,
                ArmorSprite = 200,
                OvercoatSprite = 301,
                OvercoatColor = (DisplayColor)6,
                BootsSprite = 4,
                BootsColor = (DisplayColor)7,
                PantsColor = (DisplayColor)8,
                WeaponSprite = 30,
                ShieldSprite = 2,
                AccessorySprite1 = 1001,
                AccessoryColor1 = (DisplayColor)9,
                AccessorySprite2 = 1002,
                AccessorySprite3 = 1003
            });

        look.Gender.Should().Be(Gender.Female);
        look.BodyColor.Should().Be(2);
        look.HeadSprite.Should().Be(12);
        look.FaceSprite.Should().Be(3);
        look.ArmorSprite.Should().Be(200);
        look.OvercoatSprite.Should().Be(301);
        look.BootsSprite.Should().Be(4);
        look.PantsColor.Should().Be((DisplayColor)8);
        look.WeaponSprite.Should().Be(30);
        look.ShieldSprite.Should().Be(2);
        look.Accessory1Sprite.Should().Be(1001);
        look.Accessory3Sprite.Should().Be(1003);
        await Task.CompletedTask;
    }

    [Test]
    public async Task A_ghost_uses_the_ghost_body_color()
    {
        var look = TownBallotLook.ToAppearance(new TownBallotLookInfo { BodySprite = BodySprite.MaleGhost, BodyColor = (BodyColor)2 });

        look.BodyColor.Should().Be((int)BodyColor.LightBlue);
        await Task.CompletedTask;
    }

    [Test]
    public async Task No_look_gives_a_plain_figure()
    {
        TownBallotLook.ToAppearance(null).Should().Be(TownImportPreviewLook.Plain(Gender.Male));
        await Task.CompletedTask;
    }
}
