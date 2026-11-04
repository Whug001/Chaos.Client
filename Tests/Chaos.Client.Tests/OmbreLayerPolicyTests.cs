using Chaos.Client.Rendering;
using Chaos.DarkAges.Definitions;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class OmbreLayerPolicyTests
{
    [Test]
    [Arguments('h')]
    [Arguments('e')]
    [Arguments('f')]
    [Arguments('u')]
    [Arguments('i')]
    [Arguments('a')]
    [Arguments('j')]
    [Arguments('l')]
    [Arguments('n')]
    [Arguments('c')]
    [Arguments('g')]
    [Arguments('w')]
    [Arguments('p')]
    [Arguments('s')]
    [Arguments('b')]
    public async Task Every_layer_uses_ombre_for_ombre_dyes(char typeLetter)
    {
        AislingRenderer.ShouldUseOmbre(typeLetter, (DisplayColor)72).Should().BeTrue();
        AislingRenderer.ShouldUseOmbre(typeLetter, (DisplayColor)77).Should().BeTrue();
        AislingRenderer.ShouldUseOmbre(typeLetter, (DisplayColor)88).Should().BeTrue();

        await Task.CompletedTask;
    }

    [Test]
    [Arguments('h')]
    [Arguments('e')]
    [Arguments('f')]
    [Arguments('u')]
    [Arguments('i')]
    [Arguments('a')]
    [Arguments('j')]
    [Arguments('l')]
    [Arguments('n')]
    [Arguments('c')]
    [Arguments('g')]
    [Arguments('w')]
    [Arguments('p')]
    [Arguments('s')]
    [Arguments('b')]
    public async Task No_layer_uses_ombre_for_plain_dyes(char typeLetter)
    {
        AislingRenderer.ShouldUseOmbre(typeLetter, DisplayColor.Default).Should().BeFalse();
        AislingRenderer.ShouldUseOmbre(typeLetter, DisplayColor.Apple).Should().BeFalse();
        AislingRenderer.ShouldUseOmbre(typeLetter, (DisplayColor)71).Should().BeFalse();
        AislingRenderer.ShouldUseOmbre(typeLetter, (DisplayColor)89).Should().BeFalse();

        await Task.CompletedTask;
    }

    [Test]
    [Arguments('h')]
    [Arguments('e')]
    [Arguments('f')]
    public async Task Head_layers_take_fade_rows_from_the_h_frame(char typeLetter)
    {
        AislingRenderer.UsesHeadFadeRows(typeLetter).Should().BeTrue();

        await Task.CompletedTask;
    }

    [Test]
    [Arguments('u')]
    [Arguments('i')]
    [Arguments('a')]
    [Arguments('j')]
    [Arguments('l')]
    [Arguments('n')]
    [Arguments('c')]
    [Arguments('g')]
    [Arguments('w')]
    [Arguments('p')]
    [Arguments('s')]
    [Arguments('b')]
    public async Task Other_layers_take_fade_rows_from_their_own_frame(char typeLetter)
    {
        AislingRenderer.UsesHeadFadeRows(typeLetter).Should().BeFalse();

        await Task.CompletedTask;
    }
}
