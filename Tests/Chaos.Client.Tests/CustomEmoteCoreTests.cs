using Chaos.Client.Rendering;
using Chaos.Client.Rendering.CustomEmotes;
using FluentAssertions;
using Microsoft.Xna.Framework;
using SkiaSharp;

namespace Chaos.Client.Tests;

public class CustomEmoteCoreTests
{
    private static readonly Dictionary<char, Color> RedBlue = new()
    {
        ['r'] = new Color(200, 0, 0),
        ['b'] = new Color(0, 0, 200)
    };

    private static SKImage SolidImage(int width, int height, SKColor color)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(color);

        return SKImage.FromBitmap(bitmap);
    }

    [Test]
    public async Task Pixel_art_rejects_ragged_rows()
    {
        var act = () => PixelArt.FromPalette("test:ragged", ["rr", "r"], RedBlue);

        act.Should().Throw<ArgumentException>();
        await Task.CompletedTask;
    }

    [Test]
    public async Task Pixel_art_rejects_an_empty_map()
    {
        var act = () => PixelArt.FromPalette("test:empty", [], RedBlue);

        act.Should().Throw<ArgumentException>();
        await Task.CompletedTask;
    }

    [Test]
    public async Task Colors_lists_each_distinct_colour_once_and_skips_transparent()
    {
        var art = PixelArt.FromPalette("test:colors", ["r.b", "rr."], RedBlue);

        art.Colors().Should().BeEquivalentTo(new[] { new Color(200, 0, 0), new Color(0, 0, 200) });
        await Task.CompletedTask;
    }

    [Test]
    public async Task Colors_throws_on_a_character_the_palette_does_not_define()
    {
        var art = PixelArt.FromPalette("test:unknown", ["rx"], RedBlue);
        var act = () => art.Colors().ToList();

        act.Should().Throw<InvalidOperationException>();
        await Task.CompletedTask;
    }

    [Test]
    public async Task To_bitmap_paints_mapped_pixels_and_leaves_dots_clear()
    {
        using var bitmap = PixelSprite.ToBitmap(PixelArt.FromPalette("test:bitmap", ["r.", "b."], RedBlue));

        bitmap.GetPixel(0, 0).Should().Be(new SKColor(200, 0, 0));
        bitmap.GetPixel(1, 0).Alpha.Should().Be(0);
        bitmap.GetPixel(0, 1).Should().Be(new SKColor(0, 0, 200));
        await Task.CompletedTask;
    }

    [Test]
    public async Task Stamp_grows_up_and_left_for_art_above_and_beside_the_source()
    {
        using var source = SolidImage(2, 2, new SKColor(200, 0, 0));
        var dot = PixelArt.Dot("test:dot-up-left", new Color(0, 0, 200));

        //source's top-left sits at composite (27, 0); the dot sits at (25, -2)
        using var stamped = PixelSprite.Stamp(source, 27, 0, [new PixelLayer(dot, 25, -2)]);
        using var result = SKBitmap.FromImage(stamped);

        result.Width.Should().Be(4);
        result.Height.Should().Be(4);
        result.GetPixel(0, 0).Should().Be(new SKColor(0, 0, 200));
        result.GetPixel(2, 2).Should().Be(new SKColor(200, 0, 0));
        result.GetPixel(3, 3).Should().Be(new SKColor(200, 0, 0));
        result.GetPixel(1, 1).Alpha.Should().Be(0);
        await Task.CompletedTask;
    }

    [Test]
    public async Task Stamp_grows_right_and_down_too()
    {
        using var source = SolidImage(2, 2, new SKColor(200, 0, 0));
        var dot = PixelArt.Dot("test:dot-down-right", new Color(0, 0, 200));

        using var stamped = PixelSprite.Stamp(source, 27, 0, [new PixelLayer(dot, 30, 3)]);
        using var result = SKBitmap.FromImage(stamped);

        result.Width.Should().Be(4);
        result.Height.Should().Be(4);
        result.GetPixel(3, 3).Should().Be(new SKColor(0, 0, 200));
        result.GetPixel(0, 0).Should().Be(new SKColor(200, 0, 0));
        await Task.CompletedTask;
    }

    [Test]
    public async Task Resolve_left_x_mirrors_the_right_edge_about_the_sprite_pivot()
    {
        CustomEmoteDrawContext.ResolveLeftX(false, 52, 10).Should().Be(52);
        CustomEmoteDrawContext.ResolveLeftX(true, 52, 10).Should().Be(AislingRenderer.MirrorX(61));
        await Task.CompletedTask;
    }

    [Test]
    public async Task Head_bob_only_applies_to_the_walk_frames_that_drop_the_head()
    {
        CustomEmoteGeometry.HeadBobOffset(7, "01").Should().Be(1);
        CustomEmoteGeometry.HeadBobOffset(9, "01").Should().Be(1);
        CustomEmoteGeometry.HeadBobOffset(8, "01").Should().Be(0);
        CustomEmoteGeometry.HeadBobOffset(7, "02").Should().Be(0);
        await Task.CompletedTask;
    }

    [Test]
    public async Task Ease_in_rows_clamps_a_fall_from_above()
    {
        CustomEmoteGeometry.EaseInRows(0f, 100f, -10).Should().Be(-10);
        CustomEmoteGeometry.EaseInRows(100f, 100f, -10).Should().Be(0);
        CustomEmoteGeometry.EaseInRows(-5f, 100f, -10).Should().Be(-10);
        await Task.CompletedTask;
    }

    [Test]
    public async Task Ease_in_rows_only_settles_toward_rest_for_a_rise_from_below()
    {
        CustomEmoteGeometry.EaseInRows(0f, 100f, 3).Should().Be(3);
        CustomEmoteGeometry.EaseInRows(100f, 100f, 3).Should().Be(0);

        var previous = int.MaxValue;

        for (var t = 0f; t <= 100f; t += 5f)
        {
            var value = CustomEmoteGeometry.EaseInRows(t, 100f, 3);
            value.Should().BeLessThanOrEqualTo(previous);
            previous = value;
        }

        await Task.CompletedTask;
    }
}
