using Chaos.Client.Controls.World.Popups;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class EmoteWheelClampTests
{
    private const int WHEEL = 260;
    private const int CATALOG_W = 380;
    private const int CATALOG_H = 328;

    [Test]
    public async Task Centre_unchanged_when_cursor_clear_of_edges()
    {
        EmoteWheelControl.ClampCenter(320, 240, WHEEL, WHEEL).Should().Be((320, 240));
        EmoteWheelControl.ClampCenter(320, 240, CATALOG_W, CATALOG_H).Should().Be((320, 240));
        await Task.CompletedTask;
    }

    [Test]
    public async Task Top_left_cursor_clamps_to_half_panel()
    {
        EmoteWheelControl.ClampCenter(0, 0, WHEEL, WHEEL).Should().Be((WHEEL / 2, WHEEL / 2));
        EmoteWheelControl.ClampCenter(0, 0, CATALOG_W, CATALOG_H).Should().Be((CATALOG_W / 2, CATALOG_H / 2));
        await Task.CompletedTask;
    }

    [Test]
    public async Task Bottom_right_cursor_clamps_to_screen_minus_half_panel()
    {
        EmoteWheelControl.ClampCenter(639, 479, WHEEL, WHEEL)
            .Should().Be((ChaosGame.VIRTUAL_WIDTH - WHEEL / 2, ChaosGame.VIRTUAL_HEIGHT - WHEEL / 2));
        EmoteWheelControl.ClampCenter(639, 479, CATALOG_W, CATALOG_H)
            .Should().Be((ChaosGame.VIRTUAL_WIDTH - CATALOG_W / 2, ChaosGame.VIRTUAL_HEIGHT - CATALOG_H / 2));
        await Task.CompletedTask;
    }

    [Test]
    public async Task Clamp_keeps_centre_as_close_to_cursor_as_possible()
    {
        //just past the right edge: centre stops at the limit instead of following the cursor
        EmoteWheelControl.ClampCenter(600, 240, WHEEL, WHEEL).Should().Be((510, 240));

        //inside the valid band on one axis: that axis is untouched while the other clamps
        EmoteWheelControl.ClampCenter(600, 10, WHEEL, WHEEL).Should().Be((510, WHEEL / 2));
        await Task.CompletedTask;
    }

    [Test]
    public async Task Clamped_panel_stays_fully_on_screen_for_every_cursor()
    {
        foreach (var (w, h) in new[] { (WHEEL, WHEEL), (CATALOG_W, CATALOG_H) })
            for (var x = 0; x < ChaosGame.VIRTUAL_WIDTH; x += 37)
                for (var y = 0; y < ChaosGame.VIRTUAL_HEIGHT; y += 29)
                {
                    var (cx, cy) = EmoteWheelControl.ClampCenter(x, y, w, h);

                    (cx - w / 2).Should().BeGreaterThanOrEqualTo(0);
                    (cy - h / 2).Should().BeGreaterThanOrEqualTo(0);
                    (cx + w / 2).Should().BeLessThanOrEqualTo(ChaosGame.VIRTUAL_WIDTH);
                    (cy + h / 2).Should().BeLessThanOrEqualTo(ChaosGame.VIRTUAL_HEIGHT);
                }

        await Task.CompletedTask;
    }
}
