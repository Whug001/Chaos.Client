using Chaos.CannonFair;
using Chaos.Client.Controls.World.Popups.CannonFair;
using FluentAssertions;

namespace Chaos.Client.Tests.CannonFair;

public sealed class CannonFairCameraTests
{
    [Test]
    public void Wide_view_fits_the_whole_field()
    {
        CannonFairCamera.Project(0f, 0f, 0f).Should().Be((0f, 400f, 16f));
        CannonFairCamera.Project(40f, 0f, 0f).Should().Be((640f, 400f, 16f));
    }

    [Test]
    public void Full_zoom_centres_the_fort()
    {
        var (x, y, _) = CannonFairCamera.Project(32f, -3.5f, 1f);

        x.Should().Be(320f);
        y.Should().Be(240f);
    }

    [Test]
    public void Impact_time_leads_the_first_event()
    {
        var volley = new VolleyResult([], [], [new ScoreEvent(2000, 1, 10, 0f, 0f)], [new BlastEvent(1500, 0f, 0f, 1f)], false);

        CannonFairCamera.ImpactMs(volley).Should().Be(1200);
    }

    [Test]
    public void A_volley_with_no_impact_stays_wide()
    {
        var impact = CannonFairCamera.ImpactMs(new VolleyResult([], [], [], [], false));

        impact.Should().Be(-1);
        CannonFairCamera.ZoomAt(5000, impact).Should().Be(0f);
    }

    [Test]
    public void Zoom_runs_from_wide_to_full()
    {
        CannonFairCamera.ZoomAt(1000, 1000).Should().Be(0f);
        CannonFairCamera.ZoomAt(1800, 1000).Should().Be(1f);
        CannonFairCamera.ZoomAt(9000, 1000).Should().Be(1f);
    }
}
