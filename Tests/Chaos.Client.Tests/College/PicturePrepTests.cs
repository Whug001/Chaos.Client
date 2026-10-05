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
}
