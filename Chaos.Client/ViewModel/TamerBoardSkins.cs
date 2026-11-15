namespace Chaos.Client.ViewModel;

/// <summary>
///     The battle board's skins: a skin key names one full-board picture (an SPF in setoa.dat, like the card game's
///     playmats), drawn under the units in place of the flat tinted halves. Plain strings and numbers; the board view
///     fetches the texture. An unknown or empty key draws the default skin; a missing or wrong-sized picture draws the
///     flat board. A new skin is one more entry in <see cref="Files" /> and one more SPF in setoa.dat
///     (Unora: Tools/Tamer/Board).
/// </summary>
public static class TamerBoardSkins
{
    /// <summary>"Proving Ground": worn flagstones, cool far half, warm near half, a brass strip on the centre line.</summary>
    public const string DEFAULT_KEY = "proving";

    /// <summary>Skin key to its SPF in setoa.dat. Names stay within the archive's 13 characters.</summary>
    private static readonly Dictionary<string, string> Files = new(StringComparer.OrdinalIgnoreCase)
    {
        [DEFAULT_KEY] = "tmrbd000.spf"
    };

    /// <summary>Every known skin key.</summary>
    public static IReadOnlyCollection<string> Keys => Files.Keys;

    /// <summary>The key itself when it names a skin, otherwise the default skin's key.</summary>
    public static string Resolve(string? key)
        => !string.IsNullOrWhiteSpace(key) && Files.ContainsKey(key.Trim()) ? key.Trim().ToLowerInvariant() : DEFAULT_KEY;

    /// <summary>The setoa.dat file the skin draws from; an unknown key gives the default skin's file.</summary>
    public static string FileFor(string? key) => Files[Resolve(key)];

    /// <summary>
    ///     True when a picture of this size can be drawn as the board: exactly the board's size, so it is drawn 1:1
    ///     and never stretched (the renderer's stand-in for a missing file is some other size).
    /// </summary>
    public static bool Fits(int width, int height, int boardWidth, int boardHeight)
        => (width == boardWidth) && (height == boardHeight);
}
