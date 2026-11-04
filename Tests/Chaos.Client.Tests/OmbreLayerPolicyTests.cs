using Chaos.Client.Rendering;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class OmbreLayerPolicyTests
{
    [Test]
    [Arguments('h', true)]
    [Arguments('e', true)]
    [Arguments('f', true)]
    [Arguments('u', false)]
    [Arguments('c', false)]
    [Arguments('w', false)]
    public async Task UsesHeadFadeRows(char typeLetter, bool expected)
    {
        AislingRenderer.UsesHeadFadeRows(typeLetter).Should().Be(expected);

        await Task.CompletedTask;
    }
}
