using Chaos.Client.Controls.World.Emblems;
using Chaos.DarkAges.Definitions;
using FluentAssertions;
using Microsoft.Xna.Framework;

namespace Chaos.Client.Tests;

public class GuildEmblemTexturesTests
{
    [Test]
    public void See_through_pixels_are_transparent_and_painted_ones_take_their_color()
    {
        var design = GuildEmblemDesign.CreateDefault();
        design.Colors.Add(new GuildCloakColor(140, 20, 30));
        design.Pixels[0] = 1;
        design.Pixels[12] = 2;

        var pixels = GuildEmblemTextures.ToPixels(design);

        pixels.Should().HaveCount(GuildEmblemProtocol.PIXEL_COUNT);
        pixels[0].Should().Be(new Color(212, 175, 55));
        pixels[12].Should().Be(new Color(140, 20, 30));
        pixels[1].A.Should().Be(0);
    }

    [Test]
    public void A_server_emblem_is_asked_for_at_most_once_every_ten_seconds()
    {
        //ids unique to this test: the store is static
        GuildEmblemTextures.ShouldRequest(9001, 1_000).Should().BeTrue();
        GuildEmblemTextures.ShouldRequest(9001, 5_000).Should().BeFalse();
        GuildEmblemTextures.ShouldRequest(9001, 11_000).Should().BeTrue();
        GuildEmblemTextures.ShouldRequest(0, 1_000).Should().BeFalse();
        GuildEmblemTextures.ShouldRequest(-3, 1_000).Should().BeFalse();

        GuildEmblemTextures.Set(9001, GuildEmblemDesign.CreateDefault());

        GuildEmblemTextures.ShouldRequest(9001, 50_000).Should().BeFalse();
        GuildEmblemTextures.TryGet(9001, out _).Should().BeTrue();
    }

    [Test]
    public void SetLocal_gives_a_new_negative_id_and_drops_the_one_it_replaces()
    {
        var first = GuildEmblemTextures.SetLocal(0, GuildEmblemDesign.CreateDefault());
        var second = GuildEmblemTextures.SetLocal(first, GuildEmblemDesign.CreateDefault());

        first.Should().BeNegative();
        second.Should().BeLessThan(first);
        GuildEmblemTextures.TryGet(first, out _).Should().BeFalse();
        GuildEmblemTextures.TryGet(second, out _).Should().BeTrue();
    }
}
