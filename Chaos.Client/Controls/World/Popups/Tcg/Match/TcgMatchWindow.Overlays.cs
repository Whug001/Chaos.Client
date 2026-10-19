#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Controls.World.Popups.Tcg;
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Popups.Tcg.Match;

/// <summary>The response window's big stack card, the redraw overlay and the end banner.</summary>
public sealed partial class TcgMatchWindow
{
    private const int BANNER_TEXT_WIDTH = 280;
    private const int CLOSE_BUTTON_WIDTH = 80;

    private readonly List<TcgCardFace> BannerFaces = [];
    private CustomButton? CloseButton;
    private Rectangle CloseAt;

    private static readonly Rectangle RedrawOverlay = new(0, 0, 470, 352);
    private static readonly Color OverlayDim = new(0, 0, 0, 180);
    private static readonly Color PutBackMark = new(220, 40, 40);

    private readonly List<TcgCardFace> RedrawFaces = [];

    public static List<Rectangle> RedrawSlots(int count)
    {
        var slots = new List<Rectangle>(count);

        if (count <= 0)
            return slots;

        var perRow = count > 4 ? 4 : count;
        var stepX = perRow > 1 ? Math.Min(128, (RedrawOverlay.Width - 120) / (perRow - 1)) : 0;
        var rows = (count + perRow - 1) / perRow;
        var stepY = rows > 1 ? Math.Min(176, (RedrawOverlay.Height - 168) / (rows - 1)) : 0;
        var totalH = ((rows - 1) * stepY) + 168;
        var y = RedrawOverlay.Y + ((RedrawOverlay.Height - totalH) / 2);

        for (var row = 0; row < rows; row++)
        {
            var inRow = Math.Min(perRow, count - (row * perRow));
            var rowW = ((inRow - 1) * stepX) + 120;
            var x = RedrawOverlay.X + ((RedrawOverlay.Width - rowW) / 2);

            for (var col = 0; col < inRow; col++)
                slots.Add(new Rectangle(x + (col * stepX), y + (row * stepY), 120, 168));
        }

        return slots;
    }

    public static int RedrawCardIdAt(TcgDisplayArgs board, int x, int y)
    {
        if (board.Players.Count < 2)
            return 0;

        var hand = board.Players[(int)board.YourSeat].Hand;
        var slots = RedrawSlots(hand.Count);

        for (var i = slots.Count - 1; i >= 0; i--)
        {
            if (slots[i].Contains(x, y))
                return hand[i].Id;
        }

        return 0;
    }

    public static string? PhaseOverride(
        TcgDisplayArgs board,
        TcgMatchInput input,
        string opponent,
        bool needsRedraw,
        Func<string, TcgCatalogCardView?> lookup)
    {
        if (input.Mode == TcgInputMode.Targeting)
            return "Click a target. Esc cancels.";

        if (input.Mode == TcgInputMode.Blocking)
            return HasGuardProblem(board, input.Blocks, lookup) ? "A Guard creature must block." : "Click or drag your creatures onto attackers to block, then Confirm.";

        if (input.Mode == TcgInputMode.Ordering)
            return "Click blockers in damage order.";

        if (needsRedraw)
            return "Click cards to put back, then Keep.";

        if ((board.Decision == TcgDecision.Respond) && (board.Decider != board.YourSeat))
            return $"Waiting for {opponent}…";

        return null;
    }

    public static bool ShowsStackBigCard(TcgDisplayArgs? board, bool spectating)
        => board is { Decision: TcgDecision.Respond, Stack.Count: > 0 }
           && (spectating || TcgMatchRules.YouDecide(board, TcgDecision.Respond) || (board.Stack[^1].Controller == board.YourSeat));

    private bool IsResponseShowing => ShowsStackBigCard(State.Latest, State.Spectating);

    private void SyncResponseBigCard()
    {
        if (!IsResponseShowing || (State.Latest is not { } latest))
            return;

        var top = latest.Stack[^1];
        BigCard.Card = Catalog.Get(top.Key);
        BigCard.Style = top.Printing.Style;
        BigCard.Foil = top.Printing.Foil || IsFoilAnte(latest, top.Key);
    }

    private bool HandleRedrawClick(int x, int y)
    {
        if ((State.Latest is not { } latest) || (latest.Phase != TcgPhase.Redraw))
            return false;

        if (!RedrawOverlay.Contains(x, y))
            return false;

        if (State.NeedsRedraw && (RedrawCardIdAt(Queue.ShownBoard ?? latest, x, y) is var id && (id != 0)))
            Input.ToggleRedraw(id);

        return true;
    }

    private void DrawEndBanner(SpriteBatch batch)
    {
        if (CloseButton is { } existing)
            existing.Visible = State.Ended is not null;

        if (State.Ended is not { } ended)
            return;

        if (CloseButton is null)
        {
            CloseButton = new CustomButton("Close", CLOSE_BUTTON_WIDTH);
            CloseButton.Clicked += Hide;
            AddChild(CloseButton);
        }

        var at = At(TcgMatchLayout.Banner);

        if (TcgMatchArt.ArtPresent() && (UiRenderer.Instance is { } ui))
            DrawTextureFitted(batch, ui.GetSpfTexture(TcgMatchArt.BANNER, BannerFrame(ended.Outcome)), at, Color.White);
        else
            DrawRectClipped(batch, at, BoxBack);

        var y = TcgMatchLayout.Banner.Bottom + 4;
        y = DrawCentredBannerLine(batch, TcgMatchText.BannerText(ended.Outcome, ended.WinnerName), y);

        y = DrawCentredBannerLine(batch, ended.ReasonText, y);

        if (ended.StakesText.Length > 0)
        {
            foreach (var line in TextRenderer.WrapLines(ended.StakesText, BANNER_TEXT_WIDTH))
                y = DrawCentredBannerLine(batch, line, y);

            y = DrawBannerAnte(batch, y + 2);
        }

        CloseAt = new Rectangle(
            TcgMatchLayout.Banner.X + ((TcgMatchLayout.Banner.Width - CLOSE_BUTTON_WIDTH) / 2),
            y + 2,
            CLOSE_BUTTON_WIDTH,
            CustomButton.HEIGHT);
        CloseButton.X = CloseAt.X;
        CloseButton.Y = CloseAt.Y;
        CloseButton.Visible = true;
    }

    private int DrawCentredBannerLine(SpriteBatch batch, string text, int y)
    {
        var x = TcgMatchLayout.Banner.X + ((TcgMatchLayout.Banner.Width - TextRenderer.MeasureWidth(text)) / 2);
        DrawTextClipped(batch, new Vector2(ScreenX + x, ScreenY + y), text, Color.White, false);

        return y + TextRenderer.CHAR_HEIGHT + 2;
    }

    private int DrawBannerAnte(SpriteBatch batch, int y)
    {
        if ((State.Latest is not { } latest) || (latest.Stakes != TcgStakes.Ante) || (latest.Ante.Count == 0))
            return y;

        while (BannerFaces.Count < latest.Ante.Count)
            BannerFaces.Add(new TcgCardFace(Creatures, 1));

        const int gap = 8;
        var total = (latest.Ante.Count * TcgCardFace.WIDTH) + ((latest.Ante.Count - 1) * gap);
        var x = TcgMatchLayout.Banner.X + ((TcgMatchLayout.Banner.Width - total) / 2);

        for (var i = 0; i < latest.Ante.Count; i++)
        {
            var face = BannerFaces[i];
            face.Card = Catalog.Get(latest.Ante[i].Key);
            face.Style = latest.Ante[i].Style;
            face.Foil = latest.Ante[i].Foil;
            face.FaceDown = false;
            face.FlipProgress = 1f;
            face.Badge = string.Empty;
            face.Owned = true;
            face.Selected = false;
            face.X = x;
            face.Y = y;
            face.Draw(batch);
            x += TcgCardFace.WIDTH + gap;
        }

        return y + TcgCardFace.HEIGHT + 4;
    }

    private const int HINT_WIDTH = 368;
    private const int HINT_MAX_LINES = 3;

    /// <summary>
    ///     Where the hint banner goes. During the redraw phase the redraw cards fill the board, so the banner moves below
    ///     the redraw overlay instead of dimming under it.
    /// </summary>
    public static Rectangle HintBannerRect(TcgDisplayArgs? board, int height)
    {
        var isRedraw = (board is not null) && (board.Phase == TcgPhase.Redraw) && (board.Players.Count >= 2);
        var y = isRedraw
            ? RedrawOverlay.Bottom + 4
            : TcgMatchLayout.OpponentStrip.Bottom + 4;
        var x = isRedraw
            ? ((TcgMatchLayout.Buttons.Left - 4 - HINT_WIDTH) / 2)
            : ((TcgMatchLayout.Board.Width - HINT_WIDTH) / 2);

        return new Rectangle(x, y, HINT_WIDTH, height);
    }

    private void DrawHintBanner(SpriteBatch batch)
    {
        if (string.IsNullOrEmpty(State.Hint))
            return;

        var lines = TextRenderer.WrapLines(State.Hint, HINT_WIDTH - 16).Take(HINT_MAX_LINES).ToList();

        if (lines.Count == 0)
            return;

        var height = (lines.Count * (TextRenderer.CHAR_HEIGHT + 2)) + 8;
        var at = ToScreen(HintBannerRect(State.Latest, height));

        UIElement.DrawBorderedRect(batch, at, BoxBack, LegendColors.Gold);

        for (var i = 0; i < lines.Count; i++)
        {
            var x = at.X + ((at.Width - TextRenderer.MeasureWidth(lines[i])) / 2);
            DrawTextClipped(batch, new Vector2(x, at.Y + 4 + (i * (TextRenderer.CHAR_HEIGHT + 2))), lines[i], Color.White, false);
        }
    }

    private void DrawRedraw(SpriteBatch batch)
    {
        if ((State.Latest is not { } latest) || (latest.Phase != TcgPhase.Redraw) || (latest.Players.Count < 2))
            return;

        DrawRectClipped(batch, ToScreen(RedrawOverlay), OverlayDim);

        if (!State.NeedsRedraw)
        {
            var waiting = $"Waiting for {State.OpponentName}…";
            var at = ToScreen(RedrawOverlay);
            var x = at.X + ((at.Width - TextRenderer.MeasureWidth(waiting)) / 2);
            DrawTextClipped(batch, new Vector2(x, at.Y + ((at.Height - TextRenderer.CHAR_HEIGHT) / 2)), waiting, Color.White, false);

            return;
        }

        var hand = latest.Players[(int)latest.YourSeat].Hand;

        while (RedrawFaces.Count < hand.Count)
            RedrawFaces.Add(new TcgCardFace(Creatures, 2));

        var slots = RedrawSlots(hand.Count);

        for (var i = 0; i < hand.Count; i++)
        {
            var face = RedrawFaces[i];
            face.Card = Catalog.Get(hand[i].Key);
            face.Style = hand[i].Printing.Style;
            face.Foil = hand[i].Printing.Foil;
            face.FaceDown = false;
            face.FlipProgress = 1f;
            face.Badge = string.Empty;
            face.Owned = true;
            face.Selected = false;
            face.X = slots[i].X;
            face.Y = slots[i].Y;
            face.Draw(batch);

            if (!Input.RedrawMarks.Contains(hand[i].Id))
                continue;

            var box = ToScreen(slots[i]);
            UIElement.DrawBorder(batch, box, PutBackMark);

            const string mark = "put back";
            var markX = box.X + ((box.Width - TextRenderer.MeasureWidth(mark)) / 2);
            DrawTextClipped(batch, new Vector2(markX, box.Bottom - TextRenderer.CHAR_HEIGHT - 2), mark, PutBackMark, false);
        }
    }
}
