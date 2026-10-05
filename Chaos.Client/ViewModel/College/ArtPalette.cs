using System.Globalization;
using Microsoft.Xna.Framework;

namespace Chaos.Client.ViewModel.College;

/// <summary>The 32 colours a new drawing starts with: the DawnBringer set, its parchment first so it is the background (swatch 0).</summary>
public static class ArtPalette
{
    private static readonly string[] Hex =
    [
        "eec39a", "222034", "45283c", "663931", "8f563b", "df7126", "d9a066", "000000",
        "fbf236", "99e550", "6abe30", "37946e", "4b692f", "524b24", "323c39", "3f3f74",
        "306082", "5b6ee1", "639bff", "5fcde4", "cbdbfc", "ffffff", "9badb7", "847e87",
        "696a6a", "595652", "76428a", "ac3232", "d95763", "d77bba", "8f974a", "8a6f30"
    ];

    public static IReadOnlyList<Color> Default { get; } = Hex.Select(Parse).ToArray();

    private static Color Parse(string hex)
        => new(
            byte.Parse(hex.AsSpan(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            byte.Parse(hex.AsSpan(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            byte.Parse(hex.AsSpan(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
}
