#region
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Client.Tests;

internal static class TcgMatchFixtures
{
    internal static TcgCatalogCardView Card(
        string key,
        TcgCardType type,
        byte generic = 0,
        byte fire = 0,
        TcgTargetKind target = TcgTargetKind.None,
        TcgKeyword keywords = TcgKeyword.None)
        => new(key, key, TcgElement.Fire, TcgRarity.Common, type, generic, fire, 0, 0, 0, 2, 2, keywords, "", TcgArtKind.None, 0, target);

    internal static readonly Dictionary<string, TcgCatalogCardView> Catalog = new()
    {
        ["bear"] = Card("bear", TcgCardType.Creature, generic: 1, fire: 1),
        ["bolt"] = Card("bolt", TcgCardType.Instant, fire: 1, target: TcgTargetKind.EnemyCreature),
        ["heal"] = Card("heal", TcgCardType.Spell, generic: 1, target: TcgTargetKind.Self),
        ["snare"] = Card("snare", TcgCardType.Trap, generic: 1),
        ["drake"] = Card("drake", TcgCardType.Creature, keywords: TcgKeyword.Flying),
        ["owl"] = Card("owl", TcgCardType.Creature, keywords: TcgKeyword.Reach),
        ["wall"] = Card("wall", TcgCardType.Creature, keywords: TcgKeyword.Guard),
        ["boar"] = Card("boar", TcgCardType.Creature, keywords: TcgKeyword.Trample),
        ["golem"] = Card("golem", TcgCardType.Creature, keywords: TcgKeyword.Ward)
    };

    internal static TcgCatalogCardView? Lookup(string key) => Catalog.GetValueOrDefault(key);

    internal static TcgManaView Mana(int id, TcgElement element, bool tapped = false) => new(id, element, tapped, "");

    internal static TcgCreatureView Creature(int id, string key, bool tapped = false, bool sick = false)
        => new(id, key, 2, 2, 0, tapped, sick);

    internal static TcgDisplayArgs Board(
        TcgDecision decision = TcgDecision.Main,
        TcgSeat decider = TcgSeat.A,
        TcgPhase phase = TcgPhase.Main1,
        List<TcgManaView>? mana = null,
        List<TcgCreatureView>? mine = null,
        List<TcgCreatureView>? theirs = null,
        List<TcgCardView>? traps = null)
        => new()
        {
            Type = TcgDisplayType.Update,
            YourSeat = TcgSeat.A,
            ActiveSeat = TcgSeat.A,
            Phase = phase,
            Decision = decision,
            Decider = decider,
            Players =
            [
                new TcgPlayerView { Name = "Ana", Mana = mana ?? [], Battlefield = mine ?? [], Traps = traps ?? [] },
                new TcgPlayerView { Name = "Bo", Battlefield = theirs ?? [] }
            ]
        };
}
