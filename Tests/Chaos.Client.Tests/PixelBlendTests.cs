using Chaos.Client.Rendering.Utility;
using FluentAssertions;
using Microsoft.Xna.Framework;

namespace Chaos.Client.Tests;

public sealed class PixelBlendTests
{
    private static Color[] Canvas(Color fill) => Enumerable.Repeat(fill, 4).ToArray();

    [Test]
    public async Task Opaque_pixels_replace_and_transparent_pixels_are_skipped()
    {
        var dst = Canvas(new Color(0, 0, 200, 255));
        Color[] src = [new Color(10, 20, 30, 255), Color.Transparent];

        PixelBlend.SourceOver(dst, 2, 2, src, 2, 1, 0, 0);

        dst[0].Should().Be(new Color(10, 20, 30, 255));
        dst[1].Should().Be(new Color(0, 0, 200, 255));

        await Task.CompletedTask;
    }

    [Test]
    public async Task Half_alpha_blends_premultiplied()
    {
        var dst = Canvas(new Color(0, 0, 200, 255));
        Color[] src = [new Color(100, 0, 0, 128)];

        PixelBlend.SourceOver(dst, 2, 2, src, 1, 1, 1, 1);

        //127 = 255 - 128: dst channels are scaled by 127 / 255
        dst[3].Should().Be(new Color(100, 0, 99, 255));

        await Task.CompletedTask;
    }

    [Test]
    public async Task Pixels_outside_the_canvas_are_ignored()
    {
        var dst = Canvas(Color.Transparent);
        Color[] src = [Color.White, Color.White, Color.White, Color.White];

        PixelBlend.SourceOver(dst, 2, 2, src, 2, 2, 1, -1);

        dst[0].Should().Be(Color.Transparent);
        dst[1].Should().Be(Color.White);
        dst[2].Should().Be(Color.Transparent);
        dst[3].Should().Be(Color.Transparent);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Left_and_bottom_overflow_are_ignored()
    {
        var left = Canvas(Color.Transparent);
        Color[] src = [Color.White, Color.White, Color.White, Color.White];

        PixelBlend.SourceOver(left, 2, 2, src, 2, 2, -1, 0);

        left[0].Should().Be(Color.White);
        left[1].Should().Be(Color.Transparent);
        left[2].Should().Be(Color.White);
        left[3].Should().Be(Color.Transparent);

        var bottom = Canvas(Color.Transparent);

        PixelBlend.SourceOver(bottom, 2, 2, src, 2, 2, 0, 1);

        bottom[0].Should().Be(Color.Transparent);
        bottom[1].Should().Be(Color.Transparent);
        bottom[2].Should().Be(Color.White);
        bottom[3].Should().Be(Color.White);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Half_alpha_over_translucent_destination_keeps_both_alphas()
    {
        var dst = Canvas(new Color(0, 0, 200, 100));
        Color[] half = [new Color(100, 0, 0, 128)];

        PixelBlend.SourceOver(dst, 2, 2, half, 1, 1, 0, 0);

        //keep = 255 - 128 = 127: B = 200 * 127 / 255 = 99, A = 128 + 100 * 127 / 255 = 177
        dst[0].Should().Be(new Color(100, 0, 99, 177));
        dst[1].Should().Be(new Color(0, 0, 200, 100));

        await Task.CompletedTask;
    }
}
