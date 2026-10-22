using Chaos.Client.Controls.World.Popups.GuildHall;
using FluentAssertions;

namespace Chaos.Client.Tests;

public sealed class GuildHallEditorStatusTests
{
    [Test]
    public async Task Short_lines_are_shown_whole()
    {
        GuildHallEditorControl.ShortenStatus("Click or drag to paint.").Should().Be("Click or drag to paint.");

        await Task.CompletedTask;
    }

    [Test]
    public async Task Long_reasons_are_truncated_with_an_ellipsis()
    {
        var shortened = GuildHallEditorControl.ShortenStatus("That area is too big to fill. A fill can change up to 2,500 tiles.");

        shortened.Should().HaveLength(30);
        shortened.Should().EndWith("...");
        "That area is too big to fill. A fill can change up to 2,500 tiles.".Should().StartWith(shortened[..27]);

        await Task.CompletedTask;
    }
}
