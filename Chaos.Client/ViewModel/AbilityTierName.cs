namespace Chaos.Client.ViewModel;

/// <summary>
///     Ability tier names. An upgrade is named after the ability with its tier on the end ("Sever", "Sever 2",
///     "Sever 3"), so a hotbar entry saved under one tier still finds the ability after it is upgraded.
/// </summary>
public static class AbilityTierName
{
    /// <summary>
    ///     The name without a trailing tier number ("Sever 3" -> "Sever"). Names without one are returned unchanged.
    /// </summary>
    public static string BaseName(string name)
    {
        var end = name.Length;

        while ((end > 0) && char.IsAsciiDigit(name[end - 1]))
            end--;

        //only a number set off by a space is a tier; a name that is all digits or has no space before them is not
        if ((end == name.Length) || (end < 2) || (name[end - 1] != ' '))
            return name;

        return name[..(end - 1)].TrimEnd();
    }

    /// <summary>
    ///     True when both names are tiers of the same ability, ignoring case.
    /// </summary>
    public static bool SameAbility(string? x, string? y)
        => (x is not null) && (y is not null) && BaseName(x).Equals(BaseName(y), StringComparison.OrdinalIgnoreCase);
}
