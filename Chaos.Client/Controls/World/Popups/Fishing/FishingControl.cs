using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.World.Popups.Dialog;
using Chaos.Client.Definitions;
using Chaos.Client.Extensions;
using Chaos.Client.Rendering;
using Chaos.Client.Utilities;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using SkiaSharp;

namespace Chaos.Client.Controls.World.Popups.Fishing;

/// <summary>
///     The fishing window: the ornate dialog frame, a well of moving water, one fish shown as a silhouette, and a
///     catch meter made from the loading bar stood on end. Holding the mouse or space lifts the green bar. The
///     server sends every position; this control only draws them and reports hold and release.
/// </summary>
public sealed class FishingControl : FramedDialogPanelBase
{
    private const int PANEL_WIDTH = 148;
    private const int FRAME_BOTTOM_BORDER = 47;
    private const int TITLE_TOP = 8;
    private const int WELL_TOP = 26;
    private const int METER_GAP = 6;

    //_nload.spf is the loading plaque, 310x87, with "LOADING..." above the empty bar.
    //the crop is that bar only: the groove the fill sprites sit in, starting just under the word.
    private const int TRACK_X = 20;
    private const int TRACK_Y = 42;
    private const int TRACK_W = 265;
    private const int TRACK_H = 14;
    private const int METER_WIDTH = TRACK_H;

    //wide enough that the 16px recessed frame still leaves a column for water, seaweed, and the fish icon
    private const int WELL_WIDTH = 76;
    private const int WELL_HEIGHT = 196;

    //the well and the meter beside it, centered in the panel
    private const int WELL_X = (PANEL_WIDTH - WELL_WIDTH - METER_GAP - METER_WIDTH) / 2;
    private const int ICON_SIZE = 28;

    //trout's inventory icon, drawn black. Every bite uses it, so the shape never gives away the catch.
    private const ushort SILHOUETTE_SPRITE = 2349;
    private const int HINT_GAP = 8;
    private const int OK_RIGHT_MARGIN = 20;
    private const int OK_BOTTOM_MARGIN = 3;
    private const float CLOSE_MESSAGE_SECONDS = 1f;
    private const float FOLLOW = 14f;

    private static readonly SKColor WaterFill = new(4, 22, 48, 255);
    private static readonly Color WaterTop = new(36, 118, 148);
    private static readonly Color WaterMid = new(14, 70, 104);
    private static readonly Color WaterDeep = new(3, 22, 48);
    private static readonly Color RippleColor = new(150, 214, 222);
    private static readonly Color BubbleColor = new(186, 228, 238);
    private static readonly Color BubbleHighlight = new(236, 250, 255);
    private static readonly Color WeedDeep = new(16, 72, 38);
    private static readonly Color WeedTip = new(48, 138, 62);

    //the same green the health bar uses when a creature is healthy, so the catch zone reads as the game's own green
    private static readonly Color CatchFill = new(0, 97, 0, 210);
    private static readonly Color Silhouette = new(0, 0, 0, 220);

    //used only when the loading-bar art cannot be loaded
    private static readonly Color MeterTrack = new(18, 11, 5);
    private static readonly Color MeterBorder = new(54, 34, 16);
    private static readonly Color MeterFill = new(0, 97, 0);

    private readonly Texture2D? LoadEnd;
    private readonly Texture2D? LoadFill;
    private readonly Texture2D? LoadStart;
    private readonly Texture2D? LoadTrack;
    private readonly UILabel HintLabel;
    private readonly UILabel TitleLabel;
    private readonly bool UseLoadBar;
    private float BarHeight = 30f;
    private float BarShown;
    private float BarTarget;
    private float CloseAfterSeconds = -1f;
    private float FishShown = 50f;
    private float FishTarget = 50f;
    private bool HasSnap;
    private bool Holding;
    private float ProgressShown;
    private bool TakingInput;
    private float WaterTime;
    private float WeedClock = 2.5f;
    private float WeedFade;
    private int WeedShows;
    private bool WeedOnLeft = true;
    private bool WeedPair;
    private bool WeedShown;

    private readonly Bubble[] Bubbles =
    [
        new(0.18f, 0.12f, 0.16f, 0.4f, 2),
        new(0.42f, 0.48f, 0.11f, 1.3f, 3),
        new(0.72f, 0.22f, 0.20f, 2.2f, 2),
        new(0.30f, 0.74f, 0.13f, 0.8f, 2),
        new(0.58f, 0.36f, 0.24f, 3.0f, 3),
        new(0.86f, 0.58f, 0.15f, 1.7f, 2)
    ];

    /// <summary>The player pressed and is holding the line.</summary>
    public event Action? HoldRequested;

    /// <summary>The player let go of the line.</summary>
    public event Action? ReleaseRequested;

    /// <summary>The window closed, for any reason. The world screen tells the server.</summary>
    public event Action? Closed;

    public FishingControl()
        : base("_nsett", false)
    {
        Name = "Fishing";
        Visible = false;
        UsesControlStack = true;
        Width = PANEL_WIDTH;
        Height = WELL_TOP + WELL_HEIGHT + HINT_GAP + TextRenderer.CHAR_HEIGHT + 1 + FRAME_BOTTOM_BORDER;
        this.CenterOnScreen();

        var cache = UiRenderer.Instance;

        if (cache is not null)
        {
            LoadTrack = cache.GetSpfTexture("_nload.spf");
            LoadStart = cache.GetSpfTexture("_nloadb0.spf");
            LoadFill = cache.GetSpfTexture("_nloadb1.spf");
            LoadEnd = cache.GetSpfTexture("_nloadb2.spf");

            UseLoadBar = (LoadTrack.Width >= (TRACK_X + TRACK_W))
                         && (LoadTrack.Height >= (TRACK_Y + TRACK_H))
                         && (LoadStart.Width > 0)
                         && (LoadFill.Width > 0)
                         && (LoadEnd.Width > 0)
                         && ((LoadStart.Width + LoadFill.Width + LoadEnd.Width) == TRACK_W);
        }

        OkButton = CreateCloseButton(Hide, OK_RIGHT_MARGIN, OK_BOTTOM_MARGIN);

        TitleLabel = new UILabel
        {
            X = 0,
            Y = TITLE_TOP,
            Width = PANEL_WIDTH,
            Height = TextRenderer.CHAR_HEIGHT,
            HorizontalAlignment = HorizontalAlignment.Center,
            ForegroundColor = LegendColors.White,
            IsHitTestVisible = false
        };
        AddChild(TitleLabel);

        AddChild(
            new UIPanel
            {
                X = WELL_X,
                Y = WELL_TOP,
                Width = WELL_WIDTH,
                Height = WELL_HEIGHT,
                Background = DialogFrame.BuildRecessedTexture(WaterFill, WELL_WIDTH, WELL_HEIGHT),
                IsHitTestVisible = false
            });

        HintLabel = new UILabel
        {
            X = 8,
            Y = WELL_TOP + WELL_HEIGHT + HINT_GAP,
            Width = PANEL_WIDTH - 16,
            Height = TextRenderer.CHAR_HEIGHT,
            HorizontalAlignment = HorizontalAlignment.Center,
            ForegroundColor = LegendColors.White,
            IsHitTestVisible = false,
            Text = "Hold to raise"
        };
        AddChild(HintLabel);
    }

    /// <summary>The server opened an attempt. The first state packet snaps the fish and the bar into place.</summary>
    public void Open(FishingDisplayArgs args)
    {
        CloseAfterSeconds = -1f;
        TakingInput = true;
        Holding = false;
        HasSnap = false;
        BarHeight = args.BarHeight;
        FishShown = FishTarget = 50f;
        BarShown = BarTarget = (100f - BarHeight) / 2f;
        ProgressShown = 30f;

        TitleLabel.Text = args.Title ?? string.Empty;

        TitleLabel.ForegroundColor = args.Difficulty switch
        {
            FishingDifficulty.Easy   => LegendColors.Lime,
            FishingDifficulty.Medium => LegendColors.CanaryYellow,
            _                        => LegendColors.Red
        };

        HintLabel.Text = "Hold to raise";
        HintLabel.ForegroundColor = LegendColors.White;

        base.Show();
    }

    /// <summary>A picture of the line. The drawn fish and bar ease toward it.</summary>
    public void ApplyState(FishingDisplayArgs args)
    {
        FishTarget = args.FishY;
        BarTarget = args.BarY;
        BarHeight = args.BarHeight;
        ProgressShown = args.Progress;

        if (!HasSnap)
        {
            HasSnap = true;
            FishShown = FishTarget;
            BarShown = BarTarget;
        }
    }

    /// <summary>The server ended the attempt. A reason is shown for a moment first.</summary>
    public void OnServerClose(string? reason)
    {
        if (!Visible)
            return;

        TakingInput = false;
        ReleaseIfHeld();

        if (string.IsNullOrEmpty(reason))
        {
            Hide();

            return;
        }

        HintLabel.Text = reason;
        HintLabel.ForegroundColor = LegendColors.CanaryYellow;
        CloseAfterSeconds = CLOSE_MESSAGE_SECONDS;
    }

    public override void Hide()
    {
        var wasVisible = Visible;

        TakingInput = false;
        ReleaseIfHeld();
        CloseAfterSeconds = -1f;

        base.Hide();

        if (wasVisible)
            Closed?.Invoke();
    }

    /// <summary>Ticked by the world screen, in seconds. Follows the server's picture and watches the mouse and space.</summary>
    public void Update(float seconds)
    {
        if (!Visible)
            return;

        TickWater(seconds);

        if (CloseAfterSeconds >= 0f)
        {
            CloseAfterSeconds -= seconds;

            if (CloseAfterSeconds < 0f)
                Hide();

            return;
        }

        if (TakingInput)
        {
            var wantHold = (Mouse.GetState().LeftButton == ButtonState.Pressed)
                           || Keyboard.GetState().IsKeyDown(Keys.Space);

            if (wantHold != Holding)
            {
                Holding = wantHold;

                if (wantHold)
                    HoldRequested?.Invoke();
                else
                    ReleaseRequested?.Invoke();
            }
        }

        var follow = 1f - MathF.Exp(-FOLLOW * seconds);
        FishShown += (FishTarget - FishShown) * follow;
        BarShown += (BarTarget - BarShown) * follow;
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        base.Draw(spriteBatch);

        if (!Visible)
            return;

        var wellX = ScreenX + WELL_X;
        var wellY = ScreenY + WELL_TOP;
        var water = new Rectangle(
            wellX + DialogFrame.BORDER_SIZE,
            wellY + DialogFrame.BORDER_SIZE,
            WELL_WIDTH - (DialogFrame.BORDER_SIZE * 2),
            WELL_HEIGHT - (DialogFrame.BORDER_SIZE * 2));

        DrawWater(spriteBatch, water);

        var barHeightPx = Math.Max(4, (int)Math.Round(water.Height * BarHeight / 100f));
        var barTop = water.Bottom - (int)Math.Round(water.Height * BarShown / 100f) - barHeightPx;
        barTop = Math.Clamp(barTop, water.Y, water.Bottom - barHeightPx);

        DrawRect(
            spriteBatch,
            new Rectangle(water.X + 6, barTop, water.Width - 12, barHeightPx),
            CatchFill);

        var icon = UiRenderer.Instance!.GetItemIcon(SILHOUETTE_SPRITE);
        var center = water.Bottom - (int)Math.Round(water.Height * FishShown / 100f);
        var iconTop = Math.Clamp(center - (ICON_SIZE / 2), water.Y + 2, water.Bottom - ICON_SIZE - 2);

        DrawTextureFitted(
            spriteBatch,
            icon,
            new Rectangle(water.X + ((water.Width - ICON_SIZE) / 2), iconTop, ICON_SIZE, ICON_SIZE),
            Silhouette);

        DrawMeter(spriteBatch, wellX + WELL_WIDTH + METER_GAP, wellY, wellY + WELL_HEIGHT);
    }

    private void TickWater(float seconds)
    {
        WaterTime += seconds;

        for (var i = 0; i < Bubbles.Length; i++)
        {
            Bubbles[i].Y += Bubbles[i].Speed * seconds;

            if (Bubbles[i].Y > 1f)
                Bubbles[i].Y -= 1f;
        }

        var fadeTarget = WeedShown ? 1f : 0f;
        WeedFade += (fadeTarget - WeedFade) * (1f - MathF.Exp(-4f * seconds));
        WeedClock -= seconds;

        if (WeedClock > 0f)
            return;

        WeedShown = !WeedShown;

        if (WeedShown)
        {
            WeedOnLeft = !WeedOnLeft;
            WeedShows++;
            WeedPair = (WeedShows % 3) == 0;
            WeedClock = 6f + (WeedShows % 3);
        } else
            WeedClock = 8f + ((WeedShows * 2) % 5);
    }

    /// <summary>The opening inside the recessed frame: depth, light moving on the surface, bubbles, and seaweed.</summary>
    private void DrawWater(SpriteBatch spriteBatch, Rectangle water)
    {
        var breathe = 0.5f + (0.5f * MathF.Sin(WaterTime * 0.8f));

        for (var y = 0; y < water.Height; y += 4)
        {
            var t = y / (float)Math.Max(1, water.Height - 1);
            var band = Math.Min(4, water.Height - y);

            DrawRect(
                spriteBatch,
                new Rectangle(water.X, water.Y + y, water.Width, band),
                WaterColor(t, breathe));
        }

        DrawRipples(spriteBatch, water);
        DrawSeaweed(spriteBatch, water, WeedOnLeft, WeedFade, 14);

        if (WeedPair)
            DrawSeaweed(spriteBatch, water, !WeedOnLeft, WeedFade * 0.65f, 8);

        DrawBubbles(spriteBatch, water);

        var surface = (byte)(70 + (50 * breathe));

        DrawRect(
            spriteBatch,
            new Rectangle(water.X, water.Y, water.Width, 2),
            new Color((byte)90, (byte)180, (byte)196, surface));
    }

    private static Color WaterColor(float t, float breathe)
    {
        var top = Color.Lerp(WaterTop, new Color(52, 140, 168), breathe * 0.35f);

        return t < 0.4f
            ? Color.Lerp(top, WaterMid, t / 0.4f)
            : Color.Lerp(WaterMid, WaterDeep, (t - 0.4f) / 0.6f);
    }

    private void DrawRipples(SpriteBatch spriteBatch, Rectangle water)
    {
        (float Speed, float Phase, byte Alpha)[] ripples = [(0.28f, 0f, 80), (0.18f, 2.2f, 50), (0.40f, 4.1f, 100)];

        foreach (var (speed, phase, alpha) in ripples)
        {
            var travel = (WaterTime * speed) % 1f;

            for (var x = 0; x < water.Width; x += 4)
            {
                var wave = MathF.Sin((x * 0.4f) + (WaterTime * 2.2f) + phase) * 2.5f;
                var y = water.Y + (int)Mod(((1f - travel) * water.Height) + wave, water.Height);
                var width = Math.Min(4, water.Width - x);

                DrawRect(
                    spriteBatch,
                    new Rectangle(water.X + x, y, width, 1),
                    RippleColor * (alpha / 255f));
            }
        }
    }

    private void DrawBubbles(SpriteBatch spriteBatch, Rectangle water)
    {
        foreach (var bubble in Bubbles)
        {
            var wobble = MathF.Sin((WaterTime * 1.6f) + bubble.Phase) * 2.5f;
            var x = (int)(water.X + 3 + (bubble.X * (water.Width - 8)) + wobble);
            var y = (int)(water.Bottom - (bubble.Y * water.Height));
            x = Math.Clamp(x, water.X + 1, water.Right - bubble.Size - 1);
            y = Math.Clamp(y, water.Y + 1, water.Bottom - bubble.Size - 1);

            var twinkle = 0.55f + (0.45f * (0.5f + (0.5f * MathF.Sin((WaterTime * 3f) + bubble.Phase))));

            DrawRect(
                spriteBatch,
                new Rectangle(x, y, bubble.Size, bubble.Size),
                BubbleColor * twinkle);

            DrawRect(spriteBatch, new Rectangle(x, y, 1, 1), BubbleHighlight * twinkle);
        }
    }

    private void DrawSeaweed(SpriteBatch spriteBatch, Rectangle water, bool left, float fade, int segments)
    {
        if (fade < 0.02f)
            return;

        var dir = left ? 1 : -1;
        var baseX = left ? water.X + 1 : water.Right - 4;

        for (var i = 0; i < segments; i++)
        {
            var sway = MathF.Sin((WaterTime * 1.4f) + (i * 0.45f) + (left ? 0f : 2f)) * (i * 0.28f);
            var x = (int)(baseX + sway);
            var y = water.Bottom - 2 - (i * 4);

            if (y < water.Y + 6)
                break;

            x = Math.Clamp(x, water.X, water.Right - 3);

            var color = Color.Lerp(WeedDeep, WeedTip, i / (float)segments) * fade;
            var width = i > (segments - 4) ? 2 : 3;

            DrawRect(spriteBatch, new Rectangle(x, y, width, 4), color);

            if ((i > 2) && ((i % 4) == 0))
            {
                var leafX = Math.Clamp(x + (dir * 3), water.X, water.Right - 3);

                DrawRect(spriteBatch, new Rectangle(leafX, y + 1, 3, 2), color);
            }
        }
    }

    private static float Mod(float value, float length)
    {
        var wrapped = value % length;

        return wrapped < 0f ? wrapped + length : wrapped;
    }

    /// <summary>
    ///     The catch meter is the loading bar turned so it fills upward. The plaque's "LOADING..." word is outside
    ///     the crop. <c>_nloadb0</c> is the bottom cap, <c>_nloadb1</c> the fill, and <c>_nloadb2</c> the top cap.
    /// </summary>
    private void DrawMeter(SpriteBatch spriteBatch, int meterX, int wellY, int wellBottom)
    {
        if (!UseLoadBar)
        {
            var meter = new Rectangle(meterX, wellY, METER_WIDTH, WELL_HEIGHT);

            DrawRect(spriteBatch, meter, MeterTrack);

            var fillHeight = (int)Math.Round((WELL_HEIGHT - 2) * Math.Clamp(ProgressShown, 0f, 100f) / 100f);

            if (fillHeight > 0)
                DrawRect(
                    spriteBatch,
                    new Rectangle(meterX + 1, wellBottom - 1 - fillHeight, METER_WIDTH - 2, fillHeight),
                    MeterFill);

            DrawBorder(spriteBatch, meter, MeterBorder);

            return;
        }

        //one source pixel of the bar's length becomes this many screen pixels. thickness stays native.
        var along = WELL_HEIGHT / (float)TRACK_W;
        var bottomLeft = new Vector2(meterX, wellBottom);

        DrawSideways(
            spriteBatch,
            LoadTrack,
            new Rectangle(TRACK_X, TRACK_Y, TRACK_W, TRACK_H),
            bottomLeft,
            along);

        var percent = Math.Clamp(ProgressShown, 0f, 100f) / 100f;
        var startW = LoadStart!.Width;
        var midW = (int)(LoadFill!.Width * percent);
        var inset = (TRACK_H - LoadFill.Height) / 2f;
        var fillAt = new Vector2(meterX + inset, wellBottom);

        DrawSideways(spriteBatch, LoadStart, null, fillAt, along);

        if (midW > 0)
            DrawSideways(
                spriteBatch,
                LoadFill,
                new Rectangle(0, 0, midW, LoadFill.Height),
                new Vector2(fillAt.X, wellBottom - (startW * along)),
                along);

        if (percent >= 1f)
            DrawSideways(
                spriteBatch,
                LoadEnd,
                null,
                new Vector2(fillAt.X, wellBottom - ((startW + LoadFill.Width) * along)),
                along);
    }

    /// <summary>
    ///     Draws a horizontal strip so its left end sits on <paramref name="bottomLeft" /> and its length runs upward.
    /// </summary>
    private void DrawSideways(
        SpriteBatch spriteBatch,
        Texture2D? texture,
        Rectangle? source,
        Vector2 bottomLeft,
        float along)
    {
        if (texture is null)
            return;

        Texture2D actual;
        Rectangle src;

        if (texture is CachedTexture2D { AtlasRegion: { } region })
        {
            actual = region.Atlas;

            src = source.HasValue
                ? new Rectangle(
                    region.SourceRect.X + source.Value.X,
                    region.SourceRect.Y + source.Value.Y,
                    source.Value.Width,
                    source.Value.Height)
                : region.SourceRect;
        } else
        {
            actual = texture;
            src = source ?? new Rectangle(0, 0, texture.Width, texture.Height);
        }

        if ((src.Width <= 0) || (src.Height <= 0))
            return;

        spriteBatch.Draw(
            actual,
            bottomLeft,
            src,
            Color.White,
            -MathHelper.PiOver2,
            Vector2.Zero,
            new Vector2(along, 1f),
            SpriteEffects.None,
            0f);
    }

    public override void OnKeyDown(KeyDownEvent e)
    {
        if (e.Keycode == Keycode.Escape)
        {
            Hide();
            e.Handled = true;

            return;
        }

        //space is the line, not an assail, for as long as the window is up
        if (e.Keycode == Keycode.Space)
            e.Handled = true;
    }

    private void ReleaseIfHeld()
    {
        if (!Holding)
            return;

        Holding = false;
        ReleaseRequested?.Invoke();
    }

    private struct Bubble
    {
        public float Phase;
        public int Size;
        public float Speed;
        public float X;
        public float Y;

        public Bubble(float x, float y, float speed, float phase, int size)
        {
            X = x;
            Y = y;
            Speed = speed;
            Phase = phase;
            Size = size;
        }
    }
}
