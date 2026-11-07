using Chaos.Client.Rendering;
using FluentAssertions;
using Microsoft.Xna.Framework;
using SkiaSharp;

namespace Chaos.Client.Tests.TurkeyShoot;

public sealed class TurkeyShootArtAssetsTests
{
    //formatter:off
    [Test]
    [Arguments("sky")]
    [Arguments("wheat")]
    [Arguments("turkey")]
    [Arguments("turkey_gold")]
    [Arguments("biscuit")]
    [Arguments("crosshair")]
    [Arguments("bolt")]
    [Arguments("puff")]
    [Arguments("banner")]
    //formatter:on
    public async Task Every_sheet_is_an_embedded_png_with_drawn_frames(string key)
    {
        var sheet = TurkeyShootArtAssets.ReadManifest()[key];
        using var bitmap = Decode(key);

        for (var frame = 0; frame < sheet.Frames; frame++)
            CountPixels(bitmap, sheet.Frame(frame), alpha => alpha > 0).Should().BeGreaterThan(0, $"{key} frame {frame} has no pixels");

        await Task.CompletedTask;
    }

    [Test]
    public async Task Sky_covers_the_whole_field()
    {
        using var bitmap = Decode("sky");

        CountPixels(bitmap, new Rectangle(0, 0, bitmap.Width, bitmap.Height), alpha => alpha == 255).Should().Be(bitmap.Width * bitmap.Height);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Wheat_is_open_at_the_top_and_solid_at_the_bottom()
    {
        using var bitmap = Decode("wheat");

        CountPixels(bitmap, new Rectangle(0, 0, bitmap.Width, 1), alpha => alpha == 0).Should().BeGreaterThan(bitmap.Width / 2);
        CountPixels(bitmap, new Rectangle(0, bitmap.Height - 1, bitmap.Width, 1), alpha => alpha == 255).Should().Be(bitmap.Width);

        await Task.CompletedTask;
    }

    //formatter:off
    [Test]
    [Arguments("turkey")]
    [Arguments("turkey_gold")]
    [Arguments("biscuit")]
    [Arguments("crosshair")]
    [Arguments("puff")]
    //formatter:on
    public async Task Sprite_corners_are_clear_so_the_sky_shows(string key)
    {
        var sheet = TurkeyShootArtAssets.ReadManifest()[key];
        using var bitmap = Decode(key);

        for (var frame = 0; frame < sheet.Frames; frame++)
        {
            var rect = sheet.Frame(frame);

            foreach (var (x, y) in new[] { (rect.Left, rect.Top), (rect.Right - 1, rect.Top), (rect.Left, rect.Bottom - 1), (rect.Right - 1, rect.Bottom - 1) })
                bitmap.GetPixel(x, y).Alpha.Should().Be(0, $"{key} frame {frame} corner ({x}, {y})");
        }

        await Task.CompletedTask;
    }

    private static SKBitmap Decode(string key)
    {
        using var stream = TurkeyShootArtAssets.OpenAsset(key);

        return SKBitmap.Decode(stream) ?? throw new InvalidDataException($"{key} did not decode");
    }

    private static int CountPixels(SKBitmap bitmap, Rectangle rect, Func<byte, bool> match)
    {
        var count = 0;

        for (var y = rect.Top; y < rect.Bottom; y++)
            for (var x = rect.Left; x < rect.Right; x++)
                if (match(bitmap.GetPixel(x, y).Alpha))
                    count++;

        return count;
    }
}
