using Chaos.Client.ViewModel.College;
using Chaos.DarkAges.Definitions;
using FluentAssertions;

namespace Chaos.Client.Tests.College;

public class VoteDraftsTests
{
    private const byte NONE = CollegeProtocol.NO_TIER;

    [Test]
    public void An_unsaved_vote_comes_back_when_the_judge_returns()
    {
        var drafts = new VoteDrafts();

        drafts.Leave(CollegePieceContext.Judge, 7, 2, "nice", NONE, string.Empty);

        drafts.Restore(CollegePieceContext.Judge, 7, NONE, string.Empty).Should().Be(((byte)2, "nice"));
    }

    [Test]
    public void A_vote_left_as_it_was_saved_keeps_the_server_one()
    {
        var drafts = new VoteDrafts();

        drafts.Leave(CollegePieceContext.Judge, 7, 3, "x", 3, "x");

        drafts.Restore(CollegePieceContext.Judge, 7, 1, "newer").Should().Be(((byte)1, "newer"));
    }

    [Test]
    public void Saving_a_vote_forgets_its_draft()
    {
        var drafts = new VoteDrafts();
        drafts.Leave(CollegePieceContext.Judge, 7, 2, "nice", NONE, string.Empty);

        drafts.Saved(CollegePieceContext.Judge, 7);

        drafts.Restore(CollegePieceContext.Judge, 7, 2, "nice!").Should().Be(((byte)2, "nice!"));
    }

    [Test]
    public void A_judge_vote_and_a_verdict_on_one_entry_are_kept_apart()
    {
        var drafts = new VoteDrafts();

        drafts.Leave(CollegePieceContext.Judge, 7, 2, "vote", NONE, string.Empty);
        drafts.Leave(CollegePieceContext.Verdict, 7, 4, "note", NONE, string.Empty);

        drafts.Restore(CollegePieceContext.Judge, 7, NONE, string.Empty).Should().Be(((byte)2, "vote"));
        drafts.Restore(CollegePieceContext.Verdict, 7, NONE, string.Empty).Should().Be(((byte)4, "note"));
        drafts.Restore(CollegePieceContext.Judge, 8, NONE, string.Empty).Should().Be((NONE, string.Empty));
    }
}
