using Chaos.Client.Collections;
using Chaos.Client.Controls.World.ViewPort;
using Chaos.Client.Definitions;
using Chaos.Networking.Entities.Server;
using FluentAssertions;
using Microsoft.Xna.Framework;

namespace Chaos.Client.Tests;

public sealed class PollPanelVoteTests
{
    private static PollArgs OpenPoll(byte id)
        => new()
        {
            PollId = id,
            State = PollState.Open,
            Title = "Best snack?",
            SecondsRemaining = 30,
            WinningIndex = 0xFF,
            Options =
            [
                new PollOption { Index = 0, Text = "Ale", Votes = 3 },
                new PollOption { Index = 1, Text = "Bread", Votes = 5 }
            ]
        };

    private static ClickEvent RowClick(PollPanel panel, int row)
        => new()
        {
            Button = MouseButton.Left,
            ScreenX = panel.ScreenX + 20,
            ScreenY = panel.ScreenY + 30 + (row * 22) + 5
        };

    [Test]
    public void Clicking_the_picked_row_does_not_resend_the_vote()
    {
        var panel = new PollPanel(new Rectangle(0, 0, 640, 480));

        try
        {
            WorldState.Poll.Apply(OpenPoll(9));
            panel.Update(new GameTime(TimeSpan.Zero, TimeSpan.Zero));

            var sent = new List<(byte PollId, byte Option)>();
            panel.VoteCast += (pollId, option) => sent.Add((pollId, option));

            panel.OnClick(RowClick(panel, 0));
            sent.Should().ContainSingle().Which.Should().Be((9, (byte)0));
            WorldState.Poll.MyVoteIndex.Should().Be(0);

            panel.OnClick(RowClick(panel, 0));
            sent.Should().ContainSingle();

            panel.OnClick(RowClick(panel, 1));
            sent.Should().HaveCount(2);
            WorldState.Poll.MyVoteIndex.Should().Be(1);
        }
        finally
        {
            WorldState.Poll.Clear();
        }
    }
}
