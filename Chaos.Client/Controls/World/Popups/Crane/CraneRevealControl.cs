#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.World.Popups.Tcg;
using Chaos.Client.Rendering;
using Chaos.Client.ViewModel;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Popups.Crane;

/// <summary>
///     The crane prize reveal: the mystery capsule twists open, one card rises out face down, a click or Space flips it to
///     show the prize, and a second click, Space or Esc closes it. The prize is already granted when this opens; closing
///     it sends nothing. The timeline is <see cref="CraneRevealState" />; the keys are <see cref="CraneRevealRules" />.
///     It covers the whole screen and sits on the control stack, so while it is open the arrows, Space and clicks never
///     reach crane steering or walking. Every texture is UiRenderer's or CreatureRenderer's cached one: never dispose them.
/// </summary>
public sealed class CraneRevealControl : UIPanel
{
    /// <summary>The mystery capsule's merchant sprite.</summary>
    public const int CAPSULE_SPRITE = 1522;

    //the TCG reveal card's 2x size, a little wider so the ticket line and longer names fit
    public const int CARD_HEIGHT = TcgCardFace.HEIGHT * 2;
    public const int CARD_WIDTH = (TcgCardFace.WIDTH * 2) + 30;

    private const int BORDER = 3;
    private const int CAPSULE_SIZE = 48;
    private const int HINT_GAP = 10;
    private const int ICON_SCALE = 2;
    private const int ICON_TOP = 26;
    private const int ICON_WELL = 72;
    private const int NAME_LINES = 3;
    private const int PADDING = 8;
    private const int SWEEP_WIDTH = 18;

    private const string CLOSE_HINT = "Click, Space or Esc to close";
    private const string FLIP_HINT = "Click or press Space to flip";

    private static readonly Color BackFill = new(34, 30, 40);
    private static readonly Color Backdrop = new(0, 0, 0, 120);
    private static readonly Color BackMark = new(200, 196, 210);
    private static readonly Color FaceFill = new(22, 18, 14);
    private static readonly Color HintColor = new(220, 220, 220);
    private static readonly Color NameColor = new(240, 232, 208);
    private static readonly Color NewColor = new(255, 230, 90);
    private static readonly Color Pearl = new(226, 222, 214);
    private static readonly Color Silver = new(150, 150, 160);
    private static readonly Color TicketColor = new(170, 220, 255);

    private readonly CreatureRenderer Creatures;
    private readonly CraneRevealState Reveal = new();
    private readonly List<string> NameLines = [];
    private readonly List<string> OutcomeLines = [];

    //time in the phase the control last saw, for the capsule and arrival animations; CraneRevealState keeps its own clock
    private CraneRevealPhase SeenPhase;
    private double SeenPhaseMs;
    private double FaceUpMs;

    public CraneRevealControl(CreatureRenderer creatures, int screenWidth, int screenHeight)
    {
        Creatures = creatures;
        Name = "CraneReveal";
        X = 0;
        Y = 0;
        Width = screenWidth;
        Height = screenHeight;
        Visible = false;
        UsesControlStack = true;
    }

    public CraneRevealPhase Phase => Reveal.Phase;

    /// <summary>Plays a reveal, replacing any reveal still open.</summary>
    public void Start(CranePrizeRevealArgs prize)
    {
        Reveal.Start(prize);
        SeenPhase = Reveal.Phase;
        SeenPhaseMs = 0;
        FaceUpMs = 0;

        var textWidth = CARD_WIDTH - (2 * PADDING);

        NameLines.Clear();
        NameLines.AddRange(TextRenderer.WrapLines(prize.PrizeName, textWidth).Take(NAME_LINES));

        OutcomeLines.Clear();
        OutcomeLines.AddRange(TextRenderer.WrapLines(CraneRevealRules.OutcomeText(prize.IsNew, prize.TicketsGiven), textWidth));

        Show();
    }

    public void Show()
    {
        //idempotent: a second reveal keeps one stack entry and moves it to the top
        InputDispatcher.Instance?.PushControl(this);
        Visible = true;
    }

    public void Hide()
    {
        InputDispatcher.Instance?.RemoveControl(this);
        Visible = false;
    }

    //── input: everything that lands here is the reveal's; nothing passes through to the world below ──

    public override void OnKeyDown(KeyDownEvent e)
    {
        var key = CraneRevealRules.Classify(e.Scancode, e.IsRepeat);

        if (key == CraneRevealKey.None)
            return;

        Act(key);
        e.Handled = true;
    }

    public override void OnMouseDown(MouseDownEvent e) => e.Handled = true;

    public override void OnMouseUp(MouseUpEvent e) => e.Handled = true;

    public override void OnClick(ClickEvent e)
    {
        if (e.Button == MouseButton.Left)
            Act(CraneRevealKey.Advance);

        e.Handled = true;
    }

    public override void OnDoubleClick(DoubleClickEvent e) => e.Handled = true;

    private void Act(CraneRevealKey key)
    {
        CraneRevealRules.Apply(Reveal, key);

        if (Reveal.Phase == CraneRevealPhase.Idle)
            Hide();
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        if (!Visible)
            return;

        var elapsed = gameTime.ElapsedGameTime.TotalMilliseconds;
        Reveal.Advance(elapsed);

        if (Reveal.Phase != SeenPhase)
        {
            SeenPhase = Reveal.Phase;
            SeenPhaseMs = 0;
        } else
            SeenPhaseMs += elapsed;

        if (Reveal.Phase == CraneRevealPhase.FaceUp)
            FaceUpMs += elapsed;
    }

    //── drawing ──

    public override void Draw(SpriteBatch spriteBatch)
    {
        base.Draw(spriteBatch);

        if (!Visible || (ClipRect.Width <= 0) || Reveal.Prize is not { } prize)
            return;

        var centerX = ScreenX + (Width / 2);
        var centerY = ScreenY + (Height / 2);
        var card = new Rectangle(centerX - (CARD_WIDTH / 2), centerY - (CARD_HEIGHT / 2), CARD_WIDTH, CARD_HEIGHT);

        var backdropAlpha = Reveal.Phase == CraneRevealPhase.Opening ? (float)Progress(CraneRevealState.OPEN_MS) : 1f;
        DrawRectClipped(spriteBatch, new Rectangle(ScreenX, ScreenY, Width, Height), Backdrop * backdropAlpha);

        switch (Reveal.Phase)
        {
            case CraneRevealPhase.Opening:
                DrawCapsule(spriteBatch, centerX, centerY, Progress(CraneRevealState.OPEN_MS));

                break;
            case CraneRevealPhase.Arriving:
            {
                //the card rises out of the capsule's flash and grows to full size
                var t = Progress(CraneRevealState.ARRIVE_MS);
                var eased = 1 - Math.Pow(1 - t, 3);
                var scale = 0.3 + (0.7 * eased);
                var width = Math.Max(1, (int)(CARD_WIDTH * scale));
                var height = Math.Max(1, (int)(CARD_HEIGHT * scale));
                var rise = (int)((1 - eased) * 30);
                DrawBack(spriteBatch, new Rectangle(centerX - (width / 2), centerY - (height / 2) + rise, width, height));

                //the last of the capsule's flash fades over the arriving card
                DrawFlash(spriteBatch, centerX, centerY, 1 + t, (float)(0.6 * (1 - t)));

                break;
            }
            case CraneRevealPhase.FaceDown:
                DrawBack(spriteBatch, card);
                DrawHint(spriteBatch, card, FLIP_HINT);

                break;
            case CraneRevealPhase.Flipping:
                DrawFlipping(spriteBatch, card, prize, (float)Reveal.FlipProgress);

                break;
            case CraneRevealPhase.FaceUp:
                DrawFace(spriteBatch, card, prize);

                if (CraneRevealRules.SweepProgress(prize.Tier, FaceUpMs) is { } sweep)
                    DrawSweep(spriteBatch, card, sweep);

                DrawHint(spriteBatch, card, CLOSE_HINT);

                break;
        }
    }

    private double Progress(double lengthMs) => Math.Clamp(SeenPhaseMs / lengthMs, 0, 1);

    /// <summary>The capsule pops: it twists (a squash back and forth), swells, and bursts into a white flash.</summary>
    private void DrawCapsule(SpriteBatch spriteBatch, int centerX, int centerY, double t)
    {
        var twist = 0.55 + (0.45 * Math.Abs(Math.Cos(t * Math.PI * 3)));
        var swell = 1 + (0.4 * t);
        var size = CAPSULE_SIZE * swell;
        var width = Math.Max(1, (int)(size * twist));
        var height = Math.Max(1, (int)size);
        var dest = new Rectangle(centerX - (width / 2), centerY - (height / 2), width, height);

        if (Creatures.GetFrame(CAPSULE_SPRITE, 0) is { Texture: { Width: > 0, Height: > 0 } texture })
        {
            //keep the sprite's aspect inside the twisting box
            var fit = Math.Min(dest.Width / (float)texture.Width, dest.Height / (float)texture.Height);
            var drawWidth = Math.Max(1, (int)(texture.Width * fit));
            var drawHeight = Math.Max(1, (int)(texture.Height * fit));
            DrawTextureFitted(
                spriteBatch,
                texture,
                new Rectangle(centerX - (drawWidth / 2), centerY - (drawHeight / 2), drawWidth, drawHeight),
                Color.White);
        } else
        {
            //no capsule art in these game files: a pearl-and-silver capsule drawn in code
            DrawRectClipped(spriteBatch, dest, Silver);
            var inner = dest;
            inner.Inflate(-2, -2);
            DrawRectClipped(spriteBatch, inner, Pearl);
            DrawRectClipped(spriteBatch, new Rectangle(inner.X, dest.Center.Y - 1, inner.Width, 2), Silver);
        }

        //the burst: a white flash that grows over the last part of the opening
        if (t > 0.6)
        {
            var burst = (t - 0.6) / 0.4;
            DrawFlash(spriteBatch, centerX, centerY, 1 + burst, (float)burst);
        }
    }

    private void DrawFlash(SpriteBatch spriteBatch, int centerX, int centerY, double scale, float alpha)
    {
        if (alpha <= 0)
            return;

        var size = (int)(CAPSULE_SIZE * 1.4 * scale);
        DrawRectClipped(spriteBatch, new Rectangle(centerX - (size / 2), centerY - (size / 2), size, size), Color.White * alpha);
    }

    private void DrawBack(SpriteBatch spriteBatch, Rectangle bounds)
    {
        DrawRectClipped(spriteBatch, bounds, Silver);
        var inner = bounds;
        inner.Inflate(-BORDER, -BORDER);
        DrawRectClipped(spriteBatch, inner, BackFill);

        //a pearl diamond-ish mark and a question mark: no tier shows until the flip
        if (inner.Width < 24)
            return;

        var mark = Math.Min(inner.Width, inner.Height) / 3;
        DrawRectClipped(spriteBatch, new Rectangle(inner.Center.X - (mark / 2), inner.Center.Y - (mark / 2), mark, mark), Pearl * 0.25f);
        DrawBorderClipped(spriteBatch, new Rectangle(inner.Center.X - (mark / 2), inner.Center.Y - (mark / 2), mark, mark), BackMark);

        const string QUESTION = "?";

        DrawTextClipped(
            spriteBatch,
            new Vector2(inner.Center.X - (TextRenderer.MeasureWidth(QUESTION) / 2), inner.Center.Y - (TextRenderer.CHAR_HEIGHT / 2)),
            QUESTION,
            BackMark,
            false);
    }

    /// <summary>The flip, as the TCG cards do it: the back squashes to nothing, then the face grows back out.</summary>
    private void DrawFlipping(SpriteBatch spriteBatch, Rectangle bounds, CranePrizeRevealArgs prize, float progress)
    {
        var backHalf = progress < 0.5f;
        var widthScale = backHalf ? 1f - (progress * 2f) : (progress - 0.5f) * 2f;
        var width = Math.Max(1, (int)(bounds.Width * widthScale));
        var squashed = new Rectangle(bounds.X + ((bounds.Width - width) / 2), bounds.Y, width, bounds.Height);

        if (backHalf)
            DrawBack(spriteBatch, squashed);
        else
            DrawFrame(spriteBatch, squashed, prize.Tier);
    }

    private void DrawFrame(SpriteBatch spriteBatch, Rectangle bounds, byte tier)
    {
        DrawRectClipped(spriteBatch, bounds, CraneRevealRules.TierColor(tier));
        var inner = bounds;
        inner.Inflate(-BORDER, -BORDER);
        DrawRectClipped(spriteBatch, inner, FaceFill);
    }

    private void DrawFace(SpriteBatch spriteBatch, Rectangle bounds, CranePrizeRevealArgs prize)
    {
        var tierColor = CraneRevealRules.TierColor(prize.Tier);
        DrawFrame(spriteBatch, bounds, prize.Tier);

        //the tier name across the top
        var tier = CraneRevealState.TierLabel(prize.Tier);
        DrawCentered(spriteBatch, bounds, bounds.Y + PADDING, tier, tierColor);

        //the icon, in a well tinted with the tier colour; drawn from the item panel sprites as the inventory draws them
        var well = new Rectangle(bounds.Center.X - (ICON_WELL / 2), bounds.Y + ICON_TOP, ICON_WELL, ICON_WELL);
        DrawRectClipped(spriteBatch, well, tierColor * 0.25f);
        DrawBorderClipped(spriteBatch, well, tierColor);
        DrawIcon(spriteBatch, well, prize.IconSprite);

        //the name, then NEW! or the duplicate's tickets
        var y = well.Bottom + 6;

        foreach (var line in NameLines)
        {
            DrawCentered(spriteBatch, bounds, y, line, NameColor);
            y += TextRenderer.CHAR_HEIGHT;
        }

        var outcomeColor = prize.IsNew ? NewColor : TicketColor;
        var outcomeTop = bounds.Bottom - PADDING - (OutcomeLines.Count * TextRenderer.CHAR_HEIGHT);

        foreach (var line in OutcomeLines)
        {
            DrawCentered(spriteBatch, bounds, outcomeTop, line, outcomeColor);
            outcomeTop += TextRenderer.CHAR_HEIGHT;
        }
    }

    private void DrawIcon(SpriteBatch spriteBatch, Rectangle well, ushort sprite)
    {
        if (sprite == 0 || UiRenderer.Instance is not { } ui)
            return;

        var texture = ui.GetItemIcon(sprite);

        if ((texture.Width <= 0) || (texture.Height <= 0))
            return;

        //pixel-exact at 2x, shrunk only if a sprite would overflow the well
        var fit = Math.Min(ICON_SCALE, Math.Min((well.Width - 4) / (float)texture.Width, (well.Height - 4) / (float)texture.Height));
        var width = Math.Max(1, (int)(texture.Width * fit));
        var height = Math.Max(1, (int)(texture.Height * fit));
        DrawTextureFitted(
            spriteBatch,
            texture,
            new Rectangle(well.X + ((well.Width - width) / 2), well.Y + ((well.Height - height) / 2), width, height),
            Color.White);
    }

    /// <summary>The gold sweep: a soft light band crossing the face from left to right.</summary>
    private void DrawSweep(SpriteBatch spriteBatch, Rectangle bounds, double progress)
    {
        var inner = bounds;
        inner.Inflate(-BORDER, -BORDER);
        var x = inner.X - SWEEP_WIDTH + (int)((inner.Width + SWEEP_WIDTH) * progress);

        //a centre stripe and two fainter edges, kept inside the face
        DrawBand(spriteBatch, inner, x, SWEEP_WIDTH, 0.12f);
        DrawBand(spriteBatch, inner, x + (SWEEP_WIDTH / 3), SWEEP_WIDTH / 3, 0.22f);
    }

    private void DrawBand(SpriteBatch spriteBatch, Rectangle inner, int x, int width, float alpha)
    {
        var band = Rectangle.Intersect(new Rectangle(x, inner.Y, width, inner.Height), inner);

        if (band is { Width: > 0, Height: > 0 })
            DrawRectClipped(spriteBatch, band, Color.White * alpha);
    }

    private void DrawHint(SpriteBatch spriteBatch, Rectangle card, string hint)
        => DrawCentered(spriteBatch, card, card.Bottom + HINT_GAP, hint, HintColor);

    private void DrawCentered(SpriteBatch spriteBatch, Rectangle bounds, int y, string text, Color color)
        => DrawTextClipped(spriteBatch, new Vector2(bounds.Center.X - (TextRenderer.MeasureWidth(text) / 2), y), text, color, false);

    private void DrawBorderClipped(SpriteBatch spriteBatch, Rectangle bounds, Color color)
    {
        DrawRectClipped(spriteBatch, new Rectangle(bounds.X, bounds.Y, bounds.Width, 1), color);
        DrawRectClipped(spriteBatch, new Rectangle(bounds.X, bounds.Bottom - 1, bounds.Width, 1), color);
        DrawRectClipped(spriteBatch, new Rectangle(bounds.X, bounds.Y, 1, bounds.Height), color);
        DrawRectClipped(spriteBatch, new Rectangle(bounds.Right - 1, bounds.Y, 1, bounds.Height), color);
    }
}
