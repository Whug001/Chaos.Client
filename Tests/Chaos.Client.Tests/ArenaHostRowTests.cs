using Chaos.Client.Controls.World.Popups.ArenaHost;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using FluentAssertions;
using Microsoft.Xna.Framework;

namespace Chaos.Client.Tests;

public sealed class ArenaHostRowTests
{
    private static string Shown(string text)
    {
        var row = new ArenaHostRow(150);
        row.Show(text, Color.White);

        return row.Text;
    }

    [Test]
    public void A_mode_row_shows_the_mode_name()
        => Shown(ArenaHostRow.ModeText(new ArenaHostModeInfo { Id = 9, Name = "Paintball" }, false, false)).Should().Be("Paintball");

    [Test]
    public void The_vote_winner_is_starred()
        => Shown(ArenaHostRow.ModeText(new ArenaHostModeInfo { Name = "Paintball", IsVoteWinner = true }, false, false))
            .Should()
            .Be("* Paintball");

    //formatter:off
    [Test]
    [Arguments(true, "[x] Wolf Hunt")]
    [Arguments(false, "[ ] Wolf Hunt")]
    //formatter:on
    public void In_vote_mode_a_mode_row_shows_its_tick_box(bool picked, string expected)
        => Shown(ArenaHostRow.ModeText(new ArenaHostModeInfo { Name = "Wolf Hunt" }, true, picked)).Should().Be(expected);

    [Test]
    public void A_roster_row_marks_the_host_and_a_captain()
        => Shown(ArenaHostRow.RosterText(new ArenaHostRosterInfo { Name = "Ivy", Team = ArenaHostTeam.Green, IsHost = true, IsCaptain = true }))
            .Should()
            .Be("Ivy (host) (captain)");

    [Test]
    public void A_plain_roster_row_shows_only_the_name()
        => Shown(ArenaHostRow.RosterText(new ArenaHostRosterInfo { Name = "Ivy" })).Should().Be("Ivy");
}
