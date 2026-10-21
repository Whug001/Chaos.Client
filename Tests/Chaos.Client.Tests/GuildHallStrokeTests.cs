using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
using FluentAssertions;
using Microsoft.Xna.Framework;

namespace Chaos.Client.Tests;

public sealed class GuildHallStrokeTests
{
    [Test]
    public async Task Line_steps_every_tile_without_the_start()
    {
        GuildHallStroke.Line(new Point(2, 2), new Point(5, 2)).Should().Equal(new Point(3, 2), new Point(4, 2), new Point(5, 2));
        GuildHallStroke.Line(new Point(0, 0), new Point(2, 2)).Should().Equal(new Point(1, 1), new Point(2, 2));
        GuildHallStroke.Line(new Point(4, 4), new Point(4, 4)).Should().BeEmpty();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Line_has_no_gaps_on_a_long_jump()
    {
        var from = new Point(10, 3);
        var line = GuildHallStroke.Line(from, new Point(1, 7));

        line[^1].Should().Be(new Point(1, 7));
        line.Should().OnlyHaveUniqueItems();
        var previous = from;

        foreach (var step in line)
        {
            Math.Max(Math.Abs(step.X - previous.X), Math.Abs(step.Y - previous.Y)).Should().Be(1);
            previous = step;
        }

        await Task.CompletedTask;
    }

    [Test]
    public async Task Normalize_orders_corners()
    {
        GuildHallStroke.Normalize(new Point(9, 2), new Point(3, 7)).Should().Be((new Point(3, 2), new Point(9, 7)));

        await Task.CompletedTask;
    }

    [Test]
    public async Task Perimeter_lists_each_edge_tile_once()
    {
        GuildHallStroke.Perimeter(new Point(4, 4), new Point(4, 4)).Should().Equal(new Point(4, 4));
        GuildHallStroke.Perimeter(new Point(2, 2), new Point(0, 0)).Should().HaveCount(8).And.OnlyHaveUniqueItems().And.NotContain(new Point(1, 1));
        GuildHallStroke.Perimeter(new Point(0, 0), new Point(0, 3)).Should().HaveCount(4).And.OnlyHaveUniqueItems();

        await Task.CompletedTask;
    }

    [Test]
    public async Task RectArgs_sends_normalized_corners_with_the_selection()
    {
        var state = new GuildHallEditorState();
        state.Open(new GuildHallEditorDisplayArgs
        {
            Kind = GuildHallEditorDisplayKind.Open,
            Sets = [new GuildHallPaletteSet("free", "Free set", true, 0, [11], [], [])],
            EntranceX = 98,
            EntranceY = 46
        });
        state.SetTool(GuildHallTool.Rect);

        var args = state.RectArgs(new Point(9, 8), new Point(2, 3))!;

        args.Action.Should().Be(GuildHallEditorAction.Rect);
        args.Background.Should().Be(11);
        (args.X, args.Y, args.X2, args.Y2).Should().Be(((byte)2, (byte)3, (byte)9, (byte)8));

        await Task.CompletedTask;
    }
}
