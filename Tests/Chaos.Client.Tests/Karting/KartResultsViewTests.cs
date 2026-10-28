#region
using Chaos.Client.ViewModel;
using Chaos.Networking.Entities.Server;
using FluentAssertions;
#endregion

namespace Chaos.Client.Tests.Karting;

public class KartResultsViewTests
{
    private static readonly Dictionary<byte, KartRacerInfo> Racers = new()
    {
        [0] = new KartRacerInfo(0, 5, "Alpha", 1),
        [1] = new KartRacerInfo(1, 6, "Bravo", 2)
    };

    [Test]
    public void Finishers_show_place_time_best_lap_and_medal()
    {
        var lines = KartResultsView.Lines([new KartResultEntry(1, 1, true, 95_430, 30_120), new KartResultEntry(0, 4, true, 99_000, 31_000)], Racers);

        lines[0].Should().Be(new KartResultLine("1st", "Bravo", "1:35.43", "0:30.12", 1));
        lines[1].Medal.Should().Be(0);
        lines[1].Place.Should().Be("4th");
    }

    [Test]
    public void A_dnf_row_has_no_place_or_time_and_keeps_its_order()
    {
        var lines = KartResultsView.Lines([new KartResultEntry(1, 1, true, 95_430, 30_120), new KartResultEntry(0, 2, false, 0, 41_000), new KartResultEntry(7, 3, false, 0, 0)], Racers);

        lines[1].Should().Be(new KartResultLine("-", "Alpha", "DNF", "0:41.00", 0));
        lines[2].Should().Be(new KartResultLine("-", "Racer 8", "DNF", "-", 0));
    }

    [Test]
    public void Pages_for_zero_lines_is_one()
    {
        KartResultsView.Pages(0).Should().Be(1);
    }

    [Test]
    public void Pages_for_twelve_lines_is_one()
    {
        KartResultsView.Pages(12).Should().Be(1);
    }

    [Test]
    public void Pages_for_thirteen_lines_is_two()
    {
        KartResultsView.Pages(13).Should().Be(2);
    }

    [Test]
    public void Pages_for_twenty_four_lines_is_two()
    {
        KartResultsView.Pages(24).Should().Be(2);
    }
}
