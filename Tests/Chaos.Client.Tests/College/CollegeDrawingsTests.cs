using Chaos.Client.Systems.College;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using FluentAssertions;

namespace Chaos.Client.Tests.College;

public class CollegeDrawingsTests
{
    private static readonly DateTime Start = new(2026, 10, 6, 12, 0, 0, DateTimeKind.Utc);

    private static CollegeDisplayArgs Drawing(int id)
        => new() { Type = CollegeDisplayType.Drawing, Id = id, Palette = new byte[96], Pixels = new byte[6912] };

    [Test]
    public void A_drawing_is_fetched_once_while_its_reply_is_due()
    {
        var drawings = new CollegeDrawings();

        drawings.NeedsFetch(4, Start).Should().BeTrue();
        drawings.NeedsFetch(4, Start.AddSeconds(29)).Should().BeFalse();
    }

    [Test]
    public void A_drawing_with_no_reply_is_fetched_again_every_thirty_seconds_up_to_ten_times()
    {
        var drawings = new CollegeDrawings();
        var sent = 0;

        for (var second = 0; second <= 600; second++)
            if (drawings.NeedsFetch(4, Start.AddSeconds(second)))
                sent++;

        sent.Should().Be(FetchRetry.MAX_ATTEMPTS);
    }

    [Test]
    public void An_arriving_drawing_is_kept_and_announced()
    {
        var drawings = new CollegeDrawings();
        var ready = new List<int>();
        drawings.DrawingReady += ready.Add;
        drawings.NeedsFetch(4, Start);

        drawings.OnDrawing(Drawing(4));

        drawings.Has(4).Should().BeTrue();
        drawings.NeedsFetch(4, Start.AddMinutes(1)).Should().BeFalse();
        ready.Should().Equal(4);
    }

    [Test]
    public void Clearing_forgets_drawings_and_requests()
    {
        var drawings = new CollegeDrawings();
        drawings.NeedsFetch(4, Start);
        drawings.OnDrawing(Drawing(5));

        drawings.Clear();

        drawings.Has(5).Should().BeFalse();
        drawings.NeedsFetch(4, Start).Should().BeTrue();
    }

    [Test]
    public void Only_the_most_recently_used_drawings_are_kept()
    {
        var drawings = new CollegeDrawings();

        for (var id = 1; id <= CollegeDrawings.KEPT_DRAWINGS + 1; id++)
            drawings.OnDrawing(Drawing(id));

        drawings.Has(1).Should().BeFalse();
        drawings.Has(2).Should().BeTrue();
        drawings.Has(CollegeDrawings.KEPT_DRAWINGS + 1).Should().BeTrue();
        drawings.NeedsFetch(1, Start).Should().BeTrue();
    }

    [Test]
    public void A_held_drawing_is_kept_until_released()
    {
        var drawings = new CollegeDrawings();
        drawings.OnDrawing(Drawing(1));
        drawings.Hold(1);

        for (var id = 2; id <= CollegeDrawings.KEPT_DRAWINGS + 2; id++)
            drawings.OnDrawing(Drawing(id));

        drawings.Has(1).Should().BeTrue();

        drawings.Release(1);
        drawings.OnDrawing(Drawing(CollegeDrawings.KEPT_DRAWINGS + 3));

        drawings.Has(1).Should().BeFalse();
    }
}
