#region
using Chaos.Client.Controls.World.Popups.Tcg.Match;
using FluentAssertions;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Tests;

public sealed class TcgMatchLayoutTests
{
    [Test]
    public void Centres_a_single_creature_on_the_battlefield()
        => TcgMatchLayout.CreatureSlot(0, 1, true).Should().Be(new Point(235, 316));

    [Test]
    public void Spaces_creatures_sixty_four_apart()
    {
        TcgMatchLayout.CreatureSlot(0, 2, false).Should().Be(new Point(203, 176));
        TcgMatchLayout.CreatureSlot(1, 2, false).Should().Be(new Point(267, 176));
    }

    [Test]
    public void Shrinks_the_step_for_eight_or_more()
    {
        var first = TcgMatchLayout.CreatureSlot(0, 8, true);
        var second = TcgMatchLayout.CreatureSlot(1, 8, true);

        (second.X - first.X).Should().Be(50);
    }

    [Test]
    public void Keeps_the_board_and_column_side_by_side()
    {
        TcgMatchLayout.Board.Right.Should().Be(TcgMatchLayout.Column.Left);
        TcgMatchLayout.Column.Right.Should().Be(640);
    }
}
