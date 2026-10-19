#region
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Client.ViewModel;

public enum TcgInputMode : byte
{
    Idle,
    CardMenu,
    Targeting,
    Attacking,
    Blocking,
    Ordering,
    Redraw,
    ConfirmConcede
}

/// <summary>Turns clicks and drags in the match window into actions. Holds no drawing state.</summary>
public sealed class TcgMatchInput(Func<string, TcgCatalogCardView?> lookup)
{
    private readonly List<int> ChosenAttackers = [];
    private readonly Dictionary<int, int> BlockByBlocker = [];
    private readonly List<int> Order = [];
    private readonly HashSet<int> Ordered = [];
    private readonly HashSet<int> PutBack = [];
    private readonly List<int> Picked = [];
    private TcgInputMode BeforeConcede = TcgInputMode.Idle;
    private int LastOrdered;

    public TcgInputMode Mode { get; private set; }
    public int SelectedCard { get; private set; }
    public int SelectedBlocker { get; private set; }
    public IReadOnlyList<int> Targets { get; private set; } = [];
    public IReadOnlyList<int> Attackers => ChosenAttackers;
    public IReadOnlyDictionary<int, int> Blocks => BlockByBlocker;
    public IReadOnlyList<int> DamageOrder => Order;
    public int OrderingAttacker { get; private set; }
    public IReadOnlySet<int> RedrawMarks => PutBack;
    public IReadOnlyList<int> PickedMana => Picked;

    /// <summary>The chat line for a WrongMana refusal of the last action sent with picks.</summary>
    public string? WrongManaNote { get; private set; }

    public void ToggleMana(TcgDisplayArgs board, int manaId)
    {
        if (Picked.Remove(manaId))
            return;

        if ((board.Players.Count > (int)board.YourSeat) && board.Players[(int)board.YourSeat].Mana.Exists(m => (m.Id == manaId) && !m.Tapped))
            Picked.Add(manaId);
    }

    public void Sync(TcgDisplayArgs board)
    {
        if (board.Players.Count > (int)board.YourSeat)
        {
            if ((board.Rejected != TcgRejectReason.None) || board.Events.Exists(e => e.Type == TcgEventType.TurnStarted))
                Picked.Clear();
            else
                Picked.RemoveAll(id => !board.Players[(int)board.YourSeat].Mana.Exists(m => (m.Id == id) && !m.Tapped));
        }

        //a refused damage order was never applied: let that attacker be ordered again
        if ((board.Rejected != TcgRejectReason.None) && (LastOrdered != 0) && TcgMatchRules.YouDecide(board, TcgDecision.DamageOrder))
        {
            Ordered.Remove(LastOrdered);
            LastOrdered = 0;
        }

        //a half-picked play no longer means anything once the decision moves away from us
        if (Mode is TcgInputMode.Targeting or TcgInputMode.CardMenu
            && !TcgMatchRules.YouDecide(board, TcgDecision.Main)
            && !TcgMatchRules.YouDecide(board, TcgDecision.Respond))
        {
            Mode = TcgInputMode.Idle;
            Targets = [];
        } else if ((Mode == TcgInputMode.Targeting)
                   && (HandCard(board, SelectedCard) is { } held)
                   && (lookup(held.Key) is { } heldCard))
            Targets = Targets.Intersect(TcgMatchRules.LegalTargets(board, heldCard, lookup)).ToList();
        else if (Mode == TcgInputMode.Targeting)
        {
            Mode = TcgInputMode.Idle;
            Targets = [];
        }

        if (TcgMatchRules.YouDecide(board, TcgDecision.Block))
        {
            if (Mode != TcgInputMode.Blocking)
            {
                BlockByBlocker.Clear();
                SelectedBlocker = 0;
                Mode = TcgInputMode.Blocking;
            }

            return;
        }

        if (TcgMatchRules.YouDecide(board, TcgDecision.DamageOrder))
        {
            var next = TcgMatchRules.AttackersNeedingOrder(board, Ordered);

            if (next.Count > 0)
            {
                if ((Mode != TcgInputMode.Ordering) || (OrderingAttacker != next[0]))
                {
                    OrderingAttacker = next[0];
                    Order.Clear();
                    Mode = TcgInputMode.Ordering;
                }

                return;
            }
        } else
        {
            Ordered.Clear();
            LastOrdered = 0;
        }

        if ((board.Phase == TcgPhase.Redraw) && (board.Decision == TcgDecision.Redraw))
        {
            if (Mode != TcgInputMode.Redraw)
            {
                PutBack.Clear();
                Mode = TcgInputMode.Redraw;
            }

            return;
        }

        if (Mode is TcgInputMode.Blocking or TcgInputMode.Ordering or TcgInputMode.Redraw)
        {
            SelectedBlocker = 0;
            Mode = TcgInputMode.Idle;
        }

        if ((Mode == TcgInputMode.Attacking) && !TcgMatchRules.YouDecide(board, TcgDecision.Main))
            Mode = TcgInputMode.Idle;
    }

    public void ClickHandCard(TcgDisplayArgs board, int cardId)
    {
        if (Mode is TcgInputMode.Blocking or TcgInputMode.Ordering)
            return;

        if (Mode == TcgInputMode.Redraw)
        {
            ToggleRedraw(cardId);

            return;
        }

        SelectedCard = cardId;
        Mode = TcgInputMode.CardMenu;
    }

    public TcgActionArgs? ChoosePlay(TcgDisplayArgs board)
    {
        if ((Mode != TcgInputMode.CardMenu) || (HandCard(board, SelectedCard) is not { } view) || (lookup(view.Key) is not { } card))
            return null;

        if (card.Type == TcgCardType.Trap)
            return Paid(new TcgActionArgs { Type = TcgActionType.SetTrap, CardId = SelectedCard }, card, 0);

        if (!TcgMatchRules.NeedsTarget(card))
            return Paid(new TcgActionArgs { Type = TcgActionType.Cast, CardId = SelectedCard }, card, 0);

        Targets = TcgMatchRules.LegalTargets(board, card, lookup);
        Mode = TcgInputMode.Targeting;

        return null;
    }

    public TcgActionArgs? ChooseTarget(TcgDisplayArgs board, int targetId)
    {
        if ((Mode != TcgInputMode.Targeting) || !Targets.Contains(targetId))
            return null;

        if ((HandCard(board, SelectedCard) is not { } view) || (lookup(view.Key) is not { } card))
            return null;

        var extra = IsWardCreature(board, targetId) ? TcgMatchRules.WARD_TAX : 0;

        return Paid(new TcgActionArgs { Type = TcgActionType.Cast, CardId = SelectedCard, TargetId = targetId }, card, extra);
    }

    public TcgActionArgs? ChooseMana()
        => Mode == TcgInputMode.CardMenu ? Done(new TcgActionArgs { Type = TcgActionType.PlaceMana, CardId = SelectedCard }) : null;

    /// <summary>A drag from the hand: dropped on the battlefield plays it (or starts targeting); dropped on the mana gems places it.</summary>
    public TcgActionArgs? DropHandCard(TcgDisplayArgs board, int cardId, bool onMana)
    {
        if (Mode is TcgInputMode.Blocking or TcgInputMode.Ordering or TcgInputMode.Redraw)
            return null;

        SelectedCard = cardId;
        Mode = TcgInputMode.CardMenu;

        return onMana ? ChooseMana() : ChoosePlay(board);
    }

    public void StartAttack(TcgDisplayArgs board)
    {
        if (!TcgMatchRules.YouDecide(board, TcgDecision.Main) || (board.Phase != TcgPhase.Main1))
            return;

        ChosenAttackers.Clear();
        Mode = TcgInputMode.Attacking;
    }

    public void ToggleAttacker(TcgDisplayArgs board, int creatureId)
    {
        if (Mode != TcgInputMode.Attacking)
            return;

        if (ChosenAttackers.Remove(creatureId))
            return;

        var creature = board.Players[(int)board.YourSeat].Battlefield.Find(c => c.Id == creatureId);

        if ((creature is not null) && TcgMatchRules.CanAttack(board, creature))
            ChosenAttackers.Add(creatureId);
    }

    public bool AssignBlock(TcgDisplayArgs board, int blockerId, int attackerId)
    {
        if (Mode != TcgInputMode.Blocking)
            return false;

        var blocker = board.Players[(int)board.YourSeat].Battlefield.Find(c => c.Id == blockerId);
        var attacker = board.Players[(int)TcgMatchRules.Other(board.YourSeat)].Battlefield.Find(c => c.Id == attackerId);

        if ((blocker is null) || (attacker is null) || !board.Attackers.Contains(attackerId) || !TcgMatchRules.CanBlock(blocker, attacker, lookup))
            return false;

        BlockByBlocker[blockerId] = attackerId;

        return true;
    }

    public void Unassign(int blockerId) => BlockByBlocker.Remove(blockerId);

    public void SelectBlocker(TcgDisplayArgs board, int creatureId)
    {
        if (Mode != TcgInputMode.Blocking)
            return;

        if (BlockByBlocker.ContainsKey(creatureId) || (SelectedBlocker == creatureId))
        {
            BlockByBlocker.Remove(creatureId);
            SelectedBlocker = 0;

            return;
        }

        var blocker = board.Players[(int)board.YourSeat].Battlefield.Find(c => c.Id == creatureId);

        if (blocker is null)
        {
            SelectedBlocker = 0;

            return;
        }

        SelectedBlocker = TcgMatchRules.CanBlockAny(board, blocker, lookup) ? creatureId : 0;
    }

    public bool PairSelectedBlocker(TcgDisplayArgs board, int attackerId)
    {
        if ((Mode != TcgInputMode.Blocking) || (SelectedBlocker == 0))
            return false;

        if (!AssignBlock(board, SelectedBlocker, attackerId))
            return false;

        SelectedBlocker = 0;

        return true;
    }

    public void ClearBlockSelection() => SelectedBlocker = 0;

    public int IncomingDamage(TcgDisplayArgs board)
    {
        if (board.Players.Count < 2)
            return 0;

        var you = board.Players[(int)board.YourSeat];

        if (you.Shielded)
            return 0;

        var them = board.Players[(int)TcgMatchRules.Other(board.YourSeat)];
        var total = 0;

        foreach (var attackerId in board.Attackers)
        {
            TcgCreatureView? attacker = null;

            for (var i = 0; i < them.Battlefield.Count; i++)
            {
                if (them.Battlefield[i].Id == attackerId)
                {
                    attacker = them.Battlefield[i];

                    break;
                }
            }

            if (attacker is null)
                continue;

            var card = lookup(attacker.Key);
            var trample = (card is not null) && ((card.Keywords & TcgKeyword.Trample) != 0);

            if (!trample)
            {
                var blocked = false;

                for (var i = 0; i < you.Battlefield.Count; i++)
                {
                    if (BlockByBlocker.GetValueOrDefault(you.Battlefield[i].Id, 0) == attackerId)
                    {
                        blocked = true;

                        break;
                    }
                }

                if (blocked)
                    continue;

                total += attacker.Attack;

                continue;
            }

            var venom = (card is not null) && ((card.Keywords & TcgKeyword.Venom) != 0);
            var strikesFirst = (card is not null) && ((card.Keywords & TcgKeyword.FirstStrike) != 0);
            var blockedCount = 0;
            var absorbed = 0;
            var firstStrikeDamage = 0;
            var firstStrikeVenom = false;

            for (var i = 0; i < you.Battlefield.Count; i++)
            {
                var blocker = you.Battlefield[i];

                if (BlockByBlocker.GetValueOrDefault(blocker.Id, 0) != attackerId)
                    continue;

                blockedCount++;
                absorbed += venom ? 1 : Math.Max(0, blocker.Health - blocker.Damage);

                if (!strikesFirst && HasKeyword(blocker.Key, TcgKeyword.FirstStrike))
                {
                    firstStrikeDamage += blocker.Attack;
                    firstStrikeVenom |= HasKeyword(blocker.Key, TcgKeyword.Venom) && (blocker.Attack > 0);
                }
            }

            if (blockedCount == 0)
            {
                total += attacker.Attack;

                continue;
            }

            if (firstStrikeVenom || (firstStrikeDamage >= attacker.Health - attacker.Damage))
                continue;

            total += Math.Max(0, attacker.Attack - absorbed);
        }

        return total;
    }

    public string IncomingDamageText(TcgDisplayArgs board) => IncomingDamageText(board, IncomingDamage(board));

    public string IncomingDamageText(TcgDisplayArgs board, int damage)
        => (board.Attackers.Count > 0) && (damage == 0) ? (IsShielded(board) ? "Phased" : "All blocked") : $"Incoming: {damage} damage";

    private static bool IsShielded(TcgDisplayArgs board)
        => (board.Players.Count > (int)board.YourSeat) && board.Players[(int)board.YourSeat].Shielded;

    private bool HasKeyword(string key, TcgKeyword keyword) => lookup(key) is { } card && ((card.Keywords & keyword) != 0);

    public void ClickBlocker(int blockerId)
    {
        if ((Mode == TcgInputMode.Ordering) && !Order.Contains(blockerId))
            Order.Add(blockerId);
    }

    public void ToggleRedraw(int cardId)
    {
        if (!PutBack.Remove(cardId))
            PutBack.Add(cardId);
    }

    public void AskConcede()
    {
        if (Mode != TcgInputMode.ConfirmConcede)
            BeforeConcede = Mode;

        Mode = TcgInputMode.ConfirmConcede;
    }

    public TcgActionArgs? ConfirmConcede()
        => Mode == TcgInputMode.ConfirmConcede ? Done(new TcgActionArgs { Type = TcgActionType.Concede }) : null;

    public TcgActionArgs? Confirm(TcgDisplayArgs board)
    {
        switch (Mode)
        {
            case TcgInputMode.Attacking:
                return ChosenAttackers.Count == 0
                    ? null
                    : Done(new TcgActionArgs { Type = TcgActionType.DeclareAttackers, CardIds = [..ChosenAttackers] });
            case TcgInputMode.Blocking:
                SelectedBlocker = 0;

                return new TcgActionArgs
                {
                    Type = TcgActionType.DeclareBlockers,
                    Blocks = BlockByBlocker.Select(pair => new TcgBlock(pair.Key, pair.Value)).ToList()
                };
            case TcgInputMode.Ordering:
                var blockers = board.Blocks.Count(block => block.AttackerId == OrderingAttacker);

                if (Order.Count != blockers)
                    return null;

                Ordered.Add(OrderingAttacker);
                LastOrdered = OrderingAttacker;

                return new TcgActionArgs { Type = TcgActionType.OrderDamage, CardId = OrderingAttacker, CardIds = [..Order] };
            case TcgInputMode.Redraw:
                return Done(new TcgActionArgs { Type = TcgActionType.Redraw, CardIds = [..PutBack] });
            default:
                return null;
        }
    }

    public void Cancel()
    {
        Picked.Clear();

        switch (Mode)
        {
            case TcgInputMode.Blocking:
                BlockByBlocker.Clear();
                SelectedBlocker = 0;

                break;
            case TcgInputMode.Ordering:
                Order.Clear();

                break;
            case TcgInputMode.Redraw:
                PutBack.Clear();

                break;
            case TcgInputMode.ConfirmConcede:
                Mode = BeforeConcede;

                break;
            default:
                Mode = TcgInputMode.Idle;
                Targets = [];

                break;
        }
    }

    private TcgActionArgs Done(TcgActionArgs action)
    {
        Mode = TcgInputMode.Idle;
        Targets = [];

        return action;
    }

    private TcgActionArgs Paid(TcgActionArgs action, TcgCatalogCardView card, int extraGeneric)
    {
        action.ManaIds = [..Picked];
        WrongManaNote = Picked.Count == 0 ? null : $"Those gems don't pay for {card.Name} (needs {TcgMatchRules.CostText(card, extraGeneric)}).";
        Picked.Clear();

        return Done(action);
    }

    private bool IsWardCreature(TcgDisplayArgs board, int targetId)
    {
        if (board.Players.Count < 2)
            return false;

        foreach (var seat in new[] { board.YourSeat, TcgMatchRules.Other(board.YourSeat) })
        {
            var creature = board.Players[(int)seat].Battlefield.Find(c => c.Id == targetId);

            if (creature is not null)
                return HasKeyword(creature.Key, TcgKeyword.Ward);
        }

        return false;
    }

    private static TcgCardView? HandCard(TcgDisplayArgs board, int cardId)
        => board.Players[(int)board.YourSeat].Hand.Find(c => c.Id == cardId);
}
