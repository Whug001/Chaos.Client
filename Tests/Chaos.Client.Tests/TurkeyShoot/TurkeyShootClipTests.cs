using Chaos.Client.Controls.World.Popups.TurkeyShoot;
using FluentAssertions;
using Microsoft.Xna.Framework;

namespace Chaos.Client.Tests.TurkeyShoot;

public sealed class TurkeyShootClipTests
{
    [Test]
    public async Task Sprite_inside_the_field_draws_whole()
    {
        var clip = TurkeyShootControl.ClipToField(new Rectangle(0, 0, 32, 32), new Rectangle(100, 50, 32, 32), false);

        clip.Should().Be((new Rectangle(0, 0, 32, 32), new Rectangle(100, 50, 32, 32)));

        await Task.CompletedTask;
    }

    [Test]
    public async Task Left_edge_crops_the_left_of_the_source()
    {
        var clip = TurkeyShootControl.ClipToField(new Rectangle(0, 0, 32, 32), new Rectangle(-10, 50, 32, 32), false);

        clip.Should().Be((new Rectangle(10, 0, 22, 32), new Rectangle(0, 50, 22, 32)));

        await Task.CompletedTask;
    }

    [Test]
    public async Task Flipped_sprite_at_the_left_edge_crops_the_right_of_the_source()
    {
        var clip = TurkeyShootControl.ClipToField(new Rectangle(64, 0, 32, 32), new Rectangle(-10, 50, 32, 32), true);

        clip.Should().Be((new Rectangle(64, 0, 22, 32), new Rectangle(0, 50, 22, 32)));

        await Task.CompletedTask;
    }

    [Test]
    public async Task Flipped_sprite_at_the_right_edge_crops_the_left_of_the_source()
    {
        var clip = TurkeyShootControl.ClipToField(new Rectangle(64, 0, 32, 32), new Rectangle(370, 50, 32, 32), true);

        clip.Should().Be((new Rectangle(82, 0, 14, 32), new Rectangle(370, 50, 14, 32)));

        await Task.CompletedTask;
    }

    [Test]
    public async Task Top_edge_crops_the_top_of_the_source()
    {
        var clip = TurkeyShootControl.ClipToField(new Rectangle(64, 32, 32, 32), new Rectangle(100, -8, 32, 32), false);

        clip.Should().Be((new Rectangle(64, 40, 32, 24), new Rectangle(100, 0, 32, 24)));

        await Task.CompletedTask;
    }

    [Test]
    public async Task Sprite_outside_the_field_draws_nothing()
    {
        TurkeyShootControl.ClipToField(new Rectangle(0, 0, 32, 32), new Rectangle(-32, 50, 32, 32), false).Should().BeNull();
        TurkeyShootControl.ClipToField(new Rectangle(0, 0, 32, 32), new Rectangle(100, 216, 32, 32), true).Should().BeNull();

        await Task.CompletedTask;
    }
}
