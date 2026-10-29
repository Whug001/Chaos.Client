#region
using Chaos.Client.Rendering;
using FluentAssertions;
using SkiaSharp;
#endregion

namespace Chaos.Client.Tests.Karting;

public sealed class KartHeadTests
{
    //formatter:off
    [Test]
    [Arguments(0, true, false)]
    [Arguments(4, true, false)]
    [Arguments(5, true, true)]
    [Arguments(8, true, true)]
    [Arguments(9, false, true)]
    [Arguments(12, false, true)]
    [Arguments(13, false, false)]
    [Arguments(15, false, false)]
    [Arguments(16, true, false)]
    [Arguments(-1, false, false)]
    //formatter:on
    public void Facing_follows_the_kart_heading(int frame, bool front, bool flip)
        => KartHead.Facing(frame).Should().Be((front, flip));

    [Test]
    public void Headless_driver_has_no_pixels_above_any_neck()
    {
        var sheet = KartArtAssets.ReadManifest()["kart_driver_body"];
        using var stream = KartArtAssets.OpenAsset("kart_driver_body");
        using var bitmap = SKBitmap.Decode(stream);

        for (var frame = 0; frame < sheet.Frames; frame++)
        {
            var neck = KartHead.NeckOffset(frame);
            var left = frame % sheet.Columns * sheet.FrameWidth;
            var top = frame / sheet.Columns * sheet.FrameHeight;
            var neckRow = sheet.AnchorY + (int)neck.Y;

            for (var y = 0; y < neckRow; y++)
                for (var x = 0; x < sheet.FrameWidth; x++)
                    bitmap.GetPixel(left + x, top + y).Alpha.Should().Be(0, $"frame {frame} row {y}");

            Enumerable.Range(0, sheet.FrameWidth)
                      .Any(x => bitmap.GetPixel(left + x, top + neckRow).Alpha > 0)
                      .Should()
                      .BeTrue($"frame {frame} has a body under the neck");
        }
    }
}
