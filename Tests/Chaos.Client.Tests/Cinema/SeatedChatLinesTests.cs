using Chaos.Client.Systems.Cinema;
using FluentAssertions;
using Microsoft.Xna.Framework;

namespace Chaos.Client.Tests.Cinema;

public sealed class SeatedChatLinesTests
{
    [Test]
    public void Lines_fade_after_six_seconds()
    {
        var lines = new SeatedChatLines();

        lines.Add("hi", Color.White, 0);
        lines.Visible(6000).Single().Alpha.Should().Be(1f);
        lines.Visible(6500).Single().Alpha.Should().BeApproximately(0.5f, 0.001f);
        lines.Visible(7000).Should().BeEmpty();
    }

    [Test]
    public void Keeps_the_four_newest_oldest_first()
    {
        var lines = new SeatedChatLines();

        for (var i = 0; i < 6; i++)
            lines.Add($"m{i}", Color.White, i);

        lines.Visible(10).Select(l => l.Text).Should().Equal("m2", "m3", "m4", "m5");
    }
}
