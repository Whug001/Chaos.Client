#region
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Client.ViewModel;

public readonly record struct TcgPlayChoice(bool CanPlay, bool CanMana);

/// <summary>
///     The client's copy of the match rules, used only to grey out and highlight. The server still judges every action.
/// </summary>
public static class TcgMatchRules
{
    public const int MAX_TRAPS = 3;
    public const int WARD_TAX = 2;

    private static readonly TcgElement[] Elements = [TcgElement.Fire, TcgElement.Water, TcgElement.Wind, TcgElement.Earth];

    public static int PlayerTarget(TcgSeat seat) => seat == TcgSeat.A ? -1 : -2;

    public static bool YouDecide(TcgDisplayArgs board, TcgDecision decision)
        => (board.Decision == decision) && (board.Decider == board.YourSeat);

    public static bool CanPay(IEnumerable<TcgManaView> mana, TcgCatalogCardView card, int extraGeneric)
    {
        var free = mana.Where(m => !m.Tapped).ToList();

        foreach (var element in Elements)
        {
            var need = Symbols(card, element);

            if (free.Count(m => m.Element == element) < need)
                return false;

            for (var i = 0; i < need; i++)
                free.Remove(free.First(m => m.Element == element));
        }

        return free.Count >= card.Generic + extraGeneric;
    }

    public static bool NeedsTarget(TcgCatalogCardView card)
        => card.Target is TcgTargetKind.AnyCreature
                          or TcgTargetKind.EnemyCreature
                          or TcgTargetKind.FriendlyCreature
                          or TcgTargetKind.CreatureOrPlayer
                          or TcgTargetKind.Spell;

    public static TcgPlayChoice Play(TcgDisplayArgs board, TcgCardView handCard, Func<string, TcgCatalogCardView?> lookup, bool manaPlacedThisTurn)
    {
        if (lookup(handCard.Key) is not { } card)
            return new TcgPlayChoice(false, false);

        var you = board.Players[(int)board.YourSeat];

        if (YouDecide(board, TcgDecision.Respond))
            return new TcgPlayChoice((card.Type == TcgCardType.Instant) && Affordable(board, card, lookup), false);

        if (!YouDecide(board, TcgDecision.Main))
            return new TcgPlayChoice(false, false);

        var canPlay = card.Type == TcgCardType.Trap
            ? (you.Traps.Count < MAX_TRAPS) && CanPay(you.Mana, card, 0)
            : Affordable(board, card, lookup);

        return new TcgPlayChoice(canPlay, !manaPlacedThisTurn);
    }

    public static List<int> LegalTargets(TcgDisplayArgs board, TcgCatalogCardView card, Func<string, TcgCatalogCardView?> lookup)
    {
        var you = board.Players[(int)board.YourSeat];
        var them = board.Players[(int)Other(board.YourSeat)];

        if (card.Target == TcgTargetKind.Spell)
            return CanPay(you.Mana, card, 0) ? board.Stack.Select(item => item.CardId).ToList() : [];

        IEnumerable<TcgCreatureView> creatures = card.Target switch
        {
            TcgTargetKind.AnyCreature or TcgTargetKind.CreatureOrPlayer => you.Battlefield.Concat(them.Battlefield),
            TcgTargetKind.EnemyCreature                                 => them.Battlefield,
            TcgTargetKind.FriendlyCreature                              => you.Battlefield,
            _                                                           => []
        };

        var targets = creatures.Where(creature => CanPay(you.Mana, card, HasKeyword(creature.Key, TcgKeyword.Ward, lookup) ? WARD_TAX : 0))
                               .Select(creature => creature.Id)
                               .ToList();

        if ((card.Target == TcgTargetKind.CreatureOrPlayer) && CanPay(you.Mana, card, 0))
            targets.AddRange([PlayerTarget(TcgSeat.A), PlayerTarget(TcgSeat.B)]);

        return targets;
    }

    public static bool CanAttack(TcgDisplayArgs board, TcgCreatureView creature)
        => (board.Phase == TcgPhase.Main1) && YouDecide(board, TcgDecision.Main) && !creature.Tapped && !creature.SummoningSick;

    public static bool CanBlock(TcgCreatureView blocker, TcgCreatureView attacker, Func<string, TcgCatalogCardView?> lookup)
        => !blocker.Tapped
           && (!HasKeyword(attacker.Key, TcgKeyword.Flying, lookup)
               || HasKeyword(blocker.Key, TcgKeyword.Flying, lookup)
               || HasKeyword(blocker.Key, TcgKeyword.Reach, lookup));

    /// <summary>True when an idle Guard creature could still block an attacker nobody is blocking (the server refuses that).</summary>
    public static bool GuardProblem(TcgDisplayArgs board, IReadOnlyList<TcgBlock> blocks, Func<string, TcgCatalogCardView?> lookup)
    {
        var you = board.Players[(int)board.YourSeat];
        var them = board.Players[(int)Other(board.YourSeat)];
        var blocking = blocks.Select(block => block.BlockerId).ToHashSet();

        var unblocked = them.Battlefield
                            .Where(attacker => board.Attackers.Contains(attacker.Id) && blocks.All(block => block.AttackerId != attacker.Id))
                            .ToList();

        return you.Battlefield
                  .Where(guard => HasKeyword(guard.Key, TcgKeyword.Guard, lookup) && !blocking.Contains(guard.Id))
                  .Any(guard => unblocked.Any(attacker => CanBlock(guard, attacker, lookup)));
    }

    public static List<int> AttackersNeedingOrder(TcgDisplayArgs board, IReadOnlySet<int> alreadyOrdered)
    {
        if (!YouDecide(board, TcgDecision.DamageOrder))
            return [];

        return board.Attackers
                    .Where(id => !alreadyOrdered.Contains(id) && (board.Blocks.Count(block => block.AttackerId == id) >= 2))
                    .ToList();
    }

    public static TcgSeat Other(TcgSeat seat) => seat == TcgSeat.A ? TcgSeat.B : TcgSeat.A;

    private static bool Affordable(TcgDisplayArgs board, TcgCatalogCardView card, Func<string, TcgCatalogCardView?> lookup)
        => NeedsTarget(card)
            ? LegalTargets(board, card, lookup).Count > 0
            : CanPay(board.Players[(int)board.YourSeat].Mana, card, 0);

    private static bool HasKeyword(string key, TcgKeyword keyword, Func<string, TcgCatalogCardView?> lookup)
        => lookup(key) is { } card && ((card.Keywords & keyword) != 0);

    private static int Symbols(TcgCatalogCardView card, TcgElement element)
        => element switch
        {
            TcgElement.Fire  => card.Fire,
            TcgElement.Water => card.Water,
            TcgElement.Wind  => card.Wind,
            TcgElement.Earth => card.Earth,
            _                => 0
        };
}
