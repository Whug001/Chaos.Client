using Chaos.Client.ViewModel.College;
using Chaos.Networking.Converters;
using Chaos.Networking.Entities.Server;
using FluentAssertions;

namespace Chaos.Client.Tests.College;

public class BoardStateTests
{
    [Test]
    public void A_packed_board_unpacks_into_a_board_drawing()
    {
        var source = BoardState.NewBoard();
        source.BeginStep();
        source.Set(5, 6, 3);
        source.EndStep();
        var state = new BoardState();

        state.Apply(new CollegeBoardInfo { Live = true, Data = CollegeBoardCodec.Pack(source.PaletteBytes(), source.Pixels) });

        state.Drawing!.Width.Should().Be(160);
        state.Drawing.SwatchAt(5, 6).Should().Be(3);
        state.Version.Should().Be(1);
    }

    [Test]
    public void Empty_or_bad_data_is_a_blank_pane()
    {
        var state = new BoardState();

        state.Apply(new CollegeBoardInfo { Live = true });
        state.Drawing.Should().BeNull();
        state.Apply(new CollegeBoardInfo { Live = true, Data = [1, 2, 3] });
        state.Drawing.Should().BeNull();
        state.Version.Should().Be(2);
    }

    [Test]
    public void A_new_board_is_blank_with_the_pane_as_swatch_zero()
    {
        var board = BoardState.NewBoard();

        board.Width.Should().Be(160);
        board.Pixels.Should().OnlyContain(p => p == 0);
        board.Palette[0].Should().Be(BoardState.Pane);
    }

    [Test]
    public void Unchanged_data_bumps_the_version_but_not_the_drawing_version()
    {
        var data = PackedBoard();
        var state = new BoardState();

        state.Apply(new CollegeBoardInfo { Live = true, Data = data });
        var drawing = state.Drawing;
        state.Apply(new CollegeBoardInfo { Live = true, CanDraw = true, Data = data.ToArray() });

        state.Version.Should().Be(2);
        state.DrawingVersion.Should().Be(1);
        state.Drawing.Should().BeSameAs(drawing);
        state.Info.CanDraw.Should().BeTrue();
    }

    [Test]
    public void A_board_followed_by_empty_data_goes_blank()
    {
        var state = new BoardState();
        state.Apply(new CollegeBoardInfo { Live = true, Data = PackedBoard() });

        state.Apply(new CollegeBoardInfo { Live = true });

        state.Drawing.Should().BeNull();
        state.DrawingVersion.Should().Be(2);
    }

    [Test]
    public void A_board_followed_by_bad_data_goes_blank()
    {
        var state = new BoardState();
        state.Apply(new CollegeBoardInfo { Live = true, Data = PackedBoard() });

        state.Apply(new CollegeBoardInfo { Live = true, Data = [1, 2, 3] });

        state.Drawing.Should().BeNull();
        state.DrawingVersion.Should().Be(2);
    }

    private static byte[] PackedBoard()
    {
        var source = BoardState.NewBoard();

        return CollegeBoardCodec.Pack(source.PaletteBytes(), source.Pixels);
    }

    [Test]
    public void A_new_board_starts_with_nothing_to_undo()
    {
        BoardState.NewBoard().CanUndo.Should().BeFalse();
    }
}
