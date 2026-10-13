using Chaos.Client.ViewModel.College;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using FluentAssertions;

namespace Chaos.Client.Tests.College;

public class HangmanBoardStateTests
{
    private static CollegeHangmanInfo Info()
        => new()
        {
            Mask = "GL__CA",
            Tried = "AGCLERS",
            Word = "",
            Mode = HangmanMode.Survival,
            Phase = HangmanPhase.Open
        };

    [Test]
    public void Letters_are_right_wrong_or_untried()
    {
        var board = new HangmanBoardState(Info());

        foreach (var letter in "GLCA")
            board.LetterState(letter).Should().Be(HangmanLetterState.Right);

        foreach (var letter in "ERS")
            board.LetterState(letter).Should().Be(HangmanLetterState.Wrong);

        board.LetterState('B').Should().Be(HangmanLetterState.Untried);
        board.LetterState('c').Should().Be(HangmanLetterState.Right);
    }

    [Test]
    public void A_known_word_decides_right_over_the_mask()
    {
        var info = Info();
        info.Word = "GLASCA";
        info.Mask = "GLASCA";

        var board = new HangmanBoardState(info);

        board.LetterState('S').Should().Be(HangmanLetterState.Right);
        board.LetterState('E').Should().Be(HangmanLetterState.Wrong);
    }

    [Test]
    [Arguments(true, HangmanPhase.Open, true)]
    [Arguments(true, HangmanPhase.Solving, false)]
    [Arguments(true, HangmanPhase.Revealed, false)]
    [Arguments(false, HangmanPhase.Open, false)]
    public void Letter_buttons_need_my_turn_and_an_open_board(bool myTurn, HangmanPhase phase, bool enabled)
    {
        var info = Info();
        info.MyTurn = myTurn;
        info.Phase = phase;

        new HangmanBoardState(info).LettersEnabled.Should().Be(enabled);
    }

    [Test]
    [Arguments(true, false, true)]
    [Arguments(true, true, false)]
    [Arguments(false, false, false)]
    [Arguments(false, true, false)]
    public void Buzz_shows_when_allowed_and_not_already_solving(bool canBuzz, bool iSolve, bool shown)
    {
        var info = Info();
        info.CanBuzz = canBuzz;
        info.ISolve = iSolve;

        new HangmanBoardState(info).ShowBuzz.Should().Be(shown);
    }

    [Test]
    public void Solve_box_shows_only_for_the_solver()
    {
        var info = Info();
        new HangmanBoardState(info).ShowSolveBox.Should().BeFalse();

        info.ISolve = true;
        new HangmanBoardState(info).ShowSolveBox.Should().BeTrue();
    }

    [Test]
    public void Survival_shows_the_gallows_stage_for_the_misses()
    {
        var info = Info();
        info.Misses = 3;

        var board = new HangmanBoardState(info);

        board.ShowGallows.Should().BeTrue();
        board.GallowsStage.Should().Be(3);
    }

    [Test]
    public void Gallows_stage_is_capped_at_6()
    {
        var info = Info();
        info.Misses = 9;

        new HangmanBoardState(info).GallowsStage.Should().Be(6);
    }

    [Test]
    public void Casual_shows_the_count_instead_of_the_gallows()
    {
        var info = Info();
        info.Mode = HangmanMode.Casual;
        info.Misses = 3;

        var board = new HangmanBoardState(info);

        board.ShowGallows.Should().BeFalse();
        board.GallowsStage.Should().Be(0);
    }
}
