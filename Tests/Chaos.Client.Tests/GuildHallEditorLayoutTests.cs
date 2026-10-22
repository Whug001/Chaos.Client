using Chaos.Client.Controls.World.Popups.Beauty;
using Chaos.Client.Controls.World.Popups.GuildHall;
using Chaos.Client.ViewModel;
using FluentAssertions;
using Microsoft.Xna.Framework;

namespace Chaos.Client.Tests;

public sealed class GuildHallEditorLayoutTests
{
    [Test]
    public async Task Grid_fits_inside_the_panel()
    {
        var gridWidth = ThumbnailGrid<GuildHallPick>.WidthFor(GuildHallEditorState.COLUMNS);
        var gridHeight = ThumbnailGrid<GuildHallPick>.HeightFor(GuildHallEditorState.ROWS);

        gridWidth.Should().Be(210);
        (GuildHallEditorControl.PAD + gridWidth).Should().BeLessThanOrEqualTo(GuildHallEditorControl.PANEL_WIDTH - GuildHallEditorControl.PAD);
        (GuildHallEditorControl.GRID_Y + gridHeight).Should().BeLessThan(GuildHallEditorControl.STATUS_Y);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Docks_to_the_viewport_right_edge_and_top()
    {
        GuildHallEditorControl.DockOrigin(new Rectangle(0, 0, 640, 360))
                              .Should()
                              .Be(new Point(640 - GuildHallEditorControl.PANEL_WIDTH - 2, 2));

        await Task.CompletedTask;
    }

    [Test]
    public async Task Dock_stays_on_screen_for_a_short_viewport()
    {
        var origin = GuildHallEditorControl.DockOrigin(new Rectangle(0, 300, 640, 100));

        (origin.Y + GuildHallEditorControl.PANEL_HEIGHT).Should().BeLessThanOrEqualTo(ChaosGame.VIRTUAL_HEIGHT);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Prefab_tab_buttons_fit_in_the_row()
    {
        var lastRight = GuildHallEditorControl.PAD + (4 * GuildHallEditorControl.TAB_PITCH) + GuildHallEditorControl.TAB_WIDTH;

        lastRight.Should().BeLessThanOrEqualTo(GuildHallEditorControl.PANEL_WIDTH - GuildHallEditorControl.PAD);

        await Task.CompletedTask;
    }
}
