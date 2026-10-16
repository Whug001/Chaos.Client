using Chaos.Client.Rendering;
using FluentAssertions;

namespace Chaos.Client.Tests.Portraits;

public sealed class PortraitStatusWrapTests
{
    private const int STATUS_INNER_WIDTH = 368 - 2;

    [Test]
    public void The_longest_refusal_fits_on_two_lines()
    {
        var lines = TextRenderer.WrapText("Portraits must be exactly 48 x 56 pixels. That file is 9999 x 9999.", STATUS_INNER_WIDTH);

        lines.Count.Should().Be(2);
        lines.Should().OnlyContain(line => TextRenderer.MeasureWidth(line) <= STATUS_INNER_WIDTH);
    }

    [Test]
    public void A_typical_refusal_fits_on_two_lines()
    {
        var lines = TextRenderer.WrapText("Portraits must be exactly 48 x 56 pixels. That file is 1024 x 1024.", STATUS_INNER_WIDTH);

        lines.Count.Should().Be(2);
        lines.Should().OnlyContain(line => TextRenderer.MeasureWidth(line) <= STATUS_INNER_WIDTH);
    }

    [Test]
    public void Short_statuses_stay_on_one_line()
    {
        TextRenderer.WrapText("Portrait set.", STATUS_INNER_WIDTH).Count.Should().Be(1);
        TextRenderer.WrapText("Portrait removed.", STATUS_INNER_WIDTH).Count.Should().Be(1);
        TextRenderer.WrapText("Could not read that file.", STATUS_INNER_WIDTH).Count.Should().Be(1);
    }
}
