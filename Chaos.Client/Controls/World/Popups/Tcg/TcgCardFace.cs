#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Rendering;
using Chaos.Client.Systems;
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Geometry.Abstractions.Definitions;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Popups.Tcg;

/// <summary>
///     One card, drawn from its painting in setoa.dat (`tcgart<nnn>.spf`) in its style, or from its sprite when it has none,
///     at 1x (60 x 84, the grids) or 2x (120 x 168, the detail panel and the reveal): the element frame, the picture, the
///     cost, and at 2x the name, type, rules text and stats. Unowned cards are drawn dark and foils show an animated
///     holo sheen; a face-down card shows the back and its pack glow. Every texture is UiRenderer's or CreatureRenderer's
///     cached one, except the shared foil texture this control repaints: never dispose them.
///     The art contract is in the 3b plan ("Card art contract").
/// </summary>
public sealed class TcgCardFace : UIElement
{
    public const int BACK_FRAME = 5;
    public const string FRAME_FILE = TcgFaceArts.FRAME_FILE;
    public const int FRAME_FRAMES = 6;
    public const string GLOW_FILE = "tcgglow.spf";
    public const int GLOW_FRAMES = 3;
    public const int GLOW_MARGIN = 4;
    public const int HEIGHT = 84;
    public const int WIDTH = 60;

    private const int FOIL_GLOW = 2;
    private const int LEGENDARY_GLOW = 1;
    private const int FULL_PANEL_BOTTOM = 164;
    private const int FULL_RULES_BOTTOM = 148;
    private const int FULL_RULES_LINES = 6;
    private const int MYTHIC_GLOW = 3;
    private const int MIN_FRAME_MS = 120;
    private const int RARE_GLOW = 0;

    private static readonly Color BadgeBack = new(0, 0, 0, 200);
    private static readonly Color FullTextBack = new(0, 0, 0, 160);
    private static readonly Color UnownedTint = new(80, 80, 80);

    //picture window, in 1x pixels for 1x faces and 2x pixels for 2x faces
    private static readonly Rectangle Picture1X = new(6, 14, 48, 31);
    private static readonly Rectangle Picture2X = new(12, 20, 96, 70);

    //the 2x window halved: the painting fills the frame's whole picture well, not the sprite's smaller window
    private static readonly Rectangle Painting1X = new(6, 10, 48, 35);

    //the 2x name bar's text row and the right edge the cost ends at, inside the top border
    private const int NAME_LEFT_2X = 9;
    private const int NAME_TOP_2X = 7;
    private const int COST_RIGHT_2X = 111;
    private const int TYPE_TOP_2X = 93;

    private readonly CreatureRenderer Creatures;
    private int AnimCount = 1;
    private double AnimMs;
    private int AnimStart;
    private bool AnimFlip;
    private int IdleFrame;
    private bool IdleFlip;
    private int IntervalMs = MIN_FRAME_MS;
    private int PoseSpriteId = -1;
    private bool IsHovered;

    public TcgCardFace(CreatureRenderer creatures, int scale)
    {
        Creatures = creatures;
        Scale = Math.Clamp(scale, 1, 2);
        Width = WIDTH * Scale;
        Height = HEIGHT * Scale;
    }

    /// <summary>Play the creature's idle animation (the detail panel only); otherwise it holds its idle pose.</summary>
    public bool Animate { get; set; }

    /// <summary>A small tag drawn top-right: the copy count, "in deck / owned", or "New!".</summary>
    public string Badge { get; set; } = string.Empty;

    public Color BadgeColor { get; set; } = Color.White;
    public TcgCatalogCardView? Card { get; set; }
    public bool FaceDown { get; set; }

    /// <summary>1 for a still card; 0 to 1 while flipping from the back (0) to the face (1).</summary>
    public float FlipProgress { get; set; } = 1f;

    /// <summary>Holo sheen: the player owns a foil copy (binder), or this copy is foil (reveal).</summary>
    public bool Foil { get; set; }

    public TcgRevealGlow Glow { get; set; }
    public bool Owned { get; set; } = true;
    public int Scale { get; }
    public bool Selected { get; set; }

    /// <summary>How the card is drawn: its normal window, the extended picture or full art. Foil is separate.</summary>
    public TcgCardStyle Style { get; set; }

    public event Action<TcgCardFace>? Clicked;

    /// <summary>False when this client's setoa.dat lacks the card art (old game files).</summary>
    public static bool ArtPresent()
        => UiRenderer.Instance is { } ui
           && (ui.GetSpfFrameCount(FRAME_FILE) >= FRAME_FRAMES)
           && (ui.GetSpfFrameCount(GLOW_FILE) >= GLOW_FRAMES);

    //one animated foil texture shared by every card on screen, repainted at most every 50 ms
    private const long FOIL_REPAINT_MS = 50;
    private static Texture2D? FoilTextureField;
    private static long FoilPaintedAt;

    /// <summary>The shared foil frame for this tick, repainted at most every 50 ms.</summary>
    private static Texture2D FoilTexture(SpriteBatch spriteBatch, long clock)
    {
        var device = spriteBatch.GraphicsDevice;

        if (FoilTextureField is null || FoilTextureField.IsDisposed || FoilTextureField.GraphicsDevice != device)
        {
            if (FoilTextureField is { IsDisposed: false } stale)
                stale.Dispose();

            FoilTextureField = new Texture2D(device, TcgFoilPainter.Width, TcgFoilPainter.Height);
            FoilPaintedAt = clock - FOIL_REPAINT_MS;
        }

        if (clock - FoilPaintedAt >= FOIL_REPAINT_MS)
        {
            using var scope = new PixelBufferScope(TcgFoilPainter.Width, TcgFoilPainter.Height);
            TcgFoilPainter.Paint(scope.Pixels, (float)((clock / 1000.0) % TcgFoilPainter.LoopSeconds));
            scope.CommitTo(FoilTextureField);
            FoilPaintedAt = clock;
        }

        return FoilTextureField;
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        if (!Visible)
            return;

        UpdateClipRect();

        if ((ClipRect.Width <= 0) || (ClipRect.Height <= 0) || UiRenderer.Instance is not { } ui)
            return;

        var bounds = new Rectangle(ScreenX, ScreenY, Width, Height);
        var clock = Environment.TickCount64;
        var showingBack = FaceDown || (FlipProgress < 0.5f);

        if (showingBack && (Glow != TcgRevealGlow.None))
            DrawGlow(spriteBatch, ui, bounds, clock);

        if (!FaceDown && (FlipProgress < 1f))
        {
            DrawFlipping(spriteBatch, ui, bounds);

            return;
        }

        if (FaceDown || Card is not { } card)
        {
            DrawTextureFitted(spriteBatch, ui.GetSpfTexture(FRAME_FILE, BACK_FRAME), bounds, Color.White);

            return;
        }

        var tint = Owned ? Color.White : UnownedTint;
        var art = TcgFaceArts.Resolve(card, Style, ui.GetSpfFrameCount);
        DrawFace(spriteBatch, ui, card, art, bounds, tint);
        DrawCost(spriteBatch, card, bounds, tint);

        if (Scale >= 2)
            DrawText2X(spriteBatch, card, bounds, tint, art == TcgFaceArt.Full);

        if (card.Type == TcgCardType.Creature)
            DrawStats(spriteBatch, card, bounds, tint);

        if (Foil && Owned)
            DrawTextureFitted(spriteBatch, FoilTexture(spriteBatch, clock), bounds, Color.White);

        if (Selected)
            DrawBorder(spriteBatch, bounds, LegendColors.Gold);

        if (Badge.Length > 0)
            DrawBadge(spriteBatch, bounds);
    }

    private void DrawFace(SpriteBatch spriteBatch, UiRenderer ui, TcgCatalogCardView card, TcgFaceArt art, Rectangle bounds, Color tint)
    {
        //1x faces use the half-size frames, drawn at their own size
        var half = Scale == 1 ? TcgFaceArts.HALF_OFFSET : 0;
        var painting = TcgFaceArts.PaintingFile(card.Painting);

        switch (art)
        {
            case TcgFaceArt.Normal:
                //the normal frame's picture well is opaque, so the painting goes on top of it, where the sprite goes
                DrawFrame(spriteBatch, ui, TcgFaceArts.FRAME_FILE, card, bounds, tint);
                DrawTextureFitted(
                    spriteBatch,
                    ui.GetSpfTexture(painting, TcgFaceArts.PAINTING_NORMAL + half),
                    Offset(Scale == 1 ? Painting1X : Picture2X, bounds),
                    tint);

                break;
            case TcgFaceArt.Extended:
                DrawTextureFitted(
                    spriteBatch,
                    ui.GetSpfTexture(painting, TcgFaceArts.PAINTING_EXTENDED + half),
                    new Rectangle(bounds.X, bounds.Y, bounds.Width, 49 * Scale),
                    tint);
                DrawFrame(spriteBatch, ui, TcgFaceArts.FRAME_X_FILE, card, bounds, tint);

                break;
            case TcgFaceArt.Full:
                DrawTextureFitted(spriteBatch, ui.GetSpfTexture(painting, TcgFaceArts.PAINTING_FULL + half), bounds, tint);
                DrawFrame(spriteBatch, ui, TcgFaceArts.FRAME_F_FILE, card, bounds, tint);

                break;
            default:
                DrawFrame(spriteBatch, ui, TcgFaceArts.FRAME_FILE, card, bounds, tint);
                DrawPicture(spriteBatch, ui, card, Offset(Scale == 1 ? Picture1X : Picture2X, bounds), tint);

                break;
        }
    }

    private void DrawFrame(SpriteBatch spriteBatch, UiRenderer ui, string file, TcgCatalogCardView card, Rectangle bounds, Color tint)
        => DrawTextureFitted(spriteBatch, ui.GetSpfTexture(file, TcgFaceArts.FrameIndex(card, ui.GetSpfFrameCount(file))), bounds, tint);

    public static Color ElementColor(TcgElement element)
        => element switch
        {
            TcgElement.Fire  => new Color(176, 60, 36),
            TcgElement.Water => new Color(44, 96, 180),
            TcgElement.Wind  => new Color(104, 168, 104),
            TcgElement.Earth => new Color(140, 100, 56),
            _                => new Color(130, 130, 130)
        };

    public static int FrameIndex(TcgElement element) => TcgFaceArts.ElementFrame(element);

    public override void OnClick(ClickEvent e)
    {
        if (e.Button != MouseButton.Left)
            return;

        Clicked?.Invoke(this);
        e.Handled = true;
    }

    public override void OnMouseEnter() => IsHovered = true;

    public override void OnMouseLeave() => IsHovered = false;

    //full art shows its painting bare until hovered; a face that can't be hovered (the match's big card) always shows text
    private bool ShowsFullArtText => IsHovered || !IsHitTestVisible;

    public override void Update(GameTime gameTime)
    {
        if (Animate)
            AnimMs += gameTime.ElapsedGameTime.TotalMilliseconds;
    }

    private void DrawBadge(SpriteBatch spriteBatch, Rectangle bounds)
    {
        var width = TextRenderer.MeasureWidth(Badge) + 4;
        var height = TextRenderer.CHAR_HEIGHT + 2;
        var box = new Rectangle(bounds.Right - width - (2 * Scale), bounds.Y + (2 * Scale), width, height);

        DrawRectClipped(spriteBatch, box, BadgeBack);
        DrawTextClipped(spriteBatch, new Vector2(box.X + 2, box.Y + 1), Badge, BadgeColor, false);
    }

    private void DrawCost(SpriteBatch spriteBatch, TcgCatalogCardView card, Rectangle bounds, Color tint)
    {
        var x = Scale == 1 ? bounds.X + 2 : bounds.X + COST_RIGHT_2X - CostWidth(card, Scale);
        var y = Scale == 1 ? bounds.Y + 1 : bounds.Y + NAME_TOP_2X;
        LayOutCost(card, Scale, x, (text, color, at) => DrawTextClipped(spriteBatch, new Vector2(at, y), text, MultiplyTint(color, tint), false),
            (element, at, pip) => DrawRectClipped(
                spriteBatch,
                new Rectangle(at, y + ((TextRenderer.CHAR_HEIGHT - pip) / 2), pip, pip),
                MultiplyTint(ElementColor(element), tint)));
    }

    private static int CostWidth(TcgCatalogCardView card, int scale)
        => LayOutCost(card, scale, 0, null, null) - 1;

    /// <summary>Walks the cost left to right from <paramref name="x" />, calling back for each number and pip; returns the x after the last one.</summary>
    private static int LayOutCost(
        TcgCatalogCardView card,
        int scale,
        int x,
        Action<string, Color, int>? text,
        Action<TcgElement, int, int>? pipAt)
    {
        var pip = 2 + (2 * scale);

        if ((card.Generic > 0) || (card.TotalCost == 0))
        {
            var generic = card.Generic.ToString();
            text?.Invoke(generic, Color.White, x);
            x += TextRenderer.MeasureWidth(generic) + 1;
        }

        (TcgElement Element, int Count)[] parts =
        [
            (TcgElement.Fire, card.Fire),
            (TcgElement.Water, card.Water),
            (TcgElement.Wind, card.Wind),
            (TcgElement.Earth, card.Earth)
        ];

        foreach ((var element, var count) in parts)
        {
            if (count == 0)
                continue;

            var pips = count;

            //more than 3 pips of one element would run off a 1x card; show the number in that element's colour instead
            if (count > 3)
            {
                var number = count.ToString();
                text?.Invoke(number, ElementColor(element), x);
                x += TextRenderer.MeasureWidth(number) + 1;
                pips = 1;
            }

            for (var i = 0; i < pips; i++)
            {
                pipAt?.Invoke(element, x, pip);
                x += pip + 1;
            }
        }

        return x;
    }

    private void DrawFlipping(SpriteBatch spriteBatch, UiRenderer ui, Rectangle bounds)
    {
        var backHalf = FlipProgress < 0.5f;
        var widthScale = backHalf ? 1f - (FlipProgress * 2f) : (FlipProgress - 0.5f) * 2f;
        var width = Math.Max(1, (int)(bounds.Width * widthScale));
        var squashed = new Rectangle(bounds.X + ((bounds.Width - width) / 2), bounds.Y, width, bounds.Height);

        var frame = backHalf || Card is not { } card ? BACK_FRAME : TcgFaceArts.FrameIndex(card, ui.GetSpfFrameCount(FRAME_FILE));
        DrawTextureFitted(spriteBatch, ui.GetSpfTexture(FRAME_FILE, frame), squashed, Color.White);
    }

    private void DrawGlow(SpriteBatch spriteBatch, UiRenderer ui, Rectangle bounds, long clock)
    {
        var pulse = 0.75f + (0.25f * MathF.Sin(clock / 1200f * MathF.Tau));
        var outer = bounds;
        outer.Inflate(GLOW_MARGIN * Scale, GLOW_MARGIN * Scale);

        switch (Glow)
        {
            case TcgRevealGlow.Rare:
                DrawTextureFitted(spriteBatch, ui.GetSpfTexture(GLOW_FILE, RARE_GLOW), outer, Color.White * pulse);

                break;
            case TcgRevealGlow.Legendary:
                DrawTextureFitted(spriteBatch, ui.GetSpfTexture(GLOW_FILE, LEGENDARY_GLOW), outer, Color.White * pulse);

                break;
            case TcgRevealGlow.Foil:
                DrawTextureFitted(spriteBatch, ui.GetSpfTexture(GLOW_FILE, FOIL_GLOW), outer, Color.White * pulse);

                break;
            case TcgRevealGlow.LegendaryFoil:
            {
                //the rainbow first, then the orange one native pixel smaller each side, so a rainbow edge shows around it
                DrawTextureFitted(spriteBatch, ui.GetSpfTexture(GLOW_FILE, FOIL_GLOW), outer, Color.White * pulse);
                var inner = bounds;
                inner.Inflate((GLOW_MARGIN - 1) * Scale, (GLOW_MARGIN - 1) * Scale);
                DrawTextureFitted(spriteBatch, ui.GetSpfTexture(GLOW_FILE, LEGENDARY_GLOW), inner, Color.White * pulse);

                break;
            }
            case TcgRevealGlow.Mythic:
                DrawTextureFitted(spriteBatch, ui.GetSpfTexture(GLOW_FILE, MythicGlow(ui)), outer, Color.White * pulse);

                break;
            case TcgRevealGlow.MythicFoil:
            {
                DrawTextureFitted(spriteBatch, ui.GetSpfTexture(GLOW_FILE, FOIL_GLOW), outer, Color.White * pulse);
                var inner = bounds;
                inner.Inflate((GLOW_MARGIN - 1) * Scale, (GLOW_MARGIN - 1) * Scale);
                DrawTextureFitted(spriteBatch, ui.GetSpfTexture(GLOW_FILE, MythicGlow(ui)), inner, Color.White * pulse);

                break;
            }
        }
    }

    //old game files have no Mythic glow; the legendary one stands in
    private static int MythicGlow(UiRenderer ui) => ui.GetSpfFrameCount(GLOW_FILE) > MYTHIC_GLOW ? MYTHIC_GLOW : LEGENDARY_GLOW;

    private void DrawPicture(SpriteBatch spriteBatch, UiRenderer ui, TcgCatalogCardView card, Rectangle window, Color tint)
    {
        Texture2D? texture = null;
        var flip = false;
        var maxScale = (float)Scale;

        if (card.ArtKind == TcgArtKind.Creature)
        {
            ResolvePose(card.ArtId);

            var frameIndex = IdleFrame;
            flip = IdleFlip;

            if (Animate && (AnimCount > 1))
            {
                frameIndex = AnimStart + ((int)(AnimMs / IntervalMs) % AnimCount);
                flip = AnimFlip;
            }

            var frame = Creatures.GetFrame(card.ArtId, frameIndex) ?? Creatures.GetFrame(card.ArtId, IdleFrame);

            if (frame is { } spriteFrame)
                texture = spriteFrame.Texture;
        } else if (card.ArtKind == TcgArtKind.Item)
            texture = ui.GetItemIcon(card.ArtId);

        if (texture is null || (texture.Width <= 0) || (texture.Height <= 0))
        {
            DrawRectClipped(spriteBatch, window, MultiplyTint(ElementColor(card.Element), tint));

            return;
        }

        //item icons stay pixel-exact at the card's scale; a large creature shrinks to fit its window
        var fit = Math.Min(maxScale, Math.Min(window.Width / (float)texture.Width, window.Height / (float)texture.Height));
        var width = Math.Max(1, (int)(texture.Width * fit));
        var height = Math.Max(1, (int)(texture.Height * fit));
        var dest = new Rectangle(window.X + ((window.Width - width) / 2), window.Y + ((window.Height - height) / 2), width, height);

        DrawTextureFitted(spriteBatch, texture, dest, tint, flip ? SpriteEffects.FlipHorizontally : SpriteEffects.None);
    }

    private void DrawStats(SpriteBatch spriteBatch, TcgCatalogCardView card, Rectangle bounds, Color tint)
    {
        var y = Scale == 1 ? bounds.Y + 70 : bounds.Y + 150;
        var left = Scale == 1 ? bounds.X + 4 : bounds.X + 8;
        var right = Scale == 1 ? bounds.X + 56 : bounds.X + 112;
        var health = card.Health.ToString();

        DrawTextClipped(spriteBatch, new Vector2(left, y), card.Attack.ToString(), MultiplyTint(LegendColors.Orange, tint), false);
        DrawTextClipped(spriteBatch, new Vector2(right - TextRenderer.MeasureWidth(health), y), health, MultiplyTint(LegendColors.Red, tint), false);
    }

    private void DrawText2X(SpriteBatch spriteBatch, TcgCatalogCardView card, Rectangle bounds, Color tint, bool full)
    {
        var white = MultiplyTint(Color.White, tint);
        var nameWidth = COST_RIGHT_2X - CostWidth(card, Scale) - 4 - NAME_LEFT_2X;

        DrawTextClipped(
            spriteBatch,
            new Vector2(bounds.X + NAME_LEFT_2X, bounds.Y + NAME_TOP_2X),
            Fit(card.Name, nameWidth),
            MultiplyTint(LegendColors.Gold, tint),
            false);

        if (full && !ShowsFullArtText)
            return;

        var lines = RulesLines(card, bounds, full);
        var typeTop = full ? TypeTopFull(lines.Count) : TYPE_TOP_2X;
        var boxBottom = bounds.Y + (full ? FULL_RULES_BOTTOM : RULES_TOP + RULES_HEIGHT);

        //full art has no text box in its frame; this panel keeps the text readable over the painting
        if (full)
            DrawRectClipped(spriteBatch, new Rectangle(bounds.X + 8, bounds.Y + typeTop - 2, 104, FULL_PANEL_BOTTOM - typeTop + 2), FullTextBack);

        DrawTextClipped(spriteBatch, new Vector2(bounds.X + 10, bounds.Y + typeTop), card.Type.ToString(), white, false);

        var keywordRuns = TcgKeywordRuns.FindInLines(lines.ConvertAll(entry => entry.Line));

        for (var i = 0; i < lines.Count; i++)
        {
            var (line, x, y) = lines[i];

            DrawTextClipped(spriteBatch, new Vector2(x, y), line, white, false);

            foreach (var run in keywordRuns[i])
            {
                var underlineY = y + TextRenderer.CHAR_HEIGHT - 1;

                //keep the underline inside the rules area rather than lose it
                if (underlineY + 1 > boxBottom)
                    underlineY--;

                DrawRectClipped(
                    spriteBatch,
                    new Rectangle(
                        x + TextRenderer.MeasureWidth(line.AsSpan(0, run.Index)),
                        underlineY,
                        TextRenderer.MeasureWidth(line.AsSpan(run.Index, run.Length)),
                        1),
                    white);
            }
        }
    }

    /// <summary>
    ///     The keyword under the cursor in this card's rules text, or null. Only a 2x card that is face up and not
    ///     flipping shows rules text, so only it can answer.
    /// </summary>
    public string? KeywordAt(int mouseX, int mouseY)
    {
        if (!Visible || (Scale < 2) || FaceDown || (FlipProgress < 1f) || Card is not { } card)
            return null;

        var bounds = new Rectangle(ScreenX, ScreenY, Width, Height);
        var full = UiRenderer.Instance is { } ui && (TcgFaceArts.Resolve(card, Style, ui.GetSpfFrameCount) == TcgFaceArt.Full);

        if (full && !ShowsFullArtText)
            return null;

        var lines = RulesLines(card, bounds, full);
        var keywordRuns = TcgKeywordRuns.FindInLines(lines.ConvertAll(entry => entry.Line));

        for (var i = 0; i < lines.Count; i++)
        {
            var (line, x, y) = lines[i];

            if ((mouseY < y) || (mouseY >= y + TextRenderer.CHAR_HEIGHT))
                continue;

            foreach (var run in keywordRuns[i])
            {
                var startX = x + TextRenderer.MeasureWidth(line.AsSpan(0, run.Index));

                if ((mouseX >= startX) && (mouseX < startX + TextRenderer.MeasureWidth(line.AsSpan(run.Index, run.Length))))
                    return run.Name;
            }

            return null;
        }

        return null;
    }

    //the rules box all three drawn lines sit in, in 2x pixels relative to the card
    private const int RULES_LEFT = 10;
    private const int RULES_TOP = 108;
    private const int RULES_WIDTH = 100;
    private const int RULES_HEIGHT = 38;
    private const int RULES_LINES = 3;

    /// <summary>
    ///     What <see cref="DrawText2X" /> draws and <see cref="KeywordAt" /> hit-tests: the first rules lines with their
    ///     origins. Full art shows up to 6 lines ending above the stats; the other styles 3 lines in the frame's text box.
    /// </summary>
    private static List<(string Line, int X, int Y)> RulesLines(TcgCatalogCardView card, Rectangle bounds, bool full)
    {
        var wrapped = TextRenderer.WrapLines(card.RulesText, RULES_WIDTH);
        var count = Math.Min(wrapped.Count, full ? FULL_RULES_LINES : RULES_LINES);
        var top = full ? FULL_RULES_BOTTOM - (count * TextRenderer.CHAR_HEIGHT) : RULES_TOP;
        var lines = new List<(string Line, int X, int Y)>(count);

        for (var i = 0; i < count; i++)
            lines.Add((wrapped[i], bounds.X + RULES_LEFT, bounds.Y + top + (i * TextRenderer.CHAR_HEIGHT)));

        return lines;
    }

    private static int TypeTopFull(int lineCount) => FULL_RULES_BOTTOM - (lineCount * TextRenderer.CHAR_HEIGHT) - TextRenderer.CHAR_HEIGHT - 1;

    private static string Fit(string text, int width)
    {
        if (TextRenderer.MeasureWidth(text) <= width)
            return text;

        var cut = text;

        while ((cut.Length > 1) && (TextRenderer.MeasureWidth(cut + "..") > width))
            cut = cut[..^1];

        return cut + "..";
    }

    private static Color MultiplyTint(Color color, Color tint)
        => new((byte)(color.R * tint.R / 255), (byte)(color.G * tint.G / 255), (byte)(color.B * tint.B / 255), color.A);

    private static Rectangle Offset(Rectangle rect, Rectangle bounds) => new(bounds.X + rect.X, bounds.Y + rect.Y, rect.Width, rect.Height);

    //looked up once per sprite, not per frame: GetAnimInfo reads the creature archive
    private void ResolvePose(int spriteId)
    {
        if (spriteId == PoseSpriteId)
            return;

        PoseSpriteId = spriteId;
        IdleFrame = 0;
        IdleFlip = false;
        AnimStart = 0;
        AnimCount = 1;
        AnimFlip = false;
        AnimMs = 0;
        IntervalMs = MIN_FRAME_MS;

        if (Creatures.GetAnimInfo(spriteId) is not { } info)
            return;

        //facing the viewer, as the slot machine reels draw creatures
        (IdleFrame, IdleFlip) = AnimationSystem.GetCreatureIdleFrame(in info, Direction.Down);
        IntervalMs = Math.Max(MIN_FRAME_MS, info.AnimationIntervalMs);

        if (info.OptionalAnimationFrameCount > 1)
        {
            AnimStart = IdleFrame;
            AnimCount = info.OptionalAnimationFrameCount;
            AnimFlip = IdleFlip;

            return;
        }

        //many creatures define no idle animation; their walk cycle in place reads as idle at this size
        (var walkStart, var walkCount, var walkFlip) = AnimationSystem.GetCreatureWalkCycle(in info, Direction.Down);

        if (walkCount > 1)
        {
            AnimStart = walkStart;
            AnimCount = walkCount;
            AnimFlip = walkFlip;
        }
    }
}
