using Chaos.Client.ViewModel.College;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using FluentAssertions;

namespace Chaos.Client.Tests.College;

public class ClassToolTextTests
{
    private static CollegeDebateInfo Debate()
        => new()
        {
            For = 2,
            Against = 2,
            Undecided = 1,
            OpeningFor = 1,
            OpeningAgainst = 2,
            OpeningUndecided = 2
        };

    [Test]
    public void Clock_shows_minutes_and_seconds()
    {
        ClassToolText.Clock(72).Should().Be("1:12");
        ClassToolText.Clock(5).Should().Be("0:05");
    }

    [Test]
    public void Right_line_counts_who_got_it()
    {
        ClassToolText.RightLine(4, 9).Should().Be("4 of 9 got it right.");
        ClassToolText.RightLine(0, 0).Should().Be("No one answered.");
    }

    [Test]
    public void My_result_says_right_wrong_or_none_with_the_score()
    {
        ClassToolText.MyResult(2, 2, 3).Should().Be("Right! Your score: 3.");
        ClassToolText.MyResult(1, 2, 2).Should().Be("Wrong. Your score: 2.");
        ClassToolText.MyResult(CollegeProtocol.NO_ANSWER, 2, 2).Should().Be("No answer. Your score: 2.");
    }

    [Test]
    public void Top_line_shows_the_viewer_as_you()
    {
        var top = new List<CollegeScoreInfo>
        {
            new() { Name = "Aroha", Score = 3 },
            new() { Name = "Kael", Score = 3 },
            new() { Name = "Mira", Score = 2 }
        };

        ClassToolText.TopLine(top, "mira").Should().Be("Top: Aroha 3, Kael 3, you 2");
        ClassToolText.TopLine([], "mira").Should().BeEmpty();
    }

    [Test]
    public void Answered_line_counts_and_shows_the_time()
        => ClassToolText.AnsweredLine(7, 9, 14).Should().Be("Answered 7 of 9 - 0:14 left");

    [Test]
    public void Debate_counts_and_gains()
    {
        ClassToolText.Counts(Debate()).Should().Be("For 2  Against 2  Undecided 1");
        ClassToolText.CountsWithGains(Debate()).Should().Be("For 2 (+1)  Ag. 2 (0)  Und. 1");
        ClassToolText.Opening(Debate()).Should().Be("Opening: For 1, Against 2, Undecided 2");
    }

    [Test]
    public void Result_line_names_the_winner_and_its_gain()
    {
        ClassToolText.ResultLine(DebateResult.For, Debate()).Should().Be("For wins (+1)");
        ClassToolText.ResultLine(DebateResult.Draw, Debate()).Should().Be("A draw.");
        ClassToolText.Leading(Debate()).Should().Be(DebateResult.For);
    }

    [Test]
    public void Floor_line_shows_the_speaker_or_open()
    {
        ClassToolText.FloorLine("Kael", 72).Should().Be("Floor: Kael 1:12 left");
        ClassToolText.FloorLine("", 0).Should().Be("Floor: open");
    }

    [Test]
    public void Markers_name_the_side_and_star_the_speaker()
    {
        ClassToolText.Marker(DebateSide.For, false).Should().Be("For");
        ClassToolText.Marker(DebateSide.Against, true).Should().Be("Against *");
        ClassToolText.Marker(DebateSide.Undecided, false).Should().Be("?");
        ClassToolText.Marker(DebateSide.None, true).Should().BeEmpty();
    }

    [Test]
    public void Captions_name_the_quiz_and_the_vote()
    {
        ClassToolText.QuizCaption(new CollegeQuizInfo { Title = "Mileth history", Number = 3, Count = 8 }).Should().Be("Quiz: Mileth history (3/8)");
        ClassToolText.VoteCaption(DebatePhase.Opening, 48).Should().Be("Opening vote: 0:48");
        ClassToolText.VoteCaption(DebatePhase.Final, 18).Should().Be("Final vote: 0:18");
        ClassToolText.HandsMore(3, 5).Should().Be("+2 more");
        ClassToolText.HandsMore(3, 3).Should().BeEmpty();
    }
}
