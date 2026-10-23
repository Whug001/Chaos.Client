using Chaos.Client.Controls.World.Popups.HallBattle;
using FluentAssertions;
using Microsoft.Xna.Framework;

namespace Chaos.Client.Tests;

public sealed class WarRoomLayoutTests
{
    private static readonly Rectangle Screen = new(0, 0, 640, 480);

    [Test]
    [Arguments(WarRoomPage.Board)]
    [Arguments(WarRoomPage.Team)]
    public void Every_control_fits_inside_640_by_480(WarRoomPage page)
    {
        var origin = new Point((Screen.Width - WarRoomLayout.WIDTH) / 2, (Screen.Height - WarRoomLayout.HEIGHT) / 2);

        foreach (var rect in WarRoomLayout.VisibleBounds(page))
        {
            var onScreen = new Rectangle(origin.X + rect.X, origin.Y + rect.Y, rect.Width, rect.Height);

            Screen.Contains(onScreen).Should().BeTrue($"{page} control {rect} should lie inside the screen");
        }
    }

    [Test]
    [Arguments(WarRoomPage.Board)]
    [Arguments(WarRoomPage.Team)]
    public void Every_control_fits_inside_the_window(WarRoomPage page)
    {
        var window = new Rectangle(0, 0, WarRoomLayout.WIDTH, WarRoomLayout.HEIGHT);

        foreach (var rect in WarRoomLayout.VisibleBounds(page))
            window.Contains(rect).Should().BeTrue($"{page} control {rect} should lie inside the window");
    }

    [Test]
    public void The_team_page_reuses_the_list_paging_controls()
    {
        var bounds = WarRoomLayout.VisibleBounds(WarRoomPage.Team);

        bounds.Should().Contain(WarRoomLayout.ListPrev);
        bounds.Should().Contain(WarRoomLayout.ListNext);
        bounds.Should().Contain(WarRoomLayout.ListPageLabel);
    }

    [Test]
    public void The_list_and_the_detail_do_not_overlap()
    {
        var list = new Rectangle(10, 30, 230, WarRoomLayout.LIST_ROWS * WarRoomLayout.ROW);

        list.Right.Should().BeLessThan(WarRoomLayout.DetailName.X);
    }
}
