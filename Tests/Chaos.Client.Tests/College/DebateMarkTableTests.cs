using Chaos.Client.Systems.College;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using FluentAssertions;

namespace Chaos.Client.Tests.College;

public class DebateMarkTableTests
{
    [Test]
    public void Set_replaces_every_marker_and_skips_players_with_no_side()
    {
        var table = new DebateMarkTable();
        table.Set([new CollegeDebateMarkInfo { EntityId = 9, Side = DebateSide.For }]);

        table.Set(
            [
                new CollegeDebateMarkInfo { EntityId = 1, Side = DebateSide.Against, Floor = true },
                new CollegeDebateMarkInfo { EntityId = 2, Side = DebateSide.None }
            ]);

        table.All.Keys.Should().Equal(1u);
        table.All[1].Floor.Should().BeTrue();
        table.Count.Should().Be(1);
    }

    [Test]
    public void Clear_empties_it()
    {
        var table = new DebateMarkTable();
        table.Set([new CollegeDebateMarkInfo { EntityId = 1, Side = DebateSide.For }]);

        table.Clear();

        table.Count.Should().Be(0);
    }
}
