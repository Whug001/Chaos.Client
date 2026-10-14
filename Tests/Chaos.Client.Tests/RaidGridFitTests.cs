using Chaos.Client.Controls.World.Popups.GroupPanels;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class RaidGridFitTests
{
    [Test]
    public void Fit_KeepsOnlyPartiesWithMembers()
    {
        var (parties, rows) = RaidGrid.Fit([6, 3, 0, 0]);

        parties.Should().Equal(0, 1);
        rows.Should().Be(6);
    }

    [Test]
    public void Fit_SkipsAnEmptyPartyBetweenOthers()
    {
        var (parties, rows) = RaidGrid.Fit([2, 0, 4, 0]);

        parties.Should().Equal(0, 2);
        rows.Should().Be(4);
    }

    [Test]
    public void Fit_FullRaidKeepsEveryParty()
    {
        var (parties, rows) = RaidGrid.Fit([6, 6, 6, 6]);

        parties.Should().Equal(0, 1, 2, 3);
        rows.Should().Be(6);
    }

    [Test]
    public void Fit_EmptyRosterShowsNothing()
    {
        var (parties, rows) = RaidGrid.Fit([0, 0, 0, 0]);

        parties.Should().BeEmpty();
        rows.Should().Be(0);
    }
}
