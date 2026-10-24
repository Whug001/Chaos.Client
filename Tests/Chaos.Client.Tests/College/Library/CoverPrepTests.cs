using Chaos.Client.Systems.College;
using FluentAssertions;
using SkiaSharp;

namespace Chaos.Client.Tests.College.Library;

public class CoverPrepTests
{
    private static byte[] Png(int width, int height)
    {
        using var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque);

        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                bitmap.SetPixel(x, y, new SKColor((byte)((x * 3 + y) % 256), (byte)((x + y * 5) % 256), (byte)((x * 7 + y * 11) % 256)));

        using var image = SKImage.FromBitmap(bitmap);

        return image.Encode(SKEncodedImageFormat.Png, 100).ToArray();
    }

    [Test]
    public void A_large_portrait_image_is_scaled_to_fit_the_cover()
    {
        var picture = PicturePrep.PrepareCover(Png(480, 680), out var error);

        error.Should().BeEmpty();
        (picture!.Width, picture.Height).Should().Be((240, 340));
    }

    [Test]
    public void A_wide_image_is_scaled_by_its_width()
    {
        var picture = PicturePrep.PrepareCover(Png(800, 200), out var error);

        error.Should().BeEmpty();
        (picture!.Width, picture.Height).Should().Be((240, 60));
    }

    [Test]
    public void A_small_image_is_kept_at_its_size()
    {
        var picture = PicturePrep.PrepareCover(Png(100, 150), out var error);

        error.Should().BeEmpty();
        (picture!.Width, picture.Height).Should().Be((100, 150));
    }

    [Test]
    public void A_file_that_is_not_a_picture_is_refused()
    {
        PicturePrep.PrepareCover([1, 2, 3, 4], out var error).Should().BeNull();
        error.Should().NotBeEmpty();
    }

    [Test]
    public void A_file_over_20_MB_is_refused()
    {
        PicturePrep.PrepareCover(new byte[PicturePrep.MAX_FILE_BYTES + 1], out var error).Should().BeNull();
        error.Should().Be(PicturePrep.FILE_TOO_BIG);
    }

    [Test]
    public void A_turned_portrait_photo_is_scaled_to_fit_the_cover()
    {
        using var bitmap = new SKBitmap(680, 480, SKColorType.Rgba8888, SKAlphaType.Opaque);
        bitmap.Erase(SKColors.Teal);
        using var image = SKImage.FromBitmap(bitmap);
        var file = WithOrientation(image.Encode(SKEncodedImageFormat.Jpeg, 95).ToArray(), 6);

        var picture = PicturePrep.PrepareCover(file, out var error);

        error.Should().BeEmpty();
        (picture!.Width, picture.Height).Should().Be((240, 340));
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
}
