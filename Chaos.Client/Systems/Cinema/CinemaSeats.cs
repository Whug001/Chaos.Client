namespace Chaos.Client.Systems.Cinema;

public readonly record struct CinemaSeat(int Row, int Col, int X, int Y);

/// <summary>The Loures Cinema's 24 seats, as in Unora Tools/Cinema/seats.json: 3 straight rows facing the screen wall at y = 0; row 0 is nearest the screen, col 0 is leftmost.</summary>
public static class CinemaSeats
{
    public const int MAP_ID = 788;
    public const int ROWS = 3;
    public const int COLS = 8;

    public static IReadOnlyList<CinemaSeat> All { get; } = Enumerable.Range(0, ROWS)
                                                                     .SelectMany(r => Enumerable.Range(0, COLS)
                                                                                                .Select(c => new CinemaSeat(r, c, 5 + c, 4 + 2 * r)))
                                                                     .ToArray();

    public static CinemaSeat? At(int x, int y)
    {
        foreach (var seat in All)
            if ((seat.X == x) && (seat.Y == y))
                return seat;

        return null;
    }
}
