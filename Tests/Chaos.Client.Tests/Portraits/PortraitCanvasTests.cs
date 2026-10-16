using Chaos.Client.ViewModel.College;
using Chaos.Client.ViewModel.Portraits;
using FluentAssertions;
using Microsoft.Xna.Framework;
using SkiaSharp;

namespace Chaos.Client.Tests.Portraits;

public sealed class PortraitCanvasTests
{
    private static byte[] PngBytes(int width, int height, (int X, int Y, SKColor Color)? paint = null)
    {
        using var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using (var canvas = new SKCanvas(bitmap))
            canvas.Clear(SKColors.White);

        if (paint is { } p)
            bitmap.SetPixel(p.X, p.Y, p.Color);

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);

        return data.ToArray();
    }

    [Test]
    public void Wrong_size_upload_names_its_size()
    {
        var bytes = PngBytes(47, 56);
        var error = PortraitCanvas.TryFromBytes(bytes, out _);
        error.Should().Be("Portraits must be exactly 48 x 56 pixels. That file is 47 x 56.");
    }

    [Test]
    public void Png_round_trip_is_pixel_exact()
    {
        var canvas = PortraitCanvas.Blank();
        canvas.Set(3, 4, new Color(10, 20, 30));
        PortraitCanvas.TryFromBytes(canvas.ToPng(), out var back).Should().Be(string.Empty);
        back!.ColorAt(3, 4).Should().Be(new Color(10, 20, 30));
    }

    [Test]
    public void Eyedropper_reads_a_non_swatch_colour()
    {
        var canvas = PortraitCanvas.Blank();
        canvas.Set(0, 0, new Color(1, 2, 3));
        canvas.Pick(0, 0).Should().Be(new Color(1, 2, 3));
    }

    [Test]
    public void A_valid_upload_loads_pixels_and_fills_swatches()
    {
        var canvas = PortraitCanvas.Blank();
        canvas.LoadUpload(PngBytes(48, 56, paint: (1, 1, new SKColor(200, 10, 10)))).Should().Be(string.Empty);
        canvas.ColorAt(1, 1).Should().Be(new Color(200, 10, 10));
        canvas.Swatches.Should().Contain(new Color(200, 10, 10));
    }

    [Test]
    public void A_blank_canvas_is_transparent_with_the_default_swatches()
    {
        var canvas = PortraitCanvas.Blank();
        canvas.ColorAt(0, 0).Should().Be(new Color(0, 0, 0, 0));
        canvas.Swatches.Should().Equal(ArtPalette.Default);
        canvas.IsDirty.Should().BeFalse();
    }

    [Test]
    public void Undo_and_redo_restore_pixels()
    {
        var canvas = PortraitCanvas.Blank();
        canvas.BeginStep();
        canvas.Set(3, 3, new Color(10, 20, 30));
        canvas.EndStep();

        canvas.Undo();
        canvas.ColorAt(3, 3).Should().Be(new Color(0, 0, 0, 0));
        canvas.CanRedo.Should().BeTrue();

        canvas.Redo();
        canvas.ColorAt(3, 3).Should().Be(new Color(10, 20, 30));
    }

    [Test]
    public void A_step_that_changes_nothing_leaves_nothing_to_undo()
    {
        var canvas = PortraitCanvas.Blank();
        canvas.BeginStep();
        canvas.Set(3, 3, new Color(0, 0, 0, 0));
        canvas.EndStep();

        canvas.CanUndo.Should().BeFalse();
        canvas.IsDirty.Should().BeFalse();
    }

    [Test]
    public void Fill_does_not_cross_corner_touches()
    {
        var canvas = PortraitCanvas.Blank();
        var barrier = new Color(200, 10, 10);
        var fill = new Color(10, 200, 10);
        canvas.Set(0, 2, barrier);
        canvas.Set(1, 1, barrier);
        canvas.Set(2, 0, barrier);

        canvas.Fill(0, 0, fill);

        canvas.ColorAt(0, 0).Should().Be(fill);
        canvas.ColorAt(1, 0).Should().Be(fill);
        canvas.ColorAt(0, 1).Should().Be(fill);
        canvas.ColorAt(2, 2).Should().Be(new Color(0, 0, 0, 0));
        canvas.ColorAt(1, 1).Should().Be(barrier);
    }

    [Test]
    public void Mirror_paints_both_sides()
    {
        var canvas = PortraitCanvas.Blank();
        canvas.Mirror = true;
        var color = new Color(10, 20, 30);

        canvas.Set(0, 0, color);

        canvas.ColorAt(0, 0).Should().Be(color);
        canvas.ColorAt(PortraitCanvas.WIDTH - 1, 0).Should().Be(color);
        canvas.ColorAt(1, 0).Should().Be(new Color(0, 0, 0, 0));
    }

    [Test]
    public void Fill_with_mirror_paints_both_sides()
    {
        var canvas = PortraitCanvas.Blank();
        canvas.Mirror = true;
        var barrier = new Color(200, 10, 10);
        var fill = new Color(10, 20, 30);

        for (var y = 0; y < PortraitCanvas.HEIGHT; y++)
            canvas.Set(10, y, barrier);

        canvas.Fill(0, 0, fill);

        canvas.ColorAt(0, 0).Should().Be(fill);
        canvas.ColorAt(PortraitCanvas.WIDTH - 1, 0).Should().Be(fill);
        canvas.ColorAt(10, 0).Should().Be(barrier);
        canvas.ColorAt(20, 0).Should().Be(new Color(0, 0, 0, 0));
    }

    [Test]
    public void An_upload_undoes_as_one_step()
    {
        var canvas = PortraitCanvas.Blank();
        canvas.LoadUpload(PngBytes(48, 56, paint: (1, 1, new SKColor(200, 10, 10)))).Should().Be(string.Empty);

        canvas.Undo();

        canvas.ColorAt(1, 1).Should().Be(new Color(0, 0, 0, 0));
        canvas.CanRedo.Should().BeTrue();
    }

    [Test]
    public void Clear_empties_to_swatch_zero_as_one_undo_step()
    {
        var canvas = PortraitCanvas.Blank();
        canvas.BeginStep();
        canvas.Set(5, 5, new Color(10, 20, 30));
        canvas.EndStep();

        canvas.Clear();

        canvas.ColorAt(5, 5).Should().Be(canvas.Swatches[0]);
        canvas.Undo();
        canvas.ColorAt(5, 5).Should().Be(new Color(10, 20, 30));
    }

    [Test]
    public void Swatches_take_the_most_used_colours_first()
    {
        var canvas = PortraitCanvas.Blank();
        var common = new Color(10, 20, 30);
        var rare = new Color(200, 10, 10);
        canvas.Set(0, 0, common);
        canvas.Set(1, 0, common);
        canvas.Set(2, 0, common);
        canvas.Set(3, 0, rare);

        canvas.FillSwatchesFrom();

        canvas.Swatches[0].Should().Be(new Color(0, 0, 0, 0));
        canvas.Swatches[1].Should().Be(common);
        canvas.Swatches[2].Should().Be(rare);
    }

    [Test]
    public void A_refused_upload_keeps_the_old_pixels()
    {
        var canvas = PortraitCanvas.Blank();
        var kept = new Color(10, 20, 30);
        canvas.Set(3, 4, kept);

        var error = canvas.LoadUpload(PngBytes(47, 56));

        error.Should().Be("Portraits must be exactly 48 x 56 pixels. That file is 47 x 56.");
        canvas.ColorAt(3, 4).Should().Be(kept);
    }

    [Test]
    public void Non_picture_bytes_are_refused()
    {
        PortraitCanvas.TryFromBytes([1, 2, 3], out var canvas).Should().Be("That file is not a PNG or JPEG picture.");
        canvas.Should().BeNull();
    }

    [Test]
    public void Hash_matches_sha256_lowercase_hex()
    {
        PortraitCanvas.HashOf("abc"u8.ToArray()).Should().Be("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad");
    }
}
