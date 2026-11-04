using Chaos.Client.Controls.LobbyLogin;
using FluentAssertions;
using Microsoft.Xna.Framework;

namespace Chaos.Client.Tests;

public sealed class ServerListLayoutTests
{
    private static readonly Rectangle NAME = new(21, 50, 70, 12);
    private static readonly Rectangle DESCRIPTION = new(102, 50, 286, 12);

    [Test]
    [Arguments(0, 1)]
    [Arguments(1, 1)]
    [Arguments(5, 1)]
    [Arguments(6, 1)]
    [Arguments(7, 2)]
    [Arguments(10, 5)]
    public void Strip_count_never_drops_below_one(int servers, int strips)
        => ServerListLayout.StripCount(servers).Should().Be(strips);

    [Test]
    public void Box_height_adds_the_pieces()
    {
        ServerListLayout.BoxHeight(146, 20, 17, 1).Should().Be(183);
        ServerListLayout.BoxHeight(146, 20, 17, 5).Should().Be(263);
    }

    [Test]
    public void Box_is_centered_on_the_screen()
        => ServerListLayout.BoxOrigin(640, 480, 413, 183).Should().Be(new Point(113, 148));

    [Test]
    public void Rows_move_down_by_the_pitch()
    {
        ServerListLayout.Row(NAME, 20, 0).Should().Be(NAME);
        ServerListLayout.Row(NAME, 20, 5).Should().Be(new Rectangle(21, 150, 70, 12));
    }

    [Test]
    public void Hit_row_spans_name_to_description_and_one_pitch_high()
    {
        var row = ServerListLayout.HitRow(NAME, DESCRIPTION, 20, 0);

        row.Should().Be(new Rectangle(21, 50, 367, 20));
        row.Contains(21, 69).Should().BeTrue();
        row.Contains(21, 70).Should().BeFalse();
        row.Contains(20, 55).Should().BeFalse();
        row.Contains(388, 55).Should().BeFalse();
        ServerListLayout.HitRow(NAME, DESCRIPTION, 20, 5).Should().Be(new Rectangle(21, 150, 367, 20));
    }
}
