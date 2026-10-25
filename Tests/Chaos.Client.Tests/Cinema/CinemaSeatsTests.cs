using Chaos.Client.Systems.Cinema;
using FluentAssertions;

namespace Chaos.Client.Tests.Cinema;

public sealed class CinemaSeatsTests
{
    [Test]
    public void Seats_follow_the_room_formula()
    {
        CinemaSeats.MAP_ID.Should().Be(788);
        CinemaSeats.All.Should().HaveCount(24);

        foreach (var seat in CinemaSeats.All)
        {
            seat.X.Should().Be(5 + seat.Col);
            seat.Y.Should().Be(4 + 2 * seat.Row);
            CinemaSeats.At(seat.X, seat.Y).Should().Be(seat);
        }
    }

    [Test]
    public void Aisles_are_not_seats()
    {
        CinemaSeats.At(4, 9).Should().BeNull();
        CinemaSeats.At(6, 14).Should().BeNull();
    }
}
