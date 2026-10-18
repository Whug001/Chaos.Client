using Chaos.Client.ViewModel;
using Chaos.Networking.Entities.Server;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class GroupPositionsStateTests
{
    private static SetGroupPositionsArgs Args(params (string Name, ushort X, ushort Y)[] members)
        => new()
        {
            Members = members.Select(m => new GroupPositionInfo
                             {
                                 Name = m.Name,
                                 X = m.X,
                                 Y = m.Y
                             })
                             .ToList()
        };

    [Test]
    public void Set_replaces_the_whole_list()
    {
        var state = new GroupPositionsState();

        state.Set(Args(("Ann", 1, 2), ("Bo", 3, 4)));
        state.Set(Args(("Cy", 5, 6)));

        state.Members
             .Should()
             .Equal(new GroupPosition("Cy", 5, 6));
    }

    [Test]
    public void An_empty_list_clears_the_pins()
    {
        var state = new GroupPositionsState();

        state.Set(Args(("Ann", 1, 2)));
        state.Set(Args());

        state.Members
             .Should()
             .BeEmpty();
    }

    [Test]
    public void Clear_drops_every_member()
    {
        var state = new GroupPositionsState();

        state.Set(Args(("Ann", 1, 2)));
        state.Clear();

        state.Members
             .Should()
             .BeEmpty();
    }
}
