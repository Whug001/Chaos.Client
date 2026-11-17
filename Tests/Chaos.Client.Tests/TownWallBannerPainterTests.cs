using Chaos.Client.Rendering;
using Chaos.DarkAges.Definitions;
using FluentAssertions;
using Microsoft.Xna.Framework;

namespace Chaos.Client.Tests;

public sealed class TownWallBannerPainterTests
{
    private const int SIZE = TownWallBannerArt.TEXTURE_SIZE;

    //pixel (3, 5) is colour 1 = red; colour 2 = blue paints everything else, so blue is the cloth
    private static GuildEmblemDesign RedOnBlue()
        => new()
        {
            Colors = [new GuildCloakColor(200, 10, 10), new GuildCloakColor(20, 40, 200)],
            Pixels = Enumerable.Range(0, GuildEmblemProtocol.PIXEL_COUNT)
                               .Select(i => i == (5 * GuildEmblemProtocol.SIZE) + 3 ? (byte)1 : (byte)2)
                               .ToArray()
        };

    [Test]
    public void The_green_cloth_takes_the_banners_main_colour()
    {
        var texels = TownWallBannerPainter.Texels(RedOnBlue());

        //(48, 8) is green cloth in the baked banner, above the crest
        var cloth = texels[(8 * SIZE) + 48];
        cloth.A.Should().Be(255);
        cloth.B.Should().BeGreaterThan(cloth.G);
        cloth.B.Should().BeGreaterThan(cloth.R);
    }

    [Test]
    public void The_emblem_replaces_the_crest_at_2x()
    {
        var texels = TownWallBannerPainter.Texels(RedOnBlue());
        var u = TownWallBannerPainter.EMBLEM_U + 6;
        var v = TownWallBannerPainter.EMBLEM_V + 10;

        for (var dv = 0; dv < 2; dv++)
            for (var du = 0; du < 2; du++)
                texels[((v + dv) * SIZE) + u + du].Should().Be(new Color(200, 10, 10, 255));
    }

    [Test]
    public void The_gold_edging_and_plain_wall_are_left_alone()
    {
        var texels = TownWallBannerPainter.Texels(RedOnBlue());

        //(43, 20) is the gold edging, (10, 60) plain wall
        texels[(20 * SIZE) + 43].A.Should().Be(0);
        texels[(60 * SIZE) + 10].A.Should().Be(0);
    }

    [Test]
    public void Faces_follow_the_wall_art_scripts_slant()
    {
        TownWallBannerPainter.FaceTop(0, 0).Should().Be(13 - 87);
        TownWallBannerPainter.FaceTop(0, 27).Should().Be(13 + 13 - 87);
        TownWallBannerPainter.FaceTop(1, 0).Should().Be(26 - 87);
        TownWallBannerPainter.FaceTop(1, 27).Should().Be(26 - 13 - 87);
        TownWallBannerPainter.Slice(12, 0, 0).Should().Be(0);
        TownWallBannerPainter.Slice(10, 11, 1).Should().Be(0);
        TownWallBannerPainter.Slice(10, 8, 0).Should().Be(2);
    }

    [Test]
    public void A_texel_lands_on_its_face_pixel()
    {
        var texels = new Color[SIZE * SIZE];
        texels[(20 * SIZE) + 30] = Color.Red;

        //slice 1 on tile (13, 0)'s left piece holds texture columns 28-55, so column 30 is face column 2
        var cell = TownWallBannerPainter.Cell(texels, 13, 0, leftWall: true, rightWall: false);
        var row = TownWallBannerPainter.FaceTop(0, 2) + 20 - TownWallBannerPainter.CELL_TOP;

        cell[(row * TownWallBannerPainter.CELL_WIDTH) + 2].Should().Be(Color.Red);
        cell.Count(p => p.A != 0).Should().Be(1);
        TownWallBannerPainter.Cell(texels, 13, 0, leftWall: false, rightWall: false).Should().OnlyContain(p => p.A == 0);
    }
}
