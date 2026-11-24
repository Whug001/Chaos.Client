using Chaos.Client.Rendering.Utility;
using FluentAssertions;
using Microsoft.Xna.Framework;

namespace Chaos.Client.Tests;

public sealed class DrawClipTests
{
    private static readonly Rectangle Bounds = new(100, 100, 50, 50);

    [Test]
    public async Task Inside_is_unchanged()
    {
        DrawClip.Clip(new Rectangle(110, 110, 20, 20), Bounds, out var destination, out var source).Should().BeTrue();
        destination.Should().Be(new Rectangle(110, 110, 20, 20));
        source.Should().Be(new Rectangle(0, 0, 20, 20));

        await Task.CompletedTask;
    }

    [Test]
    public async Task Overhang_is_cut_from_both_rectangles()
    {
        DrawClip.Clip(new Rectangle(90, 80, 40, 40), Bounds, out var destination, out var source).Should().BeTrue();
        destination.Should().Be(new Rectangle(100, 100, 30, 20));
        source.Should().Be(new Rectangle(10, 20, 30, 20));

        await Task.CompletedTask;
    }

    [Test]
    public async Task Outside_or_touching_is_not_drawn()
    {
        DrawClip.Clip(new Rectangle(0, 0, 10, 10), Bounds, out _, out _).Should().BeFalse();
        DrawClip.Clip(new Rectangle(150, 100, 10, 10), Bounds, out _, out _).Should().BeFalse();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Larger_than_bounds_is_cut_on_both_sides()
    {
        DrawClip.Clip(new Rectangle(80, 90, 100, 80), Bounds, out var destination, out var source).Should().BeTrue();
        destination.Should().Be(new Rectangle(100, 100, 50, 50));
        source.Should().Be(new Rectangle(20, 10, 50, 50));

        await Task.CompletedTask;
    }

    [Test]
    public async Task Zero_size_or_negative_rects_are_not_drawn()
    {
        DrawClip.Clip(new Rectangle(110, 110, 0, 20), Bounds, out _, out _).Should().BeFalse();
        DrawClip.Clip(new Rectangle(110, 110, 20, 0), Bounds, out _, out _).Should().BeFalse();
        DrawClip.Clip(new Rectangle(110, 110, -5, 20), Bounds, out _, out _).Should().BeFalse();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Top_and_bottom_edges_are_cut()
    {
        DrawClip.Clip(new Rectangle(110, 80, 20, 40), Bounds, out var top, out var topSource).Should().BeTrue();
        top.Should().Be(new Rectangle(110, 100, 20, 20));
        topSource.Should().Be(new Rectangle(0, 20, 20, 20));

        DrawClip.Clip(new Rectangle(110, 130, 20, 40), Bounds, out var bottom, out var bottomSource).Should().BeTrue();
        bottom.Should().Be(new Rectangle(110, 130, 20, 20));
        bottomSource.Should().Be(new Rectangle(0, 0, 20, 20));

        await Task.CompletedTask;
    }

    //the lock badge is a 7x8 texture drawn at 2x (14x16) at the icon's bottom-right corner: an icon at
    //(0, 0, 32, 32) puts the badge at (18, 16, 14, 16)
    private static readonly Rectangle BadgeDest = new(18, 16, 14, 16);

    [Test]
    public async Task Scaled_fully_visible_draws_the_whole_badge()
    {
        DrawClip.ClipScaled(BadgeDest, new Rectangle(0, 0, 100, 100), 7, 8, out var destination, out var source)
                .Should()
                .BeTrue();
        destination.Should().Be(BadgeDest);
        source.Should().Be(new Rectangle(0, 0, 7, 8));

        await Task.CompletedTask;
    }

    [Test]
    public async Task Scaled_cut_at_top_crops_the_source_top()
    {
        DrawClip.ClipScaled(BadgeDest, new Rectangle(0, 20, 100, 80), 7, 8, out var destination, out var source)
                .Should()
                .BeTrue();
        destination.Should().Be(new Rectangle(18, 20, 14, 12));
        source.Should().Be(new Rectangle(0, 2, 7, 6));

        await Task.CompletedTask;
    }

    [Test]
    public async Task Scaled_cut_at_bottom_keeps_the_source_top()
    {
        DrawClip.ClipScaled(BadgeDest, new Rectangle(0, 0, 100, 24), 7, 8, out var destination, out var source)
                .Should()
                .BeTrue();
        destination.Should().Be(new Rectangle(18, 16, 14, 8));
        source.Should().Be(new Rectangle(0, 0, 7, 4));

        await Task.CompletedTask;
    }

    [Test]
    public async Task Scaled_fully_outside_draws_nothing()
    {
        DrawClip.ClipScaled(BadgeDest, new Rectangle(0, 0, 100, 10), 7, 8, out _, out _).Should().BeFalse();
        DrawClip.ClipScaled(BadgeDest, new Rectangle(32, 0, 100, 100), 7, 8, out _, out _).Should().BeFalse();

        await Task.CompletedTask;
    }
}
