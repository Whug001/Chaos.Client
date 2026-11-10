using Chaos.Client.Rendering;
using DALib.Drawing;
using FluentAssertions;
using SkiaSharp;

namespace Chaos.Client.Tests;

public class WeaponLuminanceBlendTests
{
    [Test]
    [Arguments('w', 1000, true)]
    [Arguments('p', 1003, true)]
    [Arguments('w', 0, false)]
    [Arguments('w', 999, false)]
    [Arguments('c', 1000, false)]
    [Arguments('g', 1000, false)]
    [Arguments('u', 1000, false)]
    public async Task OnlyWeaponLayersWithABlendPaletteNumberBlend(char typeLetter, int paletteNumber, bool expected)
    {
        AislingRenderer.BlendsByLuminance(typeLetter, paletteNumber).Should().Be(expected);

        await Task.CompletedTask;
    }

    [Test]
    public async Task BrightnessSetsOpacity()
    {
        var colors = Enumerable.Repeat(SKColors.Black, 256).ToArray();
        colors[1] = new SKColor(255, 251, 199); //brightest channel 250+: solid
        colors[2] = new SKColor(159, 155, 187); //175-199: 60%
        colors[3] = new SKColor(60, 60, 70);    //below 75: invisible

        var blended = AislingRenderer.WithLuminanceAlpha(new Palette(colors));

        blended[1].Should().Be(new SKColor(255, 251, 199, 255));
        blended[2].Should().Be(new SKColor(159, 155, 187, 153));
        blended[3].Alpha.Should().Be(0);

        await Task.CompletedTask;
    }
}
