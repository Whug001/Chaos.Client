#region
using Chaos.Client.Controls.World.Popups.Tcg;
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Geometry.Abstractions.Definitions;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Popups.Tcg.Match;

/// <summary>Short event animations, played in order while the pre-update board stays on screen.</summary>
public sealed partial class TcgMatchWindow
{
    private const int RIVER_Y = 212;

    private readonly HashSet<int> AnimatedIds = [];
    private readonly HashSet<int> DestroyedIds = [];
    private readonly HashSet<int> FreshIds = [];
    private TcgCardFace? AnimFace;
    private TcgEventView? TurnBanner;
    private TcgDisplayArgs? TurnBannerBoard;
    private TcgDisplayArgs? TurnBannerSeenBoard;
    private double TurnBannerElapsedMs;

    /// <summary>Creature ids the board pass must skip this frame: only the ones the current animation draws itself.</summary>
    public static HashSet<int> AnimatedIdsFor(TcgEventView? e)
    {
        var ids = new HashSet<int>();

        if (e is null)
            return ids;

        var id = e.Type switch
        {
            TcgEventType.CreatureEntered   => e.SourceId != 0 ? e.SourceId : e.TargetId,
            TcgEventType.Attacked          => e.SourceId != 0 ? e.SourceId : e.TargetId,
            TcgEventType.CreatureDestroyed => e.SourceId != 0 ? e.SourceId : e.TargetId,
            TcgEventType.Blocked           => e.SourceId != 0 ? e.SourceId : e.TargetId,
            _                              => 0
        };

        if (id > 0)
            ids.Add(id);

        return ids;
    }

    /// <summary>
    ///     Sorts already-played events of the playing entry: destroyed ids stay skipped, entered ids and finished
    ///     attackers are drawn from the new board so earlier events are not undone on screen.
    /// </summary>
    public static void SettledIds(IEnumerable<TcgEventView> past, HashSet<int> destroyed, HashSet<int> fresh)
    {
        foreach (var e in past)
        {
            var id = e.SourceId != 0 ? e.SourceId : e.TargetId;

            if (id <= 0)
                continue;

            switch (e.Type)
            {
                case TcgEventType.CreatureDestroyed:
                    destroyed.Add(id);
                    fresh.Remove(id);

                    break;
                case TcgEventType.CreatureEntered:
                case TcgEventType.Attacked:
                    if (!destroyed.Contains(id))
                        fresh.Add(id);

                    break;
            }
        }
    }

    public static TcgBoardCreature.Pose AttackPose(float p) => p >= 0.7f ? TcgBoardCreature.Pose.Attack : TcgBoardCreature.Pose.Walk;

    /// <summary>Attack run: start to the river, then to the target, holding there while striking.</summary>
    public static Point AttackedFeet(Point start, Point river, Point target, float p)
    {
        if (p < 0.4f)
            return LerpPoint(start, river, p / 0.4f);

        if (p < 0.7f)
            return LerpPoint(river, target, (p - 0.4f) / 0.3f);

        return target;
    }

    /// <summary>A blocker leaning 12 px toward its attacker.</summary>
    public static Point BlockedFeet(Point blocker, Point attacker)
    {
        var dx = attacker.X - blocker.X;
        var dy = attacker.Y - blocker.Y;
        var length = Math.Sqrt((dx * dx) + (dy * dy));

        if (length <= 0)
            return blocker;

        return new Point(blocker.X + (int)(dx / length * 12), blocker.Y + (int)(dy / length * 12));
    }

    /// <summary>True when the card is an ante stake that was staked as a foil.</summary>
    public static bool IsFoilAnte(TcgDisplayArgs board, string? key)
        => !string.IsNullOrEmpty(key) && board.Ante.Any(ante => ante.Foil && (ante.Key == key));

    /// <summary>tcgbanner.spf frame for an outcome: Victory 0, Defeat 1, Draw 2.</summary>
    public static int BannerFrame(TcgEndOutcome outcome)
        => outcome switch
        {
            TcgEndOutcome.Victory => 0,
            TcgEndOutcome.Defeat  => 1,
            _                    => 2
        };

    /// <summary>The turn banner names the seat whose turn starts; input already follows the newest Update.</summary>
    public static string TurnBannerText(TcgDisplayArgs? board, TcgEventView e)
    {
        var name = board is not null && board.Players.Count > (int)e.Seat ? board.Players[(int)e.Seat].Name : string.Empty;

        if (!string.IsNullOrEmpty(name))
            return $"{name}'s turn!";

        return e.Seat == board?.YourSeat ? "Your turn!" : "Opponent's turn!";
    }

    private static Point LerpPoint(Point from, Point to, float t)
        => new(from.X + (int)((to.X - from.X) * t), from.Y + (int)((to.Y - from.Y) * t));

    private void DrawEventAnimation(SpriteBatch batch)
    {
        if ((Queue.Current is not { } e) || (SeatsBoard() is not { } board) || (board.Players.Count < 2))
            return;

        var p = Queue.Progress;
        var clockMs = (int)(Environment.TickCount64 % int.MaxValue);

        switch (e.Type)
        {
            case TcgEventType.CreatureEntered:
                DrawEnterAnimation(batch, e, p, clockMs);

                break;
            case TcgEventType.CardCast:
                DrawCastAnimation(batch, board, e, p);

                break;
            case TcgEventType.TrapRevealed:
                DrawTrapAnimation(batch, board, e, p);

                break;
            case TcgEventType.Attacked:
                DrawAttackAnimation(batch, board, e, p, clockMs);

                break;
            case TcgEventType.Damaged:
            case TcgEventType.PlayerDamaged:
                DrawDamageNumber(batch, board, e, p, true);

                break;
            case TcgEventType.LifeGained:
                DrawDamageNumber(batch, board, e, p, false);

                break;
            case TcgEventType.CreatureDestroyed:
                DrawDestroyAnimation(batch, e, p, clockMs);

                break;
            case TcgEventType.Blocked:
                DrawBlockAnimation(batch, board, e, clockMs);

                break;
        }
    }

    private TcgDisplayArgs? SeatsBoard() => Queue.PlayingBoard ?? Queue.ShownBoard ?? State.Latest;

    private void DrawEnterAnimation(SpriteBatch batch, TcgEventView e, float p, int clockMs)
    {
        if (!TryFindCreature(e.SourceId != 0 ? e.SourceId : e.TargetId, out var card, out var feet, out var facing))
            return;

        DrawPuff(batch, feet, p);
        Creature.Draw(batch, card, feet, facing, TcgBoardCreature.Pose.Idle, clockMs, Color.White, 0.4f + (0.6f * p));
    }

    private void DrawCastAnimation(SpriteBatch batch, TcgDisplayArgs board, TcgEventView e, float p)
    {
        if (Catalog.Get(e.Key) is not { } card)
            return;

        var yours = e.Seat == board.YourSeat;
        var start = yours ? TcgMatchLayout.Hand.Center : TcgMatchLayout.OpponentStrip.Center;
        var from = new Point(start.X - (TcgCardFace.WIDTH / 2), start.Y - (TcgCardFace.HEIGHT / 2));
        var at = LerpPoint(from, new Point(TcgMatchLayout.BigCard.X, TcgMatchLayout.BigCard.Y), p);

        AnimFace ??= new TcgCardFace(Creatures, 1);
        AnimFace.Card = card;
        AnimFace.Foil = false;
        AnimFace.FaceDown = false;
        AnimFace.FlipProgress = 1f;
        AnimFace.Badge = string.Empty;
        AnimFace.Owned = true;
        AnimFace.Selected = false;
        AnimFace.X = at.X;
        AnimFace.Y = at.Y;
        AnimFace.Draw(batch);
    }

    private void DrawTrapAnimation(SpriteBatch batch, TcgDisplayArgs board, TcgEventView e, float p)
    {
        if (Catalog.Get(e.Key) is not { } card)
            return;

        var row = e.Seat == board.YourSeat ? TcgMatchLayout.YourTraps : TcgMatchLayout.OpponentTraps;
        var slot = TcgMatchLayout.Trap(row, 0);

        AnimFace ??= new TcgCardFace(Creatures, 1);
        AnimFace.Card = card;
        AnimFace.Foil = false;
        AnimFace.FaceDown = false;
        AnimFace.FlipProgress = p;
        AnimFace.Badge = string.Empty;
        AnimFace.Owned = true;
        AnimFace.Selected = false;
        AnimFace.X = slot.X;
        AnimFace.Y = slot.Y;
        AnimFace.Draw(batch);
    }

    private void DrawAttackAnimation(SpriteBatch batch, TcgDisplayArgs board, TcgEventView e, float p, int clockMs)
    {
        var attackerId = e.SourceId != 0 ? e.SourceId : e.TargetId;

        if (!TryFindCreature(attackerId, out var card, out var start, out var facing))
            return;

        var river = new Point(start.X, ScreenY + RIVER_Y);
        var target = AttackTarget(attackerId, start);
        var feet = AttackedFeet(start, river, target, p);

        Creature.Draw(batch, card, feet, facing, AttackPose(p), clockMs, Color.White);
    }

    private Point AttackTarget(int attackerId, Point start)
    {
        var blockerId = Queue.ShownBoard?.Blocks.FirstOrDefault(block => block.AttackerId == attackerId)?.BlockerId ?? 0;

        if ((blockerId != 0) && TryFindCreature(blockerId, out _, out var blockerFeet, out _))
            return blockerFeet;

        //no blocker: the far strip. An attacker above the river strikes down, one below strikes up.
        var stripY = start.Y < ScreenY + RIVER_Y
            ? TcgMatchLayout.YourStrip.Y + (TcgMatchLayout.YourStrip.Height / 2)
            : TcgMatchLayout.OpponentStrip.Height / 2;

        return new Point(start.X, ScreenY + stripY);
    }

    private void DrawDamageNumber(SpriteBatch batch, TcgDisplayArgs board, TcgEventView e, float p, bool damage)
    {
        Point at;

        if (damage && (e.Type == TcgEventType.Damaged) && TryFindCreature(e.TargetId, out _, out var feet, out _))
            at = new Point(feet.X, feet.Y - 56);
        else
        {
            var seal = e.Seat == board.YourSeat ? TcgMatchLayout.YourSeal : TcgMatchLayout.OpponentSeal;
            at = new Point(ScreenX + seal.Center.X, ScreenY + seal.Center.Y - 8);
        }

        var text = damage ? $"-{e.Amount}" : $"+{e.Amount}";
        var color = (damage ? Color.Red : Color.Green) * (1 - p);
        var x = at.X - (TextRenderer.MeasureWidth(text) / 2);
        DrawTextClipped(batch, new Vector2(x, at.Y - (20 * p)), text, color, false);
    }

    /// <summary>
    ///     Starts the turn banner when a board carrying a TurnStarted event is shown, then times it out. The banner never
    ///     waits in the event queue, so the new board and its input are live while it fades.
    /// </summary>
    private void SyncTurnBanner(double elapsedMs)
    {
        var shown = Queue.ShownBoard ?? State.Latest;

        if (!ReferenceEquals(shown, TurnBannerSeenBoard))
        {
            TurnBannerSeenBoard = shown;

            if (shown?.Events.FirstOrDefault(e => e.Type == TcgEventType.TurnStarted) is { } started)
            {
                TurnBanner = started;
                TurnBannerBoard = shown;
                TurnBannerElapsedMs = 0;
            }
        }

        if (TurnBanner is not null)
        {
            TurnBannerElapsedMs += elapsedMs;

            if (TurnBannerElapsedMs >= TcgMatchEventQueue.DurationMs(TcgEventType.TurnStarted))
                TurnBanner = null;
        }
    }

    private void DrawTurnBanner(SpriteBatch batch)
    {
        if ((TurnBanner is not { } e) || (TurnBannerBoard is not { } board))
            return;

        var duration = TcgMatchEventQueue.DurationMs(TcgEventType.TurnStarted);
        var p = duration <= 0 ? 1f : (float)Math.Min(1, TurnBannerElapsedMs / duration);

        DrawTurnAnimation(batch, board, e, p);
    }

    private void DrawTurnAnimation(SpriteBatch batch, TcgDisplayArgs board, TcgEventView e, float p)
    {
        var text = TurnBannerText(board, e);

        if (string.IsNullOrEmpty(text))
            return;

        const int scale = 2;
        var width = TextRenderer.MeasureWidth(text, scale);
        var height = TextRenderer.CHAR_HEIGHT * scale;
        var x = ScreenX + ((TcgMatchLayout.Board.Width - width) / 2);
        var y = ScreenY + RIVER_Y - (height / 2);

        float alpha;

        if (p < 0.15f)
            alpha = p / 0.15f;
        else if (p > 0.7f)
            alpha = Math.Max(0, (1 - p) / 0.3f);
        else
            alpha = 1f;

        DrawRectClipped(batch, new Rectangle(x - 8, y - 4, width + 16, height + 8), new Color(0, 0, 0, (int)(180 * alpha)));
        TextRenderer.DrawShadowedText(batch, new Vector2(x, y), text, Color.Gold * alpha, Color.Black * alpha, false, scale);
    }

    private void DrawDestroyAnimation(SpriteBatch batch, TcgEventView e, float p, int clockMs)
    {
        if (!TryFindCreature(e.SourceId != 0 ? e.SourceId : e.TargetId, out var card, out var feet, out var facing))
            return;

        Color tint;

        if (p < 0.25f)
            tint = ((int)(p * 10) % 2 == 0) ? Color.Red : Color.White;
        else
            tint = Color.White * (1 - ((p - 0.25f) / 0.75f));

        Creature.Draw(batch, card, feet, facing, TcgBoardCreature.Pose.Idle, clockMs, tint);
        DrawPuff(batch, feet, p);
    }

    private void DrawPuff(SpriteBatch batch, Point feet, float p)
    {
        if (TcgMatchArt.ArtPresent() && (UiRenderer.Instance is { } ui))
        {
            var frame = Math.Clamp((int)(p * TcgMatchArt.PUFF_FRAMES), 0, TcgMatchArt.PUFF_FRAMES - 1);
            DrawTextureFitted(batch, ui.GetSpfTexture(TcgMatchArt.PUFF, frame), new Rectangle(feet.X - 16, feet.Y - 56, 32, 32), Color.White);
        }
    }

    private void DrawBlockAnimation(SpriteBatch batch, TcgDisplayArgs board, TcgEventView e, int clockMs)
    {
        var blockerId = e.SourceId != 0 ? e.SourceId : e.TargetId;

        if (!TryFindCreature(blockerId, out var card, out var feet, out var facing))
            return;

        var attackerId = e.TargetId != 0
            ? e.TargetId
            : Queue.ShownBoard?.Blocks.FirstOrDefault(block => block.BlockerId == blockerId)?.AttackerId ?? 0;

        if ((attackerId != 0) && TryFindCreature(attackerId, out _, out var attackerFeet, out _))
            feet = BlockedFeet(feet, attackerFeet);

        Creature.Draw(batch, card, feet, facing, TcgBoardCreature.Pose.Idle, clockMs, Color.White);
    }

    private bool TryFindCreature(int id, out TcgCatalogCardView card, out Point feet, out Direction facing)
    {
        card = null!;
        feet = default;
        facing = Direction.Down;

        if (id == 0)
            return false;

        foreach (var board in new[] { Queue.ShownBoard, Queue.PlayingBoard })
        {
            if (board is null || (board.Players.Count < 2))
                continue;

            foreach (var seat in new[] { TcgSeat.A, TcgSeat.B })
            {
                var creatures = board.Players[(int)seat].Battlefield;
                var index = creatures.FindIndex(creature => creature.Id == id);

                if (index < 0)
                    continue;

                if (Catalog.Get(creatures[index].Key) is not { } found)
                    return false;

                var yours = seat == board.YourSeat;
                var slot = TcgMatchLayout.CreatureSlot(index, creatures.Count, yours);
                card = found;
                feet = new Point(ScreenX + slot.X, ScreenY + slot.Y);
                facing = yours ? Direction.Up : Direction.Down;

                return true;
            }
        }

        return false;
    }
}
