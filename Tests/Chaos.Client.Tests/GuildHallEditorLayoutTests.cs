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
    public async Task Every_tab_icon_is_an_embedded_asset()
    {
        GuildHallEditorControl.TabIconKeys.Should().HaveCount(GuildHallEditorControl.TabNames.Length);

        foreach (var key in GuildHallEditorControl.TabIconKeys)
        {
            var open = () => Chaos.Client.Rendering.GuildHallIconAssets.Open(Chaos.Client.Rendering.GuildHallIconAssets.ResourceName(key)).Dispose();
            open.Should().NotThrow(key);
        }

        await Task.CompletedTask;
    }

    [Test]
    public async Task Tabs_use_the_short_names()
    {
        GuildHallEditorControl.TabNames.Should().Equal("Floor", "Walls", "Decor", "NPCs", "Prefab", "Siege");

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
        var last = GuildHallEditorControl.TabNames.Length - 1;
        var lastRight = GuildHallEditorControl.TabX(last) + GuildHallEditorControl.TabWidths[last];

        lastRight.Should().Be(GuildHallEditorControl.PANEL_WIDTH - GuildHallEditorControl.PAD);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Six_tabs_fit_the_panel()
    {
        Enum.GetValues<GuildHallTab>().Should().HaveCount(6);
        GuildHallEditorControl.TabWidths.Should().HaveCount(6);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Every_tab_caption_fits_its_button()
    {
        for (var i = 0; i < GuildHallEditorControl.TabNames.Length; i++)
        {
            var name = GuildHallEditorControl.TabNames[i];

            Chaos.Client.Rendering.TextRenderer.MeasureWidth(name).Should().BeLessThanOrEqualTo(GuildHallEditorControl.TabWidths[i] - 4, name);
        }

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
