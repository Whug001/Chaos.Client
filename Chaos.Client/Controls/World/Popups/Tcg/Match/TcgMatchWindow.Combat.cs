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
            default:
                return false;
        }
    }

    private bool HandleBlockDrop(int blockerId, int x, int y)
    {
        if ((State.Latest is not { } latest) || (Input.Mode != TcgInputMode.Blocking))
            return false;

        if (AttackerIdAt(Queue.ShownBoard ?? latest, Input, x, y) is var attacker && (attacker != 0))
            Input.AssignBlock(latest, blockerId, attacker);
        else
            Input.Unassign(blockerId);

        return true;
    }
}
