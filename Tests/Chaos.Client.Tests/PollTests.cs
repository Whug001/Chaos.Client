using Chaos.Client.ViewModel;
using Chaos.Networking.Entities.Server;
using FluentAssertions;

namespace Chaos.Client.Tests;

/// <summary>
///     The poll view model: a new poll clears the local vote, and logging out (or switching
///     character) forgets both the poll and the vote so the next character never sees them.
/// </summary>
public class PollTests
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

    [Test]
    public void A_new_poll_clears_the_local_vote()
    {
        var poll = new Poll();
        poll.Apply(OpenPoll(1));
        poll.MyVoteIndex = 0;

        poll.Apply(OpenPoll(2));

        poll.MyVoteIndex.Should().Be(-1);
    }

    [Test]
    public void The_same_poll_keeps_the_local_vote()
    {
        var poll = new Poll();
        poll.Apply(OpenPoll(1));
        poll.MyVoteIndex = 1;

        poll.Apply(OpenPoll(1));

        poll.MyVoteIndex.Should().Be(1);
    }

    [Test]
    public void Clear_resets_the_vote_and_hides_the_poll()
    {
        var poll = new Poll();
        poll.Apply(OpenPoll(1));
        poll.MyVoteIndex = 1;
        poll.ShouldShow.Should().BeTrue();

        poll.Clear();

        poll.MyVoteIndex.Should().Be(-1);
        poll.ShouldShow.Should().BeFalse();
        poll.IsOpen.Should().BeFalse();
        poll.Options.Should().BeEmpty();
    }

    [Test]
    public void Clear_on_a_fresh_poll_is_safe()
    {
        var poll = new Poll();

        poll.Clear();

        poll.MyVoteIndex.Should().Be(-1);
        poll.ShouldShow.Should().BeFalse();
    }

    [Test]
    public void A_poll_after_a_clear_shows_again_with_no_vote()
    {
        var poll = new Poll();
        poll.Apply(OpenPoll(1));
        poll.MyVoteIndex = 0;

        poll.Clear();
        poll.Apply(OpenPoll(1));

        poll.ShouldShow.Should().BeTrue();
        poll.MyVoteIndex.Should().Be(-1);
    }
}
