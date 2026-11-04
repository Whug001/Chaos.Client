using Chaos.DarkAges.Definitions;

namespace Chaos.Client.Controls.LobbyLogin;

/// <summary>
///     The 14 hair colors offered at character creation, in swatch order, and stepping through them with arrows.
/// </summary>
public static class HairColorCycle
{
    private static readonly DisplayColor[] COLORS =
    [
        DisplayColor.Teal,
        DisplayColor.Green,
        DisplayColor.Olive,
        DisplayColor.Yellow,
        DisplayColor.Pumpkin,
        DisplayColor.Apple,
        DisplayColor.Violet,
        DisplayColor.Default,
        DisplayColor.Navy,
        DisplayColor.Blue,
        DisplayColor.Gray,
        DisplayColor.Carrot,
        DisplayColor.Brown,
        DisplayColor.Black
    ];

    public static IReadOnlyList<DisplayColor> Colors => COLORS;

    /// <summary>
    ///     Returns the next color for a positive direction and the previous one for a negative direction, wrapping at both
    ///     ends. A color outside the list gives the first color going forward and the last going back.
    /// </summary>
    public static DisplayColor Step(DisplayColor current, int direction)
    {
        if (direction == 0)
            return current;

        var index = Array.IndexOf(COLORS, current);

        if (index < 0)
            return direction > 0 ? COLORS[0] : COLORS[^1];

        return COLORS[(index + Math.Sign(direction) + COLORS.Length) % COLORS.Length];
    }
}
