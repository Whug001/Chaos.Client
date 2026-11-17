#region
using System.Globalization;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Client.Controls.World.Popups.Learn;

/// <summary>One requirement line of the learn-ability popup: what is needed, how much, and whether the player has it.</summary>
/// <param name="Label">
///     What is needed: "Level", "Master", "Grand Master", "Gold", "Ability Level", a stat ("INT"), an item or an ability
///     name.
/// </param>
/// <param name="Value">How much: "40", "15,000", "25", "x3", "Lv 20", or empty when the name says it all.</param>
/// <param name="Met">Whether the player currently meets it.</param>
public sealed record LearnAbilityRow(string Label, string Value, bool Met);

/// <summary>
///     What the learn-ability popup shows, built from the server's <see cref="LearnAbilityDisplayArgs" />. Kept free of
///     any drawing so the text and row order can be tested on their own.
/// </summary>
/// <param name="Title">The ability's name, e.g. "Arcane Missile". The required level is a row, so it can show as unmet.</param>
/// <param name="ReplacesLine">"Replaces: A, B" for an upgrade, or null when it replaces nothing.</param>
/// <param name="Description">The ability's description, color codes kept.</param>
/// <param name="Rows">
///     The requirements in display order: level, master, grand master, gold, ability level, stats, items, then skills
///     and spells. Level, gold and ability level are left out when they are 0; master and grand master only appear when
///     the ability needs them.
/// </param>
/// <param name="ShowGuildFunds">Whether the dialog offers paying with guild funds.</param>
public sealed record LearnAbilityView(
    string Title,
    string? ReplacesLine,
    string Description,
    IReadOnlyList<LearnAbilityRow> Rows,
    bool ShowGuildFunds)
{
    /// <summary>Builds the view for <paramref name="args" />.</summary>
    public static LearnAbilityView From(LearnAbilityDisplayArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var rows = new List<LearnAbilityRow>();

        if (args.Level > 0)
            rows.Add(new LearnAbilityRow("Level", args.Level.ToString(), args.LevelMet));

        if (args.RequiresMaster)
            rows.Add(new LearnAbilityRow("Master", string.Empty, args.MasterMet));

        if (args.GrandMasterOnly)
            rows.Add(new LearnAbilityRow("Grand Master", string.Empty, args.GrandMasterMet));

        if (args.Gold > 0)
            rows.Add(new LearnAbilityRow("Gold", args.Gold.ToString("N0", CultureInfo.InvariantCulture), args.GoldMet));

        if (args.AbilityLevel > 0)
            rows.Add(new LearnAbilityRow("Ability Level", args.AbilityLevel.ToString(), args.AbilityLevelMet));

        foreach (var stat in args.Stats)
            rows.Add(new LearnAbilityRow(StatLabel(stat.Stat), stat.Required.ToString(), stat.Met));

        foreach (var item in args.Items)
            rows.Add(new LearnAbilityRow(item.Name, $"x{item.Amount}", item.Met));

        foreach (var ability in args.Skills.Concat(args.Spells))
            rows.Add(new LearnAbilityRow(ability.Name, ability.Level > 0 ? $"Lv {ability.Level}" : string.Empty, ability.Met));

        var replaces = args.Replaces.Count > 0 ? $"Replaces: {string.Join(", ", args.Replaces)}" : null;

        return new LearnAbilityView(
            args.Name,
            replaces,
            args.Description,
            rows,
            args.GuildFundsOption != LearnAbilityDisplayArgs.NoOption);
    }

    //the stat arrives as a raw byte; one this client does not know shows as its number rather than failing
    private static string StatLabel(Stat stat) => Enum.IsDefined(stat) ? stat.ToString() : ((int)stat).ToString();
}
