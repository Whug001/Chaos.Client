#region
using Chaos.Client.Controls.World.Popups.Tcg.Match;
using Chaos.DarkAges.Definitions;
using FluentAssertions;
using static Chaos.Client.Tests.TcgMatchFixtures;
#endregion

namespace Chaos.Client.Tests;

public sealed class TcgMatchManaPickTests
{
    [Test]
    public void Gem_under_the_point_is_found()
    {
        var board = Board(mana: [Mana(1, TcgElement.Fire), Mana(2, TcgElement.Water)]);
        var second = TcgMatchLayout.Gem(TcgMatchLayout.YourGems, 1);

        TcgMatchWindow.GemIdAt(board, second.X + 8, second.Y + 8).Should().Be(2);
        TcgMatchWindow.GemIdAt(board, second.X + 17, second.Y + 8).Should().Be(0);
        TcgMatchWindow.GemIdAt(board, 0, 0).Should().Be(0);
    }
}
