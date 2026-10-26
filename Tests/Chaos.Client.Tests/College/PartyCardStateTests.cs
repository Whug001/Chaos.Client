using Chaos.Client.ViewModel.College;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using FluentAssertions;

namespace Chaos.Client.Tests.College;

public class PartyCardStateTests
{
    private static CollegePartyInfo Info(PartyPhase phase, bool canVote = false, params bool[] answered)
        => new()
        {
            Phase = phase,
            Round = 1,
            Rounds = 2,
            Item = 1,
            Items = 2,
            CanVote = canVote,
            MyPrompts = answered
                .Select((item, index) => new CollegePartyPromptInfo { Index = (byte)index, Text = $"prompt {index}", Answered = item })
                .ToList()
        };

    [Test]
    public void Writing_with_an_unanswered_prompt_shows_write()
    {
        PartyCardState.ViewOf(Info(PartyPhase.Writing, false, false)).Should().Be(PartyCardView.Write);
        PartyCardState.ViewOf(Info(PartyPhase.Writing, false, true, false)).Should().Be(PartyCardView.Write);
    }

    [Test]
    public void Writing_with_every_prompt_answered_or_none_shows_note()
    {
        PartyCardState.ViewOf(Info(PartyPhase.Writing, false, true)).Should().Be(PartyCardView.Note);
        PartyCardState.ViewOf(Info(PartyPhase.Writing)).Should().Be(PartyCardView.Note);
    }

    [Test]
    public void Drawing_with_an_unsent_prompt_shows_draw()
    {
        PartyCardState.ViewOf(Info(PartyPhase.Drawing, false, false)).Should().Be(PartyCardView.Draw);
        PartyCardState.ViewOf(Info(PartyPhase.Drawing, false, true)).Should().Be(PartyCardView.Note);
    }

    [Test]
    public void Voting_needs_can_vote()
    {
        PartyCardState.ViewOf(Info(PartyPhase.Voting, true)).Should().Be(PartyCardView.Vote);
        PartyCardState.ViewOf(Info(PartyPhase.Voting)).Should().Be(PartyCardView.Note);
    }

    [Test]
    public void Revealed_and_final_show_their_views_and_the_rest_shows_note()
    {
        PartyCardState.ViewOf(Info(PartyPhase.Revealed, true, false)).Should().Be(PartyCardView.Reveal);
        PartyCardState.ViewOf(Info(PartyPhase.Final)).Should().Be(PartyCardView.Final);
        PartyCardState.ViewOf(Info(PartyPhase.Waiting)).Should().Be(PartyCardView.Note);
        PartyCardState.ViewOf(Info(PartyPhase.Ended)).Should().Be(PartyCardView.Note);
    }

    [Test]
    public void The_shown_prompt_starts_on_the_first_unanswered_one()
    {
        var state = new PartyCardState();
        state.Apply(Info(PartyPhase.Writing, false, true, false));

        state.Shown.Should().Be(1);
        state.ShownPrompt!.Text.Should().Be("prompt 1");
        ClassToolText.PromptCounter(state.Shown + 1, state.Info.MyPrompts.Count).Should().Be("Prompt 2 of 2");
    }

    [Test]
    public void A_send_moves_to_the_next_unanswered_prompt()
    {
        var state = new PartyCardState();
        state.Apply(Info(PartyPhase.Writing, false, false, false));

        state.NoteSent();

        state.Shown.Should().Be(1);
        state.View.Should().Be(PartyCardView.Write);
    }

    [Test]
    public void The_echo_of_a_send_keeps_the_new_prompt()
    {
        var state = new PartyCardState();
        state.Apply(Info(PartyPhase.Writing, false, false, false));
        state.NoteSent();

        state.Apply(Info(PartyPhase.Writing, false, true, false));

        state.Shown.Should().Be(1);
        state.ShownPrompt!.Text.Should().Be("prompt 1");
    }

    [Test]
    public void A_new_step_resets_to_the_first_unanswered_prompt()
    {
        var state = new PartyCardState();
        state.Apply(Info(PartyPhase.Writing, false, false, false));
        state.NoteSent();
        state.Shown.Should().Be(1);

        var next = Info(PartyPhase.Writing, false, false, false);
        next.Item = 2;
        state.Apply(next);

        state.Shown.Should().Be(0);
        ClassToolText.PromptCounter(state.Shown + 1, state.Info.MyPrompts.Count).Should().Be("Prompt 1 of 2");
    }
}
