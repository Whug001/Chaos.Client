using Chaos.Client.Systems.College;
using Chaos.Client.ViewModel.College;
using FluentAssertions;
using Microsoft.Xna.Framework;
using SkiaSharp;

namespace Chaos.Client.Tests.College;

public class DrawingPicturesTests
{
    private static PixelDrawing Sample()
    {
        var drawing = PixelDrawing.Blank();

        for (var x = 0; x < 30; x++)
            drawing.Set(x, 10, 5);

        drawing.Set(95, 71, 21);

        return drawing;
    }

    private static byte[] Png(int width, int height, Func<int, int, SKColor> colour)
    {
        using var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque);

        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                bitmap.SetPixel(x, y, colour(x, y));

        using var image = SKImage.FromBitmap(bitmap);

        return image.Encode(SKEncodedImageFormat.Png, 100).ToArray();
    }

    [Test]
    public void Colours_are_blocks_of_the_zoom()
    {
        var drawing = Sample();

        var colours = DrawingTextures.ToColors(drawing.Palette, drawing.Pixels, 2);

        colours.Should().HaveCount(192 * 144);
        colours[(20 * 192) + 0].Should().Be(drawing.Palette[5]);
        colours[(21 * 192) + 1].Should().Be(drawing.Palette[5]);
        colours[0].Should().Be(drawing.Palette[0]);
        colours.Should().OnlyContain(c => c.A == 255);
    }

    [Test]
    public void A_drawing_round_trips_through_its_picture()
    {
        var drawing = Sample();

        var picture = DrawingPictures.ToPicture(drawing);

        (picture.Width, picture.Height).Should().Be((384, 288));
        picture.Bytes[..4].Should().Equal(0x89, 0x50, 0x4E, 0x47);
        picture.Hash.Should().MatchRegex("^[0-9a-f]{64}$");

        var back = DrawingPictures.TryRead(picture.Bytes)!;

        for (var i = 0; i < drawing.Pixels.Length; i++)
            back.Palette[back.Pixels[i]].Should().Be(drawing.Palette[drawing.Pixels[i]]);
    }

    [Test]
    public void The_rebuilt_palette_keeps_first_seen_colours_then_defaults()
    {
        var drawing = PixelDrawing.Blank();
        drawing.Set(0, 0, 21);

        var back = DrawingPictures.TryRead(DrawingPictures.ToPicture(drawing).Bytes)!;

        back.Palette[0].Should().Be(ArtPalette.Default[21]);
        back.Palette[1].Should().Be(ArtPalette.Default[0]);
        back.Palette[2].Should().Be(ArtPalette.Default[1]);
        back.Palette.Distinct().Should().HaveCount(32);
        back.Pixels[0].Should().Be(0);
        back.Pixels[1].Should().Be(1);
    }

    [Test]
    public void Other_pictures_are_not_drawings()
    {
        DrawingPictures.TryRead(Png(100, 100, (_, _) => SKColors.Red)).Should().BeNull();
        DrawingPictures.TryRead(Png(384, 288, (x, y) => (x == 1) && (y == 1) ? SKColors.Blue : SKColors.Red)).Should().BeNull();
        DrawingPictures.TryRead(Png(384, 288, (x, y) => (y < 4) && (x < 33 * 4) ? new SKColor((byte)(x / 4), 0, 0) : SKColors.White)).Should().BeNull();
        DrawingPictures.TryRead([1, 2, 3]).Should().BeNull();
    }
}
