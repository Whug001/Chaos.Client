#region
using Chaos.Client.Models;
using Chaos.DarkAges.Definitions;
#endregion

namespace Chaos.Client.Controls.World.Popups.WorldList;

/// <summary>
///     One face of a world list filter button: its carved label frames in _nusersb.spf and the players it shows.
/// </summary>
public sealed record WorldListFace(string Label, int NormalFrame, int LitFrame, Func<WorldListEntry, bool> Matches);

/// <summary>
///     The nine world list filter buttons and the faces each one turns through. A button's first face is the art that
///     shipped with the drawer (frames 2b and 2b + 1). The carved faces added later sit at 18 + 2i and 19 + 2i, in the
///     order they appear below.
/// </summary>
public static class WorldListFaces
{
    public const int BUTTON_COUNT = 9;

    public static IReadOnlyList<IReadOnlyList<WorldListFace>> Buttons { get; } =
    [
        [Original(0, "Country", _ => true), Added(0, "Temuair", e => e.Continent == Continent.Temuair),
            Added(1, "Medenia", e => e.Continent == Continent.Medenia)],
        [Original(1, "Master", e => e.IsMaster), Added(2, "Ability", e => e.HasAbility)],
        [Class(2, "Warrior", BaseClass.Warrior), Medenian(3, "Berserker", AdvClass.Berserker), Medenian(4, "Warlord", AdvClass.Warlord)],
        [Class(3, "Rogue", BaseClass.Rogue), Medenian(5, "Archer", AdvClass.Archer), Medenian(6, "Assassin", AdvClass.Assassin)],
        [Class(4, "Wizard", BaseClass.Wizard), Medenian(7, "Arcanist", AdvClass.Arcanist),
            Medenian(8, "Elementalist", AdvClass.Elementalist)],
        [Class(5, "Priest", BaseClass.Priest), Medenian(9, "Bard", AdvClass.Bard),
            Medenian(10, "Plague Doctor", AdvClass.PlagueDoctor)],
        [Class(6, "Monk", BaseClass.Monk), Medenian(11, "Adept", AdvClass.Adept), Medenian(12, "Druid", AdvClass.Druid)],
        [Class(7, "Peasant", BaseClass.Peasant)],
        [Original(8, "Guild", e => e.IsGuilded)]
    ];

    private static WorldListFace Added(int index, string label, Func<WorldListEntry, bool> matches)
        => new(label, 18 + 2 * index, 19 + 2 * index, matches);

    //a temuair class face shows the whole class, medenian classes included
    private static WorldListFace Class(int button, string label, BaseClass baseClass)
        => Original(button, label, e => e.BaseClass == baseClass);

    public static int Count(IEnumerable<WorldListEntry> entries, WorldListFace face) => entries.Count(face.Matches);

    private static WorldListFace Medenian(int index, string label, AdvClass advClass)
        => Added(index, label, e => e.AdvClass == advClass);

    private static WorldListFace Original(int button, string label, Func<WorldListEntry, bool> matches)
        => new(label, 2 * button, 2 * button + 1, matches);
}
