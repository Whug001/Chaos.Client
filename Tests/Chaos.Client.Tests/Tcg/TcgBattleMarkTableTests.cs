#region
using Chaos.Client.Systems;
using FluentAssertions;
#endregion

namespace Chaos.Client.Tests;

public sealed class TcgBattleMarkTableTests
{
    [Test]
    public void Set_replaces_the_last_list()
    {
        var table = new TcgBattleMarkTable();
        table.Set([1, 2]);
        table.Set([3]);

        table.Contains(1).Should().BeFalse();
        table.Contains(3).Should().BeTrue();
        table.Count.Should().Be(1);
    }

    [Test]
    public void Clear_empties_it()
    {
        var table = new TcgBattleMarkTable();
        table.Set([1]);
        table.Clear();
        table.Count.Should().Be(0);
    }
}
