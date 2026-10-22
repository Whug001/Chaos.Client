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
        (GuildHallEditorControl.GRID_Y + gridHeight).Should().BeLessThanOrEqualTo(GuildHallEditorControl.TOOL_LABEL_Y);

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
    public async Task Prefab_tab_is_called_rooms()
    {
        GuildHallEditorControl.TabNames.Should().Equal("Floors", "Walls", "Decor", "NPCs", "Rooms");

        await Task.CompletedTask;
    }

    [Test]
    public async Task Tool_buttons_use_the_new_names()
    {
        GuildHallEditorControl.ToolNames.Should().Equal("Paint", "Box", "Fill", "Erase", "Door");

        await Task.CompletedTask;
    }

    [Test]
    public async Task Tab_buttons_fit_in_the_row()
    {
        var lastRight = GuildHallEditorControl.PAD + (4 * GuildHallEditorControl.TAB_PITCH) + GuildHallEditorControl.TAB_WIDTH;

        lastRight.Should().BeLessThanOrEqualTo(GuildHallEditorControl.PANEL_WIDTH - GuildHallEditorControl.PAD);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Tool_buttons_fit_in_the_row()
    {
        var lastRight = GuildHallEditorControl.PAD + (4 * GuildHallEditorControl.TOOL_PITCH) + GuildHallEditorControl.TOOL_WIDTH;

        lastRight.Should().BeLessThanOrEqualTo(GuildHallEditorControl.PANEL_WIDTH - GuildHallEditorControl.PAD);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Side_buttons_fit_in_the_row()
    {
        var lastRight = GuildHallEditorControl.PAD + (2 * GuildHallEditorControl.SIDE_PITCH) + GuildHallEditorControl.SIDE_WIDTH;

        lastRight.Should().BeLessThanOrEqualTo(GuildHallEditorControl.PANEL_WIDTH - GuildHallEditorControl.PAD);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Every_visible_control_fits_inside_the_panel_on_every_tab()
    {
        foreach (var tab in Enum.GetValues<GuildHallTab>())
        {
            var bounds = GuildHallEditorControl.VisibleBounds(tab);

            bounds.Should().NotBeEmpty($"tab {tab} should show some controls");

            foreach (var rect in bounds)
            {
                rect.X.Should().BeGreaterThanOrEqualTo(0, $"tab {tab} control at {rect} hangs off the left");
                rect.Y.Should().BeGreaterThanOrEqualTo(0, $"tab {tab} control at {rect} hangs off the top");
                rect.Right.Should().BeLessThanOrEqualTo(
                    GuildHallEditorControl.PANEL_WIDTH,
                    $"tab {tab} control at {rect} hangs off the right");
                rect.Bottom.Should().BeLessThanOrEqualTo(
                    GuildHallEditorControl.PANEL_HEIGHT,
                    $"tab {tab} control at {rect} hangs off the bottom");
            }
        }

        await Task.CompletedTask;
    }
}
