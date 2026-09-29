using Chaos.Client.Systems;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using FluentAssertions;

namespace Chaos.Client.Tests;

public sealed class TownImportTextTests
{
    [Test]
    public void Time_left_uses_the_two_largest_units()
    {
        var now = new DateTime(2026, 10, 3, 12, 0, 0, DateTimeKind.Utc);

        TownImportText.TimeLeft(now.AddDays(6).AddHours(23).AddMinutes(5), now).Should().Be("6d 23h");
        TownImportText.TimeLeft(now.AddHours(5).AddMinutes(10), now).Should().Be("5h 10m");
        TownImportText.TimeLeft(now.AddSeconds(20), now).Should().Be("under a minute");
        TownImportText.TimeLeft(now.AddSeconds(-5), now).Should().Be("under a minute");
    }

    [Test]
    public void Past_lines_name_how_the_import_ended()
    {
        var past = new TownImportPastInfo { Name = "Rose Crown", Sold = 12, Gold = 1_800_000, Reason = TownImportEndReason.Vetoed };

        TownImportText.PastLine(past).Should().Be("Rose Crown: 12 sold, 1,800,000 gold (vetoed)");
        TownImportText.PastLine(past with { Reason = TownImportEndReason.Finished }).Should().Be("Rose Crown: 12 sold, 1,800,000 gold");
        TownImportText.PastLine(past with { Reason = TownImportEndReason.EndedByAdmin }).Should().Be("Rose Crown: 12 sold, 1,800,000 gold (ended early)");
    }
}
