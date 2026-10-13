using Chaos.Client.ViewModel.College;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using FluentAssertions;

namespace Chaos.Client.Tests.College;

public class ScrambleHelpersTests
{
    [Test]
    public void Short_words_get_full_size_tiles()
    {
        var layout = new ScrambleTileLayout("HILMET", 338, 18);

        layout.TileWidth.Should().Be(18);
        layout.Tiles.Select(t => t.Letter).Should().Equal("HILMET".ToCharArray());
    }

    [Test]
    public void Twenty_characters_fit_on_one_line()
    {
        var layout = new ScrambleTileLayout(new string('A', 20), 338, 18);

        layout.TileWidth.Should().BeLessThan(18);
        layout.TotalWidth.Should().BeLessThanOrEqualTo(338);
        layout.Tiles[^1].X.Should().BeLessThan(338);
    }

    [Test]
    public void Spaces_leave_a_gap_and_no_tile()
    {
        var layout = new ScrambleTileLayout("AB CD", 200, 18);

        layout.Tiles.Should().HaveCount(4);
        (layout.Tiles[2].X - layout.Tiles[1].X).Should().Be(18 + 2 + 8 + 2);
    }

    [Test]
    public void Wrong_mark_stays_until_a_guess_or_the_next_word()
    {
        var state = new ScrambleCardState();
        state.Apply(Info(2, wrong: true));
        state.ShowWrong.Should().BeTrue();

        state.Apply(Info(2));
        state.ShowWrong.Should().BeTrue();

        state.NoteGuess();
        state.ShowWrong.Should().BeFalse();

        state.Apply(Info(2, wrong: true));
        state.Apply(Info(3));
        state.ShowWrong.Should().BeFalse();
    }

    [Test]
    public void A_solve_locks_the_box_and_hides_the_wrong_mark()
    {
        var state = new ScrambleCardState();
        state.Apply(Info(1, wrong: true));
        state.BoxOpen.Should().BeTrue();

        var solved = Info(1);
        solved.MyPlace = 2;
        state.Apply(solved);

        state.BoxOpen.Should().BeFalse();
        state.ShowWrong.Should().BeFalse();
    }

    [Test]
    public void Revealed_card_spells_the_word()
    {
        var state = new ScrambleCardState();
        var info = Info(1);
        info.Phase = ScramblePhase.Revealed;
        info.Word = "MILETH";
        state.Apply(info);

        state.Tiles.Should().Be("MILETH");
    }

    [Test]
    public void Solvers_line_groups_points()
    {
        var info = Info(1);
        info.Solvers = ["Aroha", "Mike", "Bryn", "Kael"];

        ScrambleCardState.SolversLine(info, "mike").Should().Be("Solved: Aroha (3), you (2), Bryn, Kael (1)");
        ScrambleCardState.SolversLine(Info(1), "x").Should().Be("No one solved it.");
    }

    [Test]
    public void Teacher_status_shows_the_clock_only_while_open()
    {
        var info = Info(1);
        info.SolvedCount = 4;
        info.Students = 9;

        ScrambleCardState.TeacherStatus(info, 12).Should().Be("Solved 4 of 9 · 0:12 left");

        info.Phase = ScramblePhase.Revealed;
        ScrambleCardState.TeacherStatus(info, 0).Should().Be("Solved 4 of 9");
    }

    private static CollegeScrambleInfo Info(int number, bool wrong = false)
        => new()
        {
            Number = (byte)number,
            Phase = ScramblePhase.Open,
            WrongGuess = wrong
        };
}
