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

    public void Sync(TcgDisplayArgs board)
    {
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
            return Done(new TcgActionArgs { Type = TcgActionType.SetTrap, CardId = SelectedCard });

        if (!TcgMatchRules.NeedsTarget(card))
            return Done(new TcgActionArgs { Type = TcgActionType.Cast, CardId = SelectedCard });

        Targets = TcgMatchRules.LegalTargets(board, card, lookup);
        Mode = TcgInputMode.Targeting;

        return null;
    }

    public TcgActionArgs? ChooseTarget(int targetId)
        => (Mode == TcgInputMode.Targeting) && Targets.Contains(targetId)
            ? Done(new TcgActionArgs { Type = TcgActionType.Cast, CardId = SelectedCard, TargetId = targetId })
            : null;

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

        SelectedBlocker = board.Players[(int)TcgMatchRules.Other(board.YourSeat)]
                               .Battlefield
                               .Where(attacker => board.Attackers.Contains(attacker.Id) && TcgMatchRules.CanBlock(blocker, attacker, lookup))
                               .Any()
            ? creatureId
            : 0;
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
        var them = board.Players[(int)TcgMatchRules.Other(board.YourSeat)];
        var total = 0;

        foreach (var attackerId in board.Attackers)
        {
            var attacker = them.Battlefield.Find(c => c.Id == attackerId);

            if (attacker is null)
                continue;

            var blockers = you.Battlefield.Where(c => BlockByBlocker.GetValueOrDefault(c.Id, 0) == attackerId).ToList();

            if (blockers.Count == 0)
            {
                total += attacker.Attack;

                continue;
            }

            if ((lookup(attacker.Key) is { } card) && ((card.Keywords & TcgKeyword.Trample) != 0))
            {
                var venom = (card.Keywords & TcgKeyword.Venom) != 0;
                var absorbed = blockers.Sum(blocker => venom ? 1 : Math.Max(0, blocker.Health - blocker.Damage));
                total += Math.Max(0, attacker.Attack - absorbed);
            }
        }

        return total;
    }

    public string IncomingDamageText(TcgDisplayArgs board)
        => (board.Attackers.Count > 0) && (IncomingDamage(board) == 0) ? "All blocked" : $"Incoming: {IncomingDamage(board)} damage";

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

    private static TcgCardView? HandCard(TcgDisplayArgs board, int cardId)
        => board.Players[(int)board.YourSeat].Hand.Find(c => c.Id == cardId);
}
