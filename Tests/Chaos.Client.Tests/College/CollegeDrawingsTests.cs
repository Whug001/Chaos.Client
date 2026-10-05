using Chaos.Client.Systems.College;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using FluentAssertions;

namespace Chaos.Client.Tests.College;

public class CollegeDrawingsTests
{
    private static CollegeDisplayArgs Drawing(int id)
        => new() { Type = CollegeDisplayType.Drawing, Id = id, Palette = new byte[96], Pixels = new byte[6912] };

    [Test]
    public void Each_drawing_is_fetched_once()
    {
        var drawings = new CollegeDrawings();

        drawings.NeedsFetch(4).Should().BeTrue();
        drawings.NeedsFetch(4).Should().BeFalse();
    }

    [Test]
    public void An_arriving_drawing_is_kept_and_announced()
    {
        var drawings = new CollegeDrawings();
        var ready = new List<int>();
        drawings.DrawingReady += ready.Add;
        drawings.NeedsFetch(4);

        drawings.OnDrawing(Drawing(4));

        drawings.Has(4).Should().BeTrue();
        drawings.NeedsFetch(4).Should().BeFalse();
        ready.Should().Equal(4);
    }

    [Test]
    public void Clearing_forgets_drawings_and_requests()
    {
        var drawings = new CollegeDrawings();
        drawings.NeedsFetch(4);
        drawings.OnDrawing(Drawing(5));

        drawings.Clear();

        drawings.Has(5).Should().BeFalse();
        drawings.NeedsFetch(4).Should().BeTrue();
    }
}
