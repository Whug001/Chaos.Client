using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class TownBallotStateTests
{
    private static TownBallotArgs Ballot(TownBallotStage stage, bool canVote, string myVote, params string[] names)
        => new()
        {
            Type = TownBallotType.Open,
            TownKey = "mileth",
            TownName = "Mileth",
            Stage = stage,
            CanVote = canVote,
            MyVote = myVote,
            Candidates = names.Select(name => new TownBallotCandidateInfo { Name = name, IsViewer = name == "Me" }).ToList()
        };

    [Test]
    public async Task Open_selects_the_first_candidate_on_the_first_page()
    {
        var state = new TownBallotState();
        state.Open(Ballot(TownBallotStage.Voting, true, "", "A", "B"));

        state.Selected!.Name.Should().Be("A");
        state.Page.Should().Be(0);
        await Task.CompletedTask;
    }

    [Test]
    public async Task An_update_keeps_the_selection_while_the_name_is_listed()
    {
        var state = new TownBallotState();
        state.Open(Ballot(TownBallotStage.Voting, true, "", "A", "B"));
        state.Select("B");

        state.Apply(Ballot(TownBallotStage.Voting, true, "B", "A", "B"));
        state.Selected!.Name.Should().Be("B");

        state.Apply(Ballot(TownBallotStage.Voting, true, "", "A"));
        state.Selected!.Name.Should().Be("A");
        await Task.CompletedTask;
    }

    [Test]
    public async Task Pages_hold_eight()
    {
        var state = new TownBallotState();
        state.Open(Ballot(TownBallotStage.Voting, true, "", Enumerable.Range(0, 10).Select(i => $"C{i}").ToArray()));

        state.Pages.Should().Be(2);
        state.PageItems.Should().HaveCount(8);
        state.TurnPage(1);
        state.PageItems.Select(c => c.Name).Should().Equal("C8", "C9");
        state.TurnPage(5);
        state.Page.Should().Be(1);
        await Task.CompletedTask;
    }

    [Test]
    public async Task Voting_takes_two_clicks()
    {
        var state = new TownBallotState();
        state.Open(Ballot(TownBallotStage.Voting, true, "", "A"));

        state.VoteCaption.Should().Be("Vote for A");
        state.ClickVote().Should().BeNull();
        state.VoteCaption.Should().Be("Confirm: A?");

        var vote = state.ClickVote()!;
        vote.Action.Should().Be(TownBallotAction.Vote);
        vote.Candidate.Should().Be("A");
        vote.TownKey.Should().Be("mileth");
        await Task.CompletedTask;
    }

    [Test]
    public async Task The_current_vote_shows_as_your_vote_and_cannot_be_clicked()
    {
        var state = new TownBallotState();
        state.Open(Ballot(TownBallotStage.Voting, true, "A", "A", "B"));

        state.ShowsVoteButton.Should().BeTrue();
        state.VoteEnabled.Should().BeFalse();
        state.VoteCaption.Should().Be("Your vote");
        state.ClickVote().Should().BeNull();
        await Task.CompletedTask;
    }

    [Test]
    public async Task No_vote_button_without_the_right_to_vote_or_outside_voting()
    {
        var state = new TownBallotState();
        state.Open(Ballot(TownBallotStage.Voting, false, "", "A"));
        state.ShowsVoteButton.Should().BeFalse();

        state.Open(Ballot(TownBallotStage.Candidacy, false, "", "A"));
        state.ShowsVoteButton.Should().BeFalse();
        await Task.CompletedTask;
    }

    [Test]
    public async Task Only_the_candidate_edits_their_own_statement_during_candidacy()
    {
        var state = new TownBallotState();
        state.Open(Ballot(TownBallotStage.Candidacy, false, "", "A", "Me"));
        state.Select("A");

        state.ShowsEditButton.Should().BeFalse();
        state.Select("Me");
        state.ShowsEditButton.Should().BeTrue();

        state.Open(Ballot(TownBallotStage.Voting, true, "", "Me"));
        state.ShowsEditButton.Should().BeFalse();
        await Task.CompletedTask;
    }

    [Test]
    public async Task Saving_needs_one_to_four_hundred_characters()
    {
        var state = new TownBallotState();
        state.Open(Ballot(TownBallotStage.Candidacy, false, "", "Me"));
        state.StartEdit();

        state.Save("   ").Should().BeNull();
        state.Save(new string('a', TownBallotProtocol.MAX_STATEMENT_CHARS + 1)).Should().BeNull();

        var save = state.Save("  A plan  ")!;
        save.Action.Should().Be(TownBallotAction.SaveStatement);
        save.Statement.Should().Be("A plan");
        await Task.CompletedTask;
    }

    [Test]
    public async Task Term_and_detail_lines_read_plainly()
    {
        TownBallotState.TermsLine(new TownBallotCandidateInfo()).Should().Be("First run");
        TownBallotState.TermsLine(new TownBallotCandidateInfo { MayorTerms = 1, CouncilTerms = 2 }).Should().Be("Mayor once, councillor twice");
        TownBallotState.TermsLine(new TownBallotCandidateInfo { CouncilTerms = 3 }).Should().Be("Councillor 3 times");
        TownBallotState.ClassLine(new TownBallotCandidateInfo { Level = 99, ClassName = "Warrior" }).Should().Be("Level 99 Warrior");
        TownBallotState.ClassLine(new TownBallotCandidateInfo()).Should().Be("Not seen yet");
        TownBallotState.CitizenLine(new TownBallotCandidateInfo { CitizenSinceUtc = new DateTime(2026, 6, 2, 0, 0, 0, DateTimeKind.Utc) })
                      .Should().Be("Citizen since 2026-06-02");
        await Task.CompletedTask;
    }

    private static TownBallotArgs WithStatement(string statement)
    {
        var args = Ballot(TownBallotStage.Candidacy, false, "", "A", "Me");
        args.Candidates[1].Statement = statement;

        return args;
    }

    [Test]
    public async Task A_refused_save_keeps_the_draft_open()
    {
        var state = new TownBallotState();
        state.Open(WithStatement("old"));
        state.StartEdit();
        state.Save("new plan").Should().NotBeNull();

        state.Apply(WithStatement("old"));

        state.Editing.Should().BeTrue();
        state.PendingStatement.Should().Be("new plan");
        await Task.CompletedTask;
    }

    [Test]
    public async Task An_accepted_save_leaves_editing()
    {
        var state = new TownBallotState();
        state.Open(WithStatement("old"));
        state.StartEdit();
        state.Save("line one\r\nline two");

        state.Apply(WithStatement("line one\nline two"));

        state.Editing.Should().BeFalse();
        state.PendingStatement.Should().BeNull();
        await Task.CompletedTask;
    }

    [Test]
    public async Task Cancel_clears_the_pending_statement()
    {
        var state = new TownBallotState();
        state.Open(WithStatement("old"));
        state.StartEdit();
        state.Save("new plan");

        state.CancelEdit();
        state.Apply(WithStatement("old"));

        state.Editing.Should().BeFalse();
        state.PendingStatement.Should().BeNull();
        await Task.CompletedTask;
    }

    [Test]
    public async Task Saving_refuses_more_than_eight_line_breaks()
    {
        var state = new TownBallotState();
        state.Open(WithStatement("old"));
        state.StartEdit();

        state.Save(string.Join("\n", Enumerable.Repeat("x", 10))).Should().BeNull();
        state.Save(string.Join("\n", Enumerable.Repeat("x", 9))).Should().NotBeNull();
        await Task.CompletedTask;
    }

    [Test]
    public async Task During_candidacy_open_selects_the_viewers_own_entry()
    {
        var state = new TownBallotState();
        state.Open(Ballot(TownBallotStage.Candidacy, false, "", "A", "B", "Me"));

        state.Selected!.Name.Should().Be("Me");
        await Task.CompletedTask;
    }
}
