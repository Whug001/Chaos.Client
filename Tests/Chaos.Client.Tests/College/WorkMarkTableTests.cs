using Chaos.Client.Systems.College;
using Chaos.Networking.Entities.Server;
using FluentAssertions;

namespace Chaos.Client.Tests.College;

public class WorkMarkTableTests
{
    [Test]
    public void Set_replaces_every_mark_and_skips_empty_text()
    {
        var table = new WorkMarkTable();
        table.Set([new CollegeWorkMarkInfo { EntityId = 9, Text = "Drawing" }]);

        table.Set(
            [
                new CollegeWorkMarkInfo { EntityId = 1, Text = "Drawing" },
                new CollegeWorkMarkInfo { EntityId = 2, Text = "" }
            ]);

        table.All.Keys.Should().Equal(1u);
        table.All[1].Should().Be("Drawing");
        table.Count.Should().Be(1);
    }

    [Test]
    public void Clear_empties_it()
    {
        var table = new WorkMarkTable();
        table.Set([new CollegeWorkMarkInfo { EntityId = 1, Text = "Drawing" }]);

        table.Clear();

        table.Count.Should().Be(0);
    }
}
