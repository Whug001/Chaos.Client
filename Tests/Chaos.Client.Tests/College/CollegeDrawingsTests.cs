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
}
