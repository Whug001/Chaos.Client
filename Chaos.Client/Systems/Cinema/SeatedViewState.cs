namespace Chaos.Client.Systems.Cinema;

/// <summary>Whether the seated view is on: the player stands on a cinema seat and has not closed the view with Esc.</summary>
public sealed class SeatedViewState
{
    private (int X, int Y)? ClosedOn;

    public CinemaSeat? Seat { get; private set; }

    public bool IsOn => Seat is not null;

    public bool Update(int mapId, int x, int y)
    {
        var seat = mapId == CinemaSeats.MAP_ID ? CinemaSeats.At(x, y) : null;

        if (ClosedOn is { } closed && ((mapId != CinemaSeats.MAP_ID) || (closed != (x, y))))
            ClosedOn = null;

        Seat = ClosedOn is null ? seat : null;

        return IsOn;
    }

    public void Close()
    {
        if (Seat is { } seat)
            ClosedOn = (seat.X, seat.Y);

        Seat = null;
    }

    /// <summary>Turns the view off and forgets an Esc close; used on a map change.</summary>
    public void Reset()
    {
        Seat = null;
        ClosedOn = null;
    }
}
