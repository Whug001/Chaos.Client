using Chaos.Client.Systems.College;
using Chaos.DarkAges.Definitions;
using FluentAssertions;
using SkiaSharp;

namespace Chaos.Client.Tests.College;

public class PicturePrepTests
{
    private static byte[] Encode(int width, int height, bool transparent, SKEncodedImageFormat format = SKEncodedImageFormat.Png)
    {
        using var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        var random = new Random(7);

        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                bitmap.SetPixel(x, y, new SKColor((byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256), transparent && (x == 0) ? (byte)0 : (byte)255));

        using var image = SKImage.FromBitmap(bitmap);

        return image.Encode(format, 95).ToArray();
    }

    [Test]
    public void Fit_size_keeps_shape_within_the_box()
    {
        PicturePrep.FitSize(800, 600).Should().Be((400, 300));
        PicturePrep.FitSize(1000, 200).Should().Be((400, 80));
        PicturePrep.FitSize(300, 900).Should().Be((100, 300));
        PicturePrep.FitSize(120, 90).Should().Be((120, 90));
    }

    [Test]
    public void An_opaque_photo_becomes_a_small_jpeg()
    {
        var picture = PicturePrep.Prepare(Encode(1200, 900, false, SKEncodedImageFormat.Jpeg), out var error);

        error.Should().BeEmpty();
        picture!.Width.Should().Be(400);
        picture.Height.Should().Be(300);
        picture.Bytes.Length.Should().BeLessThanOrEqualTo(CollegeProtocol.MAX_PICTURE_BYTES);
        picture.Bytes[..2].Should().Equal(0xFF, 0xD8);
        picture.Hash.Should().MatchRegex("^[0-9a-f]{64}$");
    }

    [Test]
    public void A_picture_with_transparency_stays_png()
    {
        var picture = PicturePrep.Prepare(Encode(64, 64, true), out _);

        picture!.Bytes[..4].Should().Equal(0x89, 0x50, 0x4E, 0x47);
        (picture.Width, picture.Height).Should().Be((64, 64));
    }

    [Test]
    public void Other_files_are_refused_with_a_message()
    {
        PicturePrep.Prepare([1, 2, 3, 4], out var error).Should().BeNull();
        error.Should().Be(PicturePrep.NOT_A_PICTURE);
    }

    private static byte[] Solid(int width, int height, SKColor color, SKEncodedImageFormat format)
    {
        using var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        bitmap.Erase(color);
        using var image = SKImage.FromBitmap(bitmap);

        return image.Encode(format, 90).ToArray();
    }

    private static byte[] WithOrientation(byte[] jpeg, byte orientation)
    {
        byte[] exif =
        [
            0xFF, 0xE1, 0x00, 0x22,
            (byte)'E', (byte)'x', (byte)'i', (byte)'f', 0, 0,
            (byte)'I', (byte)'I', 0x2A, 0x00, 0x08, 0x00, 0x00, 0x00,
            0x01, 0x00,
            0x12, 0x01, 0x03, 0x00, 0x01, 0x00, 0x00, 0x00, orientation, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00
        ];

        return [.. jpeg[..2], .. exif, .. jpeg[2..]];
    }

    [Test]
    public void A_sideways_phone_photo_is_turned_upright()
    {
        using var bitmap = new SKBitmap(80, 40, SKColorType.Rgba8888, SKAlphaType.Premul);

        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Blue);
            canvas.DrawRect(0, 0, 40, 40, new SKPaint { Color = SKColors.Red });
        }

        using var image = SKImage.FromBitmap(bitmap);
        var file = WithOrientation(image.Encode(SKEncodedImageFormat.Jpeg, 95).ToArray(), 6);

        var picture = PicturePrep.Prepare(file, out var error);

        error.Should().BeEmpty();
        (picture!.Width, picture.Height).Should().Be((40, 80));
        using var upright = SKBitmap.Decode(picture.Bytes);
        (upright.Width, upright.Height).Should().Be((40, 80));
        upright.GetPixel(20, 10).Red.Should().BeGreaterThan(200);
        upright.GetPixel(20, 70).Blue.Should().BeGreaterThan(200);
    }

    [Test]
    public void A_very_large_photo_is_scaled_into_the_box()
    {
        var picture = PicturePrep.Prepare(Solid(6000, 4000, SKColors.Teal, SKEncodedImageFormat.Jpeg), out var error);

        error.Should().BeEmpty();
        (picture!.Width, picture.Height).Should().Be((400, 266));
    }

    [Test]
    public void A_picture_with_too_many_pixels_to_open_is_refused()
    {
        var file = Solid(1000, 1000, SKColors.Teal, SKEncodedImageFormat.Png);

        PicturePrep.Prepare(file, 500_000, out var error).Should().BeNull();
        error.Should().Be(PicturePrep.TOO_MANY_PIXELS);

        PicturePrep.Prepare(file, 1_000_000, out error).Should().NotBeNull();
    }
}
