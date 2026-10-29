#region
using Chaos.Client.Rendering;
using Chaos.Client.Systems.Karting;
using FluentAssertions;
using Microsoft.Xna.Framework;
using SkiaSharp;
#endregion

namespace Chaos.Client.Tests.Karting;

public sealed class KartHeadTests
{
    private static Color[] Pixels(int width, int height, params Rectangle[] opaque)
    {
        var pixels = new Color[width * height];

        foreach (var rect in opaque)
            for (var y = rect.Top; y < rect.Bottom; y++)
                for (var x = rect.Left; x < rect.Right; x++)
                    pixels[y * width + x] = Color.White;

        return pixels;
    }

    [Test]
    public void Short_hair_anchors_to_the_bald_chin()
    {
        var bald = Pixels(12, 10, new Rectangle(4, 2, 4, 5));
        var hair = Pixels(12, 10, new Rectangle(3, 1, 6, 6));

        KartHeadPixelMath.Measure(hair, bald, 12, 10, 0, 12)
            .Should().Be(new KartHeadCrop(1, 6, new Vector2(6, 6)));
    }

    [Test]
    public void Hair_below_the_chin_does_not_change_the_anchor_row()
    {
        var bald = Pixels(12, 12, new Rectangle(4, 3, 4, 5));
        var hair = Pixels(12, 12, new Rectangle(2, 0, 8, 12));

        KartHeadPixelMath.Measure(hair, bald, 12, 12, 0, 12)
            .Should().Be(new KartHeadCrop(0, 11, new Vector2(6, 8)));
    }

    [Test]
    public void A_missing_bald_render_keeps_the_existing_haired_anchor()
    {
        var hair = Pixels(12, 12, new Rectangle(2, 0, 8, 12));

        KartHeadPixelMath.Measure(hair, null, 12, 12, 0, 12)
            .Should().Be(new KartHeadCrop(0, 11, new Vector2(6, 12)));
    }

    [Test]
    public void Wide_hair_fits_inside_the_forty_four_pixel_crop_while_using_the_face_chin()
    {
        const int width = 60;
        var bald = Pixels(width, 12, new Rectangle(27, 3, 6, 5));
        var hair = Pixels(width, 12, new Rectangle(8, 0, 44, 12));

        KartHeadPixelMath.Measure(hair, bald, width, 12, 8, 44)
            .Should().Be(new KartHeadCrop(0, 11, new Vector2(22, 8)));
        KartHeadPixelMath.CROP_WIDTH.Should().Be(44);
    }

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
