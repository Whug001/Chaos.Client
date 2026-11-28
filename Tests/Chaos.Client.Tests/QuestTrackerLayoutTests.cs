using Chaos.Client.Controls.World.Hud;
using Chaos.Networking.Entities.Server;
using FluentAssertions;
using Microsoft.Xna.Framework;

namespace Chaos.Client.Tests;

public sealed class QuestTrackerLayoutTests
{
    private static int Measure(string s) => s.Length * 6;

    [Test]
    public async Task Position_is_top_right_inside_the_viewport()
    {
        var viewport = new Rectangle(0, 0, 640, 480);

        QuestTrackerLayout.Position(viewport, 0).Should().Be(new Point(452, 8));
    }

    [Test]
    public async Task Position_moves_left_of_right_docked_bars()
    {
        var viewport = new Rectangle(0, 0, 640, 480);

        QuestTrackerLayout.Position(viewport, 40).Should().Be(new Point(412, 8));
    }

    [Test]
    public async Task StageLines_wraps_and_caps_at_three_with_ellipsis()
    {
        var text = string.Join(' ', Enumerable.Repeat("aaaaaaaaa", 10));

        var lines = QuestTrackerLayout.StageLines(text, Measure, 120);

        lines.Should().HaveCount(3);
        lines[2].Should().EndWith("...");
        lines.Should().OnlyContain(line => Measure(line) <= 120);
    }

    [Test]
    public async Task StageLines_keeps_short_text_on_one_line()
    {
        var lines = QuestTrackerLayout.StageLines("Rats in the cellar", Measure, 120);

        lines.Should().ContainSingle().Which.Should().Be("Rats in the cellar");
    }

    [Test]
    public async Task ProgressLine_formats_label_have_need()
    {
        QuestTrackerLayout.ProgressLine(new QuestLogProgressInfo("Rats", 3, 5)).Should().Be("Rats: 3 / 5");
    }
}
