using Chaos.Client.Controls.World.ViewPort;
using Chaos.Client.Rendering;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class PollPanelOptionTextTests
{
    [Test]
    public void Short_option_text_is_unchanged()
    {
        PollPanel.FitOptionText("Yes", 100).Should().Be("Yes");
        PollPanel.FitOptionText(string.Empty, 100).Should().BeEmpty();
    }

    [Test]
    public void Empty_or_nonpositive_width_yields_empty()
    {
        PollPanel.FitOptionText("Anything", 0).Should().BeEmpty();
        PollPanel.FitOptionText("Anything", -5).Should().BeEmpty();
    }

    [Test]
    public void Long_option_text_is_truncated_with_an_ellipsis_and_fits()
    {
        var text = new string('W', 60);
        var maxWidth = 120;

        var fitted = PollPanel.FitOptionText(text, maxWidth);

        fitted.Should().EndWith("...");
        TextRenderer.MeasureWidth(fitted).Should().BeLessThanOrEqualTo(maxWidth);
    }
}
