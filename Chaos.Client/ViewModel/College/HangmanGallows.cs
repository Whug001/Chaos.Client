namespace Chaos.Client.ViewModel.College;

public readonly record struct GallowsSegment(int X1, int Y1, int X2, int Y2);

/// <summary>The gallows as line segments on a 46 x 52 grid: the frame always shows, and each miss from 1 to 6 adds a part of the figure.</summary>
public static class HangmanGallows
{
    public const int WIDTH = 46;
    public const int HEIGHT = 52;
    public const int STAGES = 7;

    private static readonly GallowsSegment[][] Parts =
    [
        [new(4, 50, 34, 50), new(12, 50, 12, 4), new(12, 4, 32, 4)],
        [
            new(32, 4, 32, 11), new(30, 11, 34, 11), new(34, 11, 37, 14), new(37, 14, 37, 18), new(37, 18, 34, 21), new(34, 21, 30, 21),
            new(30, 21, 27, 18), new(27, 18, 27, 14), new(27, 14, 30, 11)
        ],
        [new(32, 21, 32, 38)],
        [new(32, 25, 26, 31)],
        [new(32, 25, 38, 31)],
        [new(32, 38, 26, 48)],
        [new(32, 38, 38, 48)]
    ];

    public static IReadOnlyList<GallowsSegment> Segments(int stage)
    {
        var shown = Math.Clamp(stage, 0, STAGES - 1);

        return Parts.Take(shown + 1).SelectMany(p => p).ToList();
    }
}
