namespace Chaos.Client.Controls.World.Popups.Beauty;

/// <summary>The mirror's pages, in the order the guide walks through them.</summary>
public enum MirrorPage
{
    Gender,
    Hair,
    Skin,
    Face,
    Review
}

/// <summary>Page order and the words each page shows: its heading, its one-line instruction and the arrow labels.</summary>
public static class MirrorPages
{
    public const int COUNT = 5;

    public static MirrorPage First => MirrorPage.Gender;

    public static MirrorPage? Previous(MirrorPage page) => page == MirrorPage.Gender ? null : page - 1;

    public static MirrorPage? Next(MirrorPage page) => page == MirrorPage.Review ? null : page + 1;

    /// <summary>The page heading, in capitals.</summary>
    public static string Title(MirrorPage page) => Name(page).ToUpperInvariant();

    /// <summary>The page's name as the arrows show it.</summary>
    public static string Name(MirrorPage page)
        => page switch
        {
            MirrorPage.Gender => "Gender",
            MirrorPage.Hair   => "Hair",
            MirrorPage.Skin   => "Skin",
            MirrorPage.Face   => "Face",
            MirrorPage.Review => "Review",
            _                 => throw new ArgumentOutOfRangeException(nameof(page), page, null)
        };

    /// <summary>The gray line under the heading. The review page draws its own heading and has none.</summary>
    public static string Instruction(MirrorPage page)
        => page switch
        {
            MirrorPage.Gender => "Choose male or female.",
            MirrorPage.Hair   => "Pick a style, then a color. Hover to try one on.",
            MirrorPage.Skin   => "Choose a skin color. Hover to try one on.",
            MirrorPage.Face   => "Choose a face. Hover to try one on.",
            _                 => string.Empty
        };

    public static string BackLabel(MirrorPage page) => Previous(page) is { } previous ? $"< {Name(previous)}" : string.Empty;

    public static string NextLabel(MirrorPage page) => Next(page) is { } next ? $"{Name(next)} >" : "APPLY";
}
