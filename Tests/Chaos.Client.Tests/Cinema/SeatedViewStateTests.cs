using Chaos.Client.Systems.Cinema;
using FluentAssertions;

namespace Chaos.Client.Tests.Cinema;

public sealed class SeatedViewStateTests
{
    [Test]
    public void On_only_on_a_seat_in_the_room()
    {
        var state = new SeatedViewState();
        var seat = CinemaSeats.All[0];

        state.Update(788, seat.X, seat.Y).Should().BeTrue();
        state.Seat!.Value.Row.Should().Be(0);
        state.Update(788, 4, 9).Should().BeFalse();
        state.Update(15, seat.X, seat.Y).Should().BeFalse();
    }

    [Test]
    public void Close_holds_until_the_player_moves()
    {
        var state = new SeatedViewState();
        var seat = CinemaSeats.All[0];

        state.Update(788, seat.X, seat.Y);
        state.Close();
        state.Update(788, seat.X, seat.Y).Should().BeFalse();
        state.Update(788, 6, 7).Should().BeFalse();
        state.Update(788, seat.X, seat.Y).Should().BeTrue();
    }

    [Test]
    public void Another_map_forgets_the_close()
    {
        var state = new SeatedViewState();
        var seat = CinemaSeats.All[0];

        state.Update(788, seat.X, seat.Y);
        state.Close();
        state.Update(15, seat.X, seat.Y);
        state.Update(788, seat.X, seat.Y).Should().BeTrue();
    }

    [Test]
    public void Reset_forgets_an_Esc_close()
    {
        var state = new SeatedViewState();
        var seat = CinemaSeats.All[0];

        state.Update(CinemaSeats.MAP_ID, seat.X, seat.Y);
        state.Close();
        state.Reset();
        state.IsOn.Should().BeFalse();
        state.Update(CinemaSeats.MAP_ID, seat.X, seat.Y).Should().BeTrue();
    }
}
