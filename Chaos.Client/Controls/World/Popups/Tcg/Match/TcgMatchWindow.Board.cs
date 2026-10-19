#region
using Chaos.Client.Controls.Components;
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Geometry.Abstractions.Definitions;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Popups.Tcg.Match;

public sealed partial class TcgMatchWindow
{
    private static readonly Color BoardFallback = new(46, 32, 20);
    private static readonly Color StripFallback = new(58, 40, 24);
    private static readonly Color ColumnFallback = new(24, 18, 12);
    private static readonly Color PlateBack = new(0, 0, 0, 200);
    private static readonly Color TappedTint = new(150, 150, 150);
    private static readonly Color BoxBack = new(20, 14, 8);
    private static readonly Color DisabledBack = new(12, 12, 12);
    private static readonly Color BlockGlow = new(90, 170, 255);

    private Rectangle At(Rectangle rect) => new(ScreenX + rect.X, ScreenY + rect.Y, rect.Width, rect.Height);

    private void DrawBackground(SpriteBatch batch)
    {
        if (TcgMatchArt.ArtPresent() && (UiRenderer.Instance is { } ui))
            DrawTextureFitted(batch, ui.GetSpfTexture(TcgMatchArt.TABLE), At(new Rectangle(0, 0, 640, 480)), Color.White);
        else
        {
            DrawRectClipped(batch, At(TcgMatchLayout.Board), BoardFallback);
            DrawRectClipped(batch, At(TcgMatchLayout.OpponentStrip), StripFallback);
            DrawRectClipped(batch, At(TcgMatchLayout.YourStrip), StripFallback);
            DrawRectClipped(batch, At(TcgMatchLayout.Column), ColumnFallback);
        }
    }

    private void DrawBoard(SpriteBatch batch, TcgDisplayArgs board, TcgDisplayArgs? latest)
    {
        if (board.Players.Count < 2)
            return;

        var clockMs = (int)(Environment.TickCount64 % int.MaxValue);
        var you = board.Players[(int)board.YourSeat];
        var them = board.Players[(int)TcgMatchRules.Other(board.YourSeat)];

        DrawOpponentStrip(batch, them);

        if ((latest is not null) && (latest.Players.Count >= 2) && (Input.Mode == TcgInputMode.Blocking) && TcgMatchRules.YouDecide(latest, TcgDecision.Block))
        {
            var incoming = Input.IncomingDamage(latest);

            DrawBlockArrows(batch, latest);
            DrawIncomingDamage(batch, incoming, Input.IncomingDamageText(latest, incoming));
        }

        DrawCreatureRow(batch, board, them.Battlefield, false, clockMs);
        DrawCreatureRow(batch, board, you.Battlefield, true, clockMs);

        if (Queue.PlayingBoard is { } playing && (playing.Players.Count >= 2))
            DrawFresh(batch, playing, clockMs);

        DrawYourStrip(batch, board, you, clockMs);
        DrawButtons(batch);
    }

    private void DrawOpponentStrip(SpriteBatch batch, TcgPlayerView them)
    {
        DrawSeal(batch, TcgMatchLayout.OpponentSeal, them.Life, them.Shielded);

        for (var i = 0; i < them.Mana.Count; i++)
            DrawGem(batch, TcgMatchLayout.OpponentGems, i, them.Mana[i], true);

        DrawTrapBacks(batch, TcgMatchLayout.OpponentTraps, them.Traps.Count);

        var at = At(TcgMatchLayout.OpponentCounts);
        DrawTextClipped(batch, new Vector2(at.X, at.Y), Fit(them.Name, at.Width), LegendColors.Gold, false);
        DrawTextClipped(batch, new Vector2(at.X, at.Y + 14), $"Hand {them.HandCount}", Color.White, false);
        DrawTextClipped(batch, new Vector2(at.X, at.Y + 28), $"Deck {them.DeckCount}", Color.White, false);
    }

    private void DrawYourStrip(SpriteBatch batch, TcgDisplayArgs board, TcgPlayerView you, int clockMs)
    {
        DrawSeal(batch, TcgMatchLayout.YourSeal, you.Life, you.Shielded);

        for (var i = 0; i < you.Mana.Count; i++)
            DrawGem(batch, TcgMatchLayout.YourGems, i, you.Mana[i], true, Input.PickedMana.Contains(you.Mana[i].Id));

        DrawTrapBacks(batch, TcgMatchLayout.YourTraps, you.Traps.Count);

        var name = Fit(you.Name, 120);
        DrawTextClipped(batch, new Vector2(ScreenX + 52, ScreenY + 373), name, LegendColors.Gold, false);
        DrawTextClipped(
            batch,
            new Vector2(ScreenX + 58 + TextRenderer.MeasureWidth(name), ScreenY + 373),
            $"Deck {you.DeckCount}",
            Color.White,
            false);

        if (!State.NeedsRedraw && !State.Spectating)
            DrawHand(batch, board, you);
    }

    private void DrawSeal(SpriteBatch batch, Rectangle rect, int life, bool shielded)
    {
        var at = At(rect);

        if (TcgMatchArt.ArtPresent() && (UiRenderer.Instance is { } ui))
            DrawTextureFitted(batch, ui.GetSpfTexture(TcgMatchArt.SEAL), at, Color.White);
        else
            DrawRectClipped(batch, at, StripFallback);

        var text = life.ToString();
        var x = at.X + ((at.Width - TextRenderer.MeasureWidth(text)) / 2);
        var y = at.Y + ((at.Height - TextRenderer.CHAR_HEIGHT) / 2);
        DrawTextClipped(batch, new Vector2(x, y), text, Color.White, false);

        if (shielded)
            DrawShield(batch, at);
    }

    private static readonly Color ShieldFallback = new(185, 140, 255);

    private void DrawShield(SpriteBatch batch, Rectangle seal)
    {
        var marker = new Rectangle(seal.Right - 12, seal.Y - 4, 16, 16);

        if (UiRenderer.Instance is { } ui && (ui.GetSpfFrameCount(TcgMatchArt.SHIELD) >= 1))
            DrawTextureFitted(batch, ui.GetSpfTexture(TcgMatchArt.SHIELD), marker, Color.White);
        else
            DrawRectClipped(batch, marker, ShieldFallback);
    }

    private void DrawGem(SpriteBatch batch, Rectangle row, int index, TcgManaView mana, bool darkWhenTapped, bool picked = false)
    {
        var slot = TcgMatchLayout.Gem(row, index);
        var at = new Rectangle(ScreenX + slot.X, ScreenY + slot.Y - (picked ? 3 : 0), 16, 16);
        var frame = (int)mana.Element + ((darkWhenTapped && mana.Tapped) ? TcgMatchArt.DARK_GEM_OFFSET : 0);

        if (TcgMatchArt.ArtPresent() && (UiRenderer.Instance is { } ui))
            DrawTextureFitted(batch, ui.GetSpfTexture(TcgMatchArt.GEM, frame), at, Color.White);
        else
            DrawRectClipped(batch, at, mana.Tapped && darkWhenTapped ? DisabledBack : TcgCardFace.ElementColor(mana.Element));

        if (picked)
        {
            DrawRectClipped(batch, new Rectangle(at.X, at.Y, at.Width, 1), LegendColors.Gold);
            DrawRectClipped(batch, new Rectangle(at.X, at.Bottom - 1, at.Width, 1), LegendColors.Gold);
            DrawRectClipped(batch, new Rectangle(at.X, at.Y, 1, at.Height), LegendColors.Gold);
            DrawRectClipped(batch, new Rectangle(at.Right - 1, at.Y, 1, at.Height), LegendColors.Gold);
        }
    }

    private void DrawTrapBacks(SpriteBatch batch, Rectangle row, int count)
    {
        var art = TcgMatchArt.ArtPresent() ? UiRenderer.Instance : null;

        for (var i = 0; i < count; i++)
        {
            var slot = TcgMatchLayout.Trap(row, i);
            var at = new Rectangle(ScreenX + slot.X, ScreenY + slot.Y, 24, 34);

            if (art is not null)
                DrawTextureFitted(batch, art.GetSpfTexture(TcgMatchArt.TRAP), at, Color.White);
            else
                DrawRectClipped(batch, at, BoxBack);
        }
    }

    private void DrawCreatureRow(SpriteBatch batch, TcgDisplayArgs board, List<TcgCreatureView> creatures, bool yours, int clockMs)
    {
        var facing = yours ? Direction.Up : Direction.Down;

        for (var i = 0; i < creatures.Count; i++)
        {
            var view = creatures[i];

            if (AnimatedIds.Contains(view.Id) || DestroyedIds.Contains(view.Id) || FreshIds.Contains(view.Id))
                continue;

            var slot = DrawFeet(board, Input, view.Id, yours, i, creatures.Count);
            var feet = new Point(ScreenX + slot.X, ScreenY + slot.Y);
            var tint = view.Tapped ? TappedTint : Color.White;

            if (CreatureOutlined(view))
                DrawAttackerOutline(batch, slot);

            var choice = TcgCreatureBorders.ForCreature(board, Input, view, Catalog.Get);

            if (choice.Border != TcgCreatureBorder.None)
                DrawSelectionBorder(batch, slot, choice.Border, choice.Mode);

            if (yours && ReadyGlow(board, Input, view, HoverCreatureId, Catalog.Get) is var glow && (glow != TcgReadyGlow.None))
                DrawReadyGlow(batch, feet, glow == TcgReadyGlow.Attack ? LegendColors.Gold : BlockGlow, clockMs);

            if (Catalog.Get(view.Key) is { } card)
                Creature.Draw(batch, card, feet, facing, TcgBoardCreature.Pose.Idle, clockMs, tint);
            else
            {
                var box = new Rectangle(feet.X - 16, feet.Y - 32, 32, 32);
                DrawRectClipped(batch, box, BoxBack);
                DrawTextClipped(batch, new Vector2(box.X + 12, box.Y + 10), "?", tint, false);
            }

            var plate = $"{view.Attack}/{view.Health - view.Damage}";
            var plateWidth = TextRenderer.MeasureWidth(plate) + 6;
            var plateRect = new Rectangle(feet.X - (plateWidth / 2), feet.Y + 2, plateWidth, TextRenderer.CHAR_HEIGHT);
            DrawRectClipped(batch, plateRect, PlateBack);
            DrawTextClipped(batch, new Vector2(plateRect.X + 3, plateRect.Y), plate, tint, false);

            if (view.SummoningSick)
                DrawSick(batch, feet, clockMs);

            if (CreatureBadge(board, view) is { } badge)
                DrawOrderBadge(batch, slot, badge);
        }
    }

    /// <summary>Creatures already entered or done attacking in this Update, drawn at their new-board seats.</summary>
    private void DrawFresh(SpriteBatch batch, TcgDisplayArgs playing, int clockMs)
    {
        foreach (var id in FreshIds)
        {
            if (DestroyedIds.Contains(id))
                continue;

            foreach (var seat in new[] { playing.YourSeat, TcgMatchRules.Other(playing.YourSeat) })
            {
                var creatures = playing.Players[(int)seat].Battlefield;
                var index = creatures.FindIndex(creature => creature.Id == id);

                if (index < 0)
                    continue;

                var view = creatures[index];
                var yours = seat == playing.YourSeat;
                var slot = TcgMatchLayout.CreatureSlot(index, creatures.Count, yours);
                var feet = new Point(ScreenX + slot.X, ScreenY + slot.Y);
                var tint = view.Tapped ? TappedTint : Color.White;

                if (Catalog.Get(view.Key) is { } card)
                    Creature.Draw(batch, card, feet, yours ? Direction.Up : Direction.Down, TcgBoardCreature.Pose.Idle, clockMs, tint);

                var plate = $"{view.Attack}/{view.Health - view.Damage}";
                var plateWidth = TextRenderer.MeasureWidth(plate) + 6;
                var plateRect = new Rectangle(feet.X - (plateWidth / 2), feet.Y + 2, plateWidth, TextRenderer.CHAR_HEIGHT);
                DrawRectClipped(batch, plateRect, PlateBack);
                DrawTextClipped(batch, new Vector2(plateRect.X + 3, plateRect.Y), plate, tint, false);

                if (view.SummoningSick)
                    DrawSick(batch, feet, clockMs);

                break;
            }
        }
    }

    private void DrawReadyGlow(SpriteBatch batch, Point feet, Color color, int clockMs)
    {
        const int HALF_WIDTH = 20;
        const int HALF_HEIGHT = 5;
        var pulse = 0.45f + (0.2f * MathF.Sin(clockMs / 250f));

        for (var row = -HALF_HEIGHT; row < HALF_HEIGHT; row++)
        {
            var y = (row + 0.5f) / HALF_HEIGHT;
            var halfWidth = (int)MathF.Round(HALF_WIDTH * MathF.Sqrt(1 - (y * y)));
            var fade = 1 - (0.5f * MathF.Abs(y));
            DrawRectClipped(batch, new Rectangle(feet.X - halfWidth, feet.Y - 1 + row, halfWidth * 2, 1), color * (pulse * fade));
        }
    }

    private void DrawSick(SpriteBatch batch, Point feet, int clockMs)
    {
        var at = new Rectangle(feet.X - 8, feet.Y - 64, 16, 16);

        if (TcgMatchArt.ArtPresent() && (UiRenderer.Instance is { } ui))
            DrawTextureFitted(batch, ui.GetSpfTexture(TcgMatchArt.ZZZ, (clockMs / 300) % 3), at, Color.White);
        else
            DrawTextClipped(batch, new Vector2(at.X + 4, at.Y + 2), "z", Color.White, false);
    }

    private static string Fit(string text, int width)
    {
        if (TextRenderer.MeasureWidth(text) <= width)
            return text;

        var cut = text;

        while ((cut.Length > 1) && (TextRenderer.MeasureWidth(cut + "..") > width))
            cut = cut[..^1];

        return cut + "..";
    }
}
