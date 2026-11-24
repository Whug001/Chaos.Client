#region
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Rendering.CustomEmotes;

/// <summary>
///     The padlock drawn over a locked Crane Arcade prize emote in the wheel, the slot picker row and
///     the catalog grid. 7 wide x 8 tall: a grey shackle over a gold body with a dark keyhole.
/// </summary>
public static class LockBadge
{
    private static readonly Dictionary<char, Color> Palette = new()
    {
        ['s'] = new Color(150, 150, 160),
        ['b'] = new Color(232, 184, 48),
        ['K'] = new Color(20, 16, 12)
    };

    private static readonly IReadOnlyList<string> Pixels =
    [
        "..sss..",
        ".s...s.",
        ".s...s.",
        ".s...s.",
        "bbbbbbb",
        "bbbKbbb",
        "bbbKbbb",
        "bbbbbbb"
    ];

    public static PixelArt Art { get; } = PixelArt.FromPalette("ui:emote-lock", Pixels, Palette);
}
