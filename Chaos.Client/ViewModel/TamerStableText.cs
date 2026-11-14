#region
using System.Text.RegularExpressions;
using Chaos.Client.Rendering;
using Chaos.DarkAges.Definitions;
#endregion

namespace Chaos.Client.ViewModel;

/// <summary>
///     Fixed strings for the stable window (spec section 6), the name rule (the house-pet rule the
///     server's <c>PetRules.IsValidName</c> checks) and the element colour names. Pure: no textures.
/// </summary>
public static class TamerStableText
{
    public const int MAX_LEVEL = 20;
    public const int MAX_NAME_LENGTH = 12;

    public const string WindowTitle = "Stable";
    public const string CreaturesTab = "Creatures";
    public const string SquadsTab = "Squads";
    public const string Opening = "Opening the ledger...";
    public const string EmptyStable = "No creatures yet. Weaken a wild creature and throw a binding orb.";
    public const string UnknownCreature = "Unknown creature";
    public const string PeakLevel = "Peak level";
    public const string Chosen = "Chosen";
    public const string Choose = "Choose";
    public const string Rename = "Rename";
    public const string Feed = "Feed";
    public const string Release = "Release";
    public const string MakeActive = "Make active";
    public const string Front = "Front";
    public const string Back = "Back";
    public const string NoSetBonus = "No set bonus.";
    public const string DiscardQuestion = "Discard changes to this squad?";
    public const string Close = "Close";
    public const string Save = "Save";
    public const string Revert = "Revert";
    public const string Remove = "Remove";
    public const string Confirm = "Confirm";
    public const string Cancel = "Cancel";
    public const string Yes = "Yes";
    public const string No = "No";
    public const string EssenceLabel = "Essence";
    public const string FeedLabel = "Feed";
    public const string InvalidName = "Name must be 1-12 letters, single spaces between words.";

    private static readonly Regex NamePattern = new("^[A-Za-z]+(?: [A-Za-z]+)*\\z", RegexOptions.Compiled);

    /// <summary>1 to 12 letters with single spaces between words, on the trimmed name (the server trims too).</summary>
    public static bool IsValidName(string? name)
    {
        var cleaned = name is null ? string.Empty : name.Trim();

        return (cleaned.Length is >= 1 and <= MAX_NAME_LENGTH) && NamePattern.IsMatch(cleaned);
    }

    public static string ReleaseQuestion(string name, long essence)
        => $"Release {name} for {essence} essence? This cannot be undone.";

    public static string CountLine(int count, int capacity)
        => $"{count} / {capacity} creatures";

    public static string EssenceLine(long essence)
        => $"Essence {essence}";

    public static string FeedLine(int held)
        => $"Feed {held}";

    public static string SlotsLine(int used, int slots)
        => $"{used} of {slots} slots used";

    public static string ListLevel(int level)
        => $"Lv {level}";

    public static string DetailLevel(int level)
        => $"Level {level}";

    public static string UnlocksAtLevel(int level)
        => $"Unlocks at level {level}";

    public static string FeedPreviewLine(int fromLevel, int toLevel)
        => $"Level {fromLevel} -> {toLevel}";

    public static string SortName(TamerStableSort sort)
        => sort switch
        {
            TamerStableSort.Newest => "Newest",
            TamerStableSort.Level => "Level",
            TamerStableSort.Name => "Name",
            _ => "Unknown"
        };

    public static string SortLabel(TamerStableSort sort)
        => $"Sort: {SortName(sort)}";

    public static string SourceName(TamerWireFeedSource source)
        => source switch
        {
            TamerWireFeedSource.Essence => EssenceLabel,
            TamerWireFeedSource.Feed => FeedLabel,
            _ => "Unknown"
        };

    public static string ElementName(TamerWireElement element)
        => element switch
        {
            TamerWireElement.Fire => "Fire",
            TamerWireElement.Water => "Water",
            TamerWireElement.Wind => "Wind",
            TamerWireElement.Earth => "Earth",
            _ => "Unknown"
        };

    public static string RarityName(TamerWireRarity rarity)
        => rarity switch
        {
            TamerWireRarity.Common => "Common",
            TamerWireRarity.Uncommon => "Uncommon",
            TamerWireRarity.Rare => "Rare",
            TamerWireRarity.Boss => "Boss",
            _ => "Unknown"
        };

    public static string RoleName(TamerWireRole role)
        => role switch
        {
            TamerWireRole.Bruiser => "Bruiser",
            TamerWireRole.Striker => "Striker",
            TamerWireRole.Support => "Support",
            _ => "Unknown"
        };

    public static string ElementColorName(TamerWireElement element)
        => element switch
        {
            TamerWireElement.Fire => "Red",
            TamerWireElement.Water => "Blue",
            TamerWireElement.Wind => "Green",
            TamerWireElement.Earth => "Brown",
            _ => "Gray"
        };

    /// <summary>
    ///     Wraps <paramref name="text" /> to <paramref name="maxWidth" /> pixels and caps it at <paramref name="maxLines" />
    ///     lines, ending the last line with ".." when text was cut, so a fixed-size label never overflows. Colour codes
    ///     are measured as text, matching labels that draw with colour codes off. Empty when the text is null or there
    ///     is no room.
    /// </summary>
    public static string FitLines(string? text, int maxWidth, int maxLines)
    {
        if (string.IsNullOrEmpty(text) || (maxWidth <= 0) || (maxLines <= 0))
            return string.Empty;

        //the wrapping <see cref="TextRenderer.WrapLines" /> skips colour codes in the measure, but the labels this feeds
        //draw with codes off, so wrap here with codes measured as text to stay inside the label's width
        var lines = new List<string>();

        foreach (var paragraph in text.Split('\n'))
        {
            var remaining = paragraph;

            while (remaining.Length > 0)
            {
                var lineEnd = TextRenderer.FindLineBreak(remaining, maxWidth, colorCodesEnabled: false);

                lines.Add(remaining[..lineEnd].TrimEnd());
                remaining = remaining[lineEnd..].TrimStart();
            }

            if (paragraph.Length == 0)
                lines.Add(string.Empty);
        }

        if (lines.Count <= maxLines)
            return string.Join("\n", lines);

        var kept = lines.GetRange(0, maxLines);
        kept[^1] = FitSingleLine(kept[^1], maxWidth);

        return string.Join("\n", kept);
    }

    /// <summary>Trims one line until it fits <paramref name="maxWidth" /> with the ".." dots appended.</summary>
    private static string FitSingleLine(string line, int maxWidth)
    {
        var dots = "..";

        while ((TextRenderer.MeasureWidth(line) + TextRenderer.MeasureWidth(dots) > maxWidth) && (line.Length > 0))
            line = line[..^1];

        while ((line.Length > 0) && char.IsHighSurrogate(line[^1]))
            line = line[..^1];

        return line + dots;
    }
}
