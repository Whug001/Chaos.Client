#region
using Chaos.Client.Controls.Components;
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Popups.Tcg.Match;

/// <summary>Attack selection, drag-to-block, damage order clicks and numbering.</summary>
public sealed partial class TcgMatchWindow
{
    private const int RIVER_LIFT = 24;
    private const int BLOCK_FRONT_Y = 44;
    private const int BLOCK_SPREAD_X = 40;

    private static readonly Color UnblockedArrow = new(220, 50, 50);
    private static readonly Color BlockedArrow = new(255, 176, 0);

    public static List<string> CombatButtonLabels(TcgInputMode mode)
        => mode switch
        {
            TcgInputMode.Attacking => ["Confirm", "Cancel"],
            TcgInputMode.Blocking  => ["Confirm", "Cancel"],
            TcgInputMode.Ordering  => ["Confirm", "Cancel"],
            _                      => []
        };

    public static bool ConfirmEnabled(
        TcgDisplayArgs board,
        TcgMatchInput input,
        Func<string, TcgCatalogCardView?> lookup)
        => input.Mode switch
        {
            TcgInputMode.Attacking => input.Attackers.Count > 0,
            TcgInputMode.Blocking  => !HasGuardProblem(board, input.Blocks, lookup),
            TcgInputMode.Ordering  => OrderComplete(board, input.OrderingAttacker, input.DamageOrder),
            _                      => false
        };

    public static TcgReadyGlow ReadyGlow(
        TcgDisplayArgs board,
        TcgMatchInput input,
        TcgCreatureView creature,
        int hoveredId,
        Func<string, TcgCatalogCardView?> lookup)
    {
        if ((board.Players.Count < 2) || !board.Players[(int)board.YourSeat].Battlefield.Any(own => own.Id == creature.Id))
            return TcgReadyGlow.None;

        if (TcgMatchRules.CanAttack(board, creature))
            return (input.Mode == TcgInputMode.Attacking) && input.Attackers.Contains(creature.Id) ? TcgReadyGlow.None : TcgReadyGlow.Attack;

        if ((input.Mode != TcgInputMode.Blocking) || !TcgMatchRules.YouDecide(board, TcgDecision.Block) || input.Blocks.ContainsKey(creature.Id))
            return TcgReadyGlow.None;

        var attackers = board.Players[(int)TcgMatchRules.Other(board.YourSeat)]
                             .Battlefield
                             .Where(attacker => board.Attackers.Contains(attacker.Id))
                             .ToList();

        if (attackers.Any(attacker => attacker.Id == hoveredId))
            attackers = attackers.Where(attacker => attacker.Id == hoveredId).ToList();

        return attackers.Any(attacker => TcgMatchRules.CanBlock(creature, attacker, lookup)) ? TcgReadyGlow.Block : TcgReadyGlow.None;
    }

    public static bool HasGuardProblem(
        TcgDisplayArgs board,
        IReadOnlyDictionary<int, int> blocks,
        Func<string, TcgCatalogCardView?> lookup)
        => TcgMatchRules.GuardProblem(board, blocks.Select(pair => new TcgBlock(pair.Key, pair.Value)).ToList(), lookup);

    public static bool OrderComplete(TcgDisplayArgs board, int attackerId, IReadOnlyList<int> order)
    {
        var blockers = board.Blocks.Count(block => block.AttackerId == attackerId);

        return (blockers > 0) && (order.Count == blockers);
    }

    public static Point DrawFeet(
        TcgDisplayArgs board,
        TcgMatchInput input,
        int creatureId,
        bool yours,
        int index,
        int count)
    {
        var feet = TcgMatchLayout.CreatureSlot(index, count, yours);

        if (yours)
        {
            if ((input.Mode == TcgInputMode.Attacking) && input.Attackers.Contains(creatureId))
                return new Point(feet.X, feet.Y - RIVER_LIFT);

            if ((input.Mode == TcgInputMode.Blocking) && input.Blocks.TryGetValue(creatureId, out var attackerId))
                return BlockerFeet(ShiftedAttackerFeet(board, attackerId), BlockerIndex(board, input, creatureId, attackerId), BlockCount(input, attackerId));

            if ((input.Mode == TcgInputMode.Blocking) && (input.SelectedBlocker == creatureId))
                return new Point(feet.X, feet.Y - RIVER_LIFT);
        } else if ((input.Mode == TcgInputMode.Blocking) && board.Attackers.Contains(creatureId))
            return new Point(feet.X, feet.Y + RIVER_LIFT);

        return feet;
    }

    public static Point BlockerFeet(Point attackerFeet, int index, int count)
        => new(attackerFeet.X + ((index - ((count - 1) / 2)) * BLOCK_SPREAD_X), attackerFeet.Y + BLOCK_FRONT_Y);

    public static int OwnCreatureIdAt(TcgDisplayArgs board, TcgMatchInput input, int x, int y)
    {
        if (board.Players.Count < 2)
            return 0;

        var creatures = board.Players[(int)board.YourSeat].Battlefield;

        for (var i = creatures.Count - 1; i >= 0; i--)
        {
            var feet = DrawFeet(board, input, creatures[i].Id, true, i, creatures.Count);

            if (new Rectangle(feet.X - 24, feet.Y - 48, 48, 48).Contains(x, y))
                return creatures[i].Id;
        }

        return 0;
    }

    public static int AttackerIdAt(TcgDisplayArgs board, TcgMatchInput input, int x, int y)
    {
        if (board.Players.Count < 2)
            return 0;

        var creatures = board.Players[(int)TcgMatchRules.Other(board.YourSeat)].Battlefield;

        for (var i = creatures.Count - 1; i >= 0; i--)
        {
            if (!board.Attackers.Contains(creatures[i].Id))
                continue;

            var feet = DrawFeet(board, input, creatures[i].Id, false, i, creatures.Count);

            if (new Rectangle(feet.X - 24, feet.Y - 48, 48, 48).Contains(x, y))
                return creatures[i].Id;
        }

        return 0;
    }

    public static int OrderedBlockerIdAt(TcgDisplayArgs board, TcgMatchInput input, int x, int y)
    {
        if ((board.Players.Count < 2) || (input.Mode != TcgInputMode.Ordering))
            return 0;

        var creatures = board.Players[(int)TcgMatchRules.Other(board.YourSeat)].Battlefield;

        for (var i = creatures.Count - 1; i >= 0; i--)
        {
            if (!board.Blocks.Any(block => (block.AttackerId == input.OrderingAttacker) && (block.BlockerId == creatures[i].Id)))
                continue;

            var feet = TcgMatchLayout.CreatureSlot(i, creatures.Count, false);

            if (new Rectangle(feet.X - 24, feet.Y - 48, 48, 48).Contains(x, y))
                return creatures[i].Id;
        }

        return 0;
    }

    private static Point ShiftedAttackerFeet(TcgDisplayArgs board, int attackerId)
    {
        if (board.Players.Count < 2)
            return Point.Zero;

        var creatures = board.Players[(int)TcgMatchRules.Other(board.YourSeat)].Battlefield;
        var index = creatures.FindIndex(creature => creature.Id == attackerId);

        if (index < 0)
            return Point.Zero;

        var feet = TcgMatchLayout.CreatureSlot(index, creatures.Count, false);

        return new Point(feet.X, feet.Y + RIVER_LIFT);
    }

    private static int BlockerIndex(TcgDisplayArgs board, TcgMatchInput input, int blockerId, int attackerId)
    {
        if (board.Players.Count < 2)
            return 0;

        var order = board.Players[(int)board.YourSeat]
                         .Battlefield
                         .Where(creature => input.Blocks.GetValueOrDefault(creature.Id, 0) == attackerId)
                         .Select(creature => creature.Id)
                         .ToList();

        return Math.Max(0, order.IndexOf(blockerId));
    }

    private static int BlockCount(TcgMatchInput input, int attackerId)
        => Math.Max(1, input.Blocks.Count(pair => pair.Value == attackerId));

    private string? CreatureBadge(TcgDisplayArgs board, TcgCreatureView view)
    {
        if ((Input.Mode != TcgInputMode.Ordering) || !board.Blocks.Any(block => (block.AttackerId == Input.OrderingAttacker) && (block.BlockerId == view.Id)))
            return null;

        var order = Input.DamageOrder.ToList().IndexOf(view.Id);

        return order < 0 ? null : (order + 1).ToString();
    }

    private bool CreatureOutlined(TcgCreatureView view)
        => (Input.Mode == TcgInputMode.Ordering) && (view.Id == Input.OrderingAttacker);

    private void DrawOrderBadge(SpriteBatch batch, Point feet, string badge)
    {
        var at = new Rectangle(feet.X + 14, feet.Y - 66, 16, TextRenderer.CHAR_HEIGHT + 2);
        UIElement.DrawBorderedRect(batch, ToScreen(at), BoxBack, LegendColors.Gold);
        DrawTextClipped(batch, new Vector2(at.X + ScreenX + 4, at.Y + ScreenY + 1), badge, LegendColors.Gold, false);
    }

    private void DrawAttackerOutline(SpriteBatch batch, Point feet)
    {
        var at = ToScreen(new Rectangle(feet.X - 24, feet.Y - 48, 48, 48));
        UIElement.DrawBorder(batch, at, LegendColors.Gold);
        at.Inflate(-1, -1);

        if ((at.Width > 0) && (at.Height > 0))
            UIElement.DrawBorder(batch, at, LegendColors.Gold);
    }

    private Rectangle ToScreen(Rectangle local) => new(ScreenX + local.X, ScreenY + local.Y, local.Width, local.Height);

    private bool HandleCombatClick(TcgDisplayArgs board, TcgDisplayArgs latest, int x, int y)
    {
        switch (Input.Mode)
        {
            case TcgInputMode.Attacking:
                if (OwnCreatureIdAt(board, Input, x, y) is var id && (id != 0))
                {
                    Input.ToggleAttacker(latest, id);

                    return true;
                }

                return false;
            case TcgInputMode.Ordering:
                if (OrderedBlockerIdAt(board, Input, x, y) is var blocker && (blocker != 0))
                {
                    Input.ClickBlocker(blocker);

                    return true;
                }

                return false;
            case TcgInputMode.Blocking:
                if (OwnCreatureIdAt(board, Input, x, y) is var own && (own != 0))
                {
                    Input.SelectBlocker(latest, own);

                    return true;
                }

                if (AttackerIdAt(board, Input, x, y) is var attacker && (attacker != 0))
                {
                    Input.PairSelectedBlocker(latest, attacker);

                    return true;
                }

                if (Input.SelectedBlocker != 0)
                {
                    Input.ClearBlockSelection();

                    return true;
                }

                return false;
            default:
                return false;
        }
    }

    private void DrawBlockArrows(SpriteBatch batch, TcgDisplayArgs board)
    {
        if (board.Players.Count < 2)
            return;

        var you = board.Players[(int)board.YourSeat];
        var them = board.Players[(int)TcgMatchRules.Other(board.YourSeat)];
        var seal = new Point(ScreenX + TcgMatchLayout.YourSeal.Center.X, ScreenY + TcgMatchLayout.YourSeal.Center.Y);

        foreach (var attackerId in board.Attackers)
        {
            var attackerIndex = them.Battlefield.FindIndex(creature => creature.Id == attackerId);

            if (attackerIndex < 0)
                continue;

            var attackerFeet = DrawFeet(board, Input, attackerId, false, attackerIndex, them.Battlefield.Count);
            var from = new Point(ScreenX + attackerFeet.X, ScreenY + attackerFeet.Y - 24);
            var blockers = you.Battlefield.Where(creature => Input.Blocks.GetValueOrDefault(creature.Id, 0) == attackerId).ToList();

            if (blockers.Count == 0)
            {
                DrawArrow(batch, from, seal, UnblockedArrow);

                continue;
            }

            foreach (var blocker in blockers)
            {
                var blockerIndex = you.Battlefield.FindIndex(creature => creature.Id == blocker.Id);

                if (blockerIndex < 0)
                    continue;

                var blockerFeet = DrawFeet(board, Input, blocker.Id, true, blockerIndex, you.Battlefield.Count);
                DrawArrow(batch, from, new Point(ScreenX + blockerFeet.X, ScreenY + blockerFeet.Y - 24), BlockedArrow);
            }
        }
    }

    private void DrawIncomingDamage(SpriteBatch batch, TcgDisplayArgs board)
    {
        var damage = Input.IncomingDamage(board);
        var text = Input.IncomingDamageText(board);
        DrawTextClipped(batch, new Vector2(ScreenX + 8, ScreenY + 338), text, damage == 0 ? Color.Green : Color.Red, false);
    }

    private void DrawArrow(SpriteBatch batch, Point from, Point to, Color color)
    {
        var dx = (double)(to.X - from.X);
        var dy = (double)(to.Y - from.Y);
        var length = Math.Sqrt((dx * dx) + (dy * dy));

        if (length < 10)
            return;

        var ux = dx / length;
        var uy = dy / length;
        var tip = new Point((int)(to.X - (ux * 8)), (int)(to.Y - (uy * 8)));
        DrawDotLine(batch, from, tip, color);

        var tipX = (double)tip.X;
        var tipY = (double)tip.Y;
        DrawDotLine(batch, tip, new Point((int)(tipX - (ux * 10) - (uy * 6)), (int)(tipY - (uy * 10) + (ux * 6))), color);
        DrawDotLine(batch, tip, new Point((int)(tipX - (ux * 10) + (uy * 6)), (int)(tipY - (uy * 10) - (ux * 6))), color);
    }

    private void DrawDotLine(SpriteBatch batch, Point from, Point to, Color color)
    {
        var dx = to.X - from.X;
        var dy = to.Y - from.Y;
        var steps = Math.Max(Math.Abs(dx), Math.Abs(dy));

        if (steps == 0)
        {
            DrawRectClipped(batch, new Rectangle(from.X - 1, from.Y - 1, 3, 3), color);

            return;
        }

        for (var i = 0; i <= steps; i += 2)
            DrawRectClipped(batch, new Rectangle(from.X + ((dx * i) / steps) - 1, from.Y + ((dy * i) / steps) - 1, 3, 3), color);
    }

    private bool HandleBlockDrop(int blockerId, int x, int y)
    {
        if ((State.Latest is not { } latest) || (Input.Mode != TcgInputMode.Blocking))
            return false;

        if (AttackerIdAt(Queue.ShownBoard ?? latest, Input, x, y) is var attacker && (attacker != 0))
            Input.AssignBlock(latest, blockerId, attacker);
        else
            Input.Unassign(blockerId);

        Input.ClearBlockSelection();

        return true;
    }
}

public enum TcgReadyGlow
{
    None,
    Attack,
    Block
}
