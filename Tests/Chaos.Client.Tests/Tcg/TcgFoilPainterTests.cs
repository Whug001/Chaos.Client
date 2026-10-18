using Chaos.Client.Rendering;
using FluentAssertions;
using Microsoft.Xna.Framework;

namespace Chaos.Client.Tests;

public sealed class TcgFoilPainterTests
{
    private const int MaxSheenAlpha = (int)(0.26f * 255) + 1;
    private const float SparkleFull = 0.42f;
    private const float SparkleGone = 1.02f;

    private static Color[] Painted(float seconds)
    {
        var pixels = new Color[TcgFoilPainter.Width * TcgFoilPainter.Height];
        Array.Fill(pixels, new Color(1, 2, 3, 4));
        TcgFoilPainter.Paint(pixels, seconds);

        return pixels;
    }

    [Test]
    public void The_corners_outside_the_rounded_card_are_transparent()
    {
        var pixels = Painted(0f);
        var width = TcgFoilPainter.Width;
        var height = TcgFoilPainter.Height;

        pixels[0].Should().Be(new Color(0, 0, 0, 0));
        pixels[width - 1].Should().Be(new Color(0, 0, 0, 0));
        pixels[(width * (height - 1))].Should().Be(new Color(0, 0, 0, 0));
        pixels[(width * height) - 1].Should().Be(new Color(0, 0, 0, 0));
    }

    [Test]
    public void An_inside_pixel_far_from_any_sparkle_shows_only_the_sheen()
    {
        var pixel = Painted(SparkleGone)[(45 * TcgFoilPainter.Width) + 30];

        pixel.A.Should().BeGreaterThan(0).And.BeLessThanOrEqualTo(MaxSheenAlpha);
        pixel.R.Should().BeLessThanOrEqualTo(pixel.A);
        pixel.G.Should().BeLessThanOrEqualTo(pixel.A);
        pixel.B.Should().BeLessThanOrEqualTo(pixel.A);
    }

    [Test]
    public void The_frame_repeats_every_loop()
        => Painted(1.23f).Should().Equal(Painted(1.23f + TcgFoilPainter.LoopSeconds));

    [Test]
    public void A_sparkle_centre_is_near_white_at_full_alpha_mid_twinkle()
    {
        //(t + 1.58) mod 1.6 == 0.4, so k = sin(pi / 2) = 1
        var centre = Painted(SparkleFull)[(27 * TcgFoilPainter.Width) + 26];

        centre.Should().Be(new Color(255, 255, 240, 255));
    }

    [Test]
    public void A_sparkle_centre_is_plain_sheen_once_its_twinkle_ends()
    {
        //(t + 1.58) mod 1.6 == 1.0, past the 0.8 cutoff, so the sheen shows through untouched
        var centre = Painted(SparkleGone)[(27 * TcgFoilPainter.Width) + 26];

        centre.A.Should().BeGreaterThan(0).And.BeLessThanOrEqualTo(MaxSheenAlpha);
        centre.Should().NotBe(new Color(255, 255, 240, 255));
    }
}
