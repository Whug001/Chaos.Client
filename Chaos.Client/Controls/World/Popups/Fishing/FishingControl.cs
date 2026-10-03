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
///     The fishing window: the ornate dialog frame, a well of moving water, one fish shown as a silhouette on a line
///     from a float, and a catch meter beside the well. Holding the mouse or space lifts the catch zone. The server
///     sends every position; this control only draws them and reports hold and release.
/// </summary>
public sealed class FishingControl : FramedDialogPanelBase
{
    private const int PANEL_WIDTH = 148;
    private const int FRAME_BOTTOM_BORDER = 47;
    private const int TITLE_TOP = 8;
    private const int WELL_TOP = 26;
    private const int METER_GAP = 8;
    private const int METER_WIDTH = 10;

    //the recessed frame's lip is only 4px on the top and left and 5px on the bottom and right; the water fills the rest
    private const int LIP_NEAR = 4;
    private const int LIP_FAR = 5;
    private const int WELL_WIDTH = 76;
    private const int WELL_HEIGHT = 196;

    //the well and the meter beside it, centered in the panel
    private const int WELL_X = (PANEL_WIDTH - WELL_WIDTH - METER_GAP - METER_WIDTH) / 2;
    private const int ICON_SIZE = 28;

    //trout's inventory icon, drawn black. Every bite uses it, so the shape never gives away the catch.
    private const ushort SILHOUETTE_SPRITE = 2349;

    //where the trout's mouth lands inside the 28px icon box: its source pixel (0, 5) of 30x24, stretched
    private const int MOUTH_X = 1;
    private const int MOUTH_Y = 6;
    private const int SAND_HEIGHT = 12;
    private const int HINT_GAP = 8;
    private const int OK_RIGHT_MARGIN = 20;
    private const int OK_BOTTOM_MARGIN = 3;
    private const float CLOSE_MESSAGE_SECONDS = 1f;
    private const float FOLLOW = 14f;

    private static readonly SKColor WaterFill = new(4, 22, 48, 255);
    private static readonly Color WaterTop = new(52, 140, 168);
    private static readonly Color WaterMid = new(16, 78, 112);
    private static readonly Color WaterDeep = new(4, 24, 52);
    private static readonly Color RayColor = new(200, 240, 255);
    private static readonly Color SurfaceLip = new(190, 240, 248, 220);
    private static readonly Color SurfaceUnder = new(110, 200, 220, 150);
    private static readonly Color Sparkle = new(200, 245, 255);
    private static readonly Color RippleColor = new(150, 214, 222);
    private static readonly Color BubbleColor = new(180, 230, 245);
    private static readonly Color BubbleHighlight = new(255, 255, 255);
    private static readonly Color WeedDeep = new(20, 84, 44);
    private static readonly Color WeedTip = new(70, 160, 80);
    private static readonly Color SandTop = new(96, 82, 52);
    private static readonly Color SandBottom = new(58, 46, 28);
    private static readonly Color SandGrain = new(140, 124, 86);
    private static readonly Color Pebble = new(70, 66, 60);
    private static readonly Color PebbleTop = new(110, 104, 92);
    private static readonly Color Silhouette = new(6, 14, 24, 235);
    private static readonly Color FishRim = new Color(120, 190, 210) * 0.35f;
    private static readonly Color LineColor = new Color(230, 230, 220) * 0.67f;
    private static readonly Color FloatRed = new(220, 50, 40);
    private static readonly Color FloatRedTop = new(240, 90, 70);
    private static readonly Color FloatWhite = new(240, 240, 240);
    private static readonly Color FloatWhiteBottom = new(200, 200, 200);
    private static readonly Color MeterGroove = new(24, 16, 8);
    private static readonly Color MeterShadow = new(70, 48, 22);
    private static readonly Color MeterLight = new(110, 80, 40);
    private static readonly Color MeterTick = new(150, 120, 70);
    private static readonly Color HintColor = new(230, 214, 170);

    //light rays slanting down from the surface: where they start across the water, how wide, and how bright
    private static readonly (float X, int Width, float Alpha)[] Rays = [(0.15f, 7, 0.13f), (0.45f, 10, 0.10f), (0.78f, 6, 0.12f)];

    //pebbles on the sand: position across the water, and width
    private static readonly (float X, int Width)[] Pebbles = [(0.08f, 4), (0.47f, 3), (0.82f, 5)];

    private readonly UILabel HintLabel;
    private readonly UILabel TitleLabel;
    private float BarHeight = 30f;
    private float BarShown;
    private float BarTarget;
    private float CloseAfterSeconds = -1f;
    private Texture2D? FishMask;
    private bool FishMaskTried;
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
    private float ZoneInside = 1f;

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

        OkButton = CreateCloseButton(Hide, OK_RIGHT_MARGIN, OK_BOTTOM_MARGIN);

        TitleLabel = new UILabel
        {
            X = 0,
            Y = TITLE_TOP,
            Width = PANEL_WIDTH,
            Height = TextRenderer.CHAR_HEIGHT,
            HorizontalAlignment = HorizontalAlignment.Center,
            ForegroundColor = LegendColors.White,
            ShadowStyle = ShadowStyle.BottomRight,
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

        //empty while fishing; it only shows the server's closing message, such as the fish getting away
        HintLabel = new UILabel
        {
            X = 8,
            Y = WELL_TOP + WELL_HEIGHT + HINT_GAP,
            Width = PANEL_WIDTH - 16,
            Height = TextRenderer.CHAR_HEIGHT,
            HorizontalAlignment = HorizontalAlignment.Center,
            ForegroundColor = HintColor,
            ShadowStyle = ShadowStyle.BottomRight,
            IsHitTestVisible = false
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
        ZoneInside = 1f;

        TitleLabel.Text = args.Title ?? string.Empty;

        TitleLabel.ForegroundColor = args.Difficulty switch
        {
            FishingDifficulty.Easy   => LegendColors.Lime,
            FishingDifficulty.Medium => LegendColors.CanaryYellow,
            _                        => LegendColors.Red
        };

        HintLabel.Text = string.Empty;
        HintLabel.ForegroundColor = HintColor;

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

        //judged on the server's own numbers, so green means the server is counting the catch
        var covered = FishingLook.Covers(FishTarget, BarTarget, BarHeight);
        ZoneInside = FishingLook.Approach(ZoneInside, covered ? 1f : 0f, seconds);

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
            wellX + LIP_NEAR,
            wellY + LIP_NEAR,
            WELL_WIDTH - LIP_NEAR - LIP_FAR,
            WELL_HEIGHT - LIP_NEAR - LIP_FAR);

        DrawWater(spriteBatch, water);

        var barHeightPx = Math.Max(4, (int)Math.Round(water.Height * BarHeight / 100f));
        var barTop = water.Bottom - (int)Math.Round(water.Height * BarShown / 100f) - barHeightPx;
        barTop = Math.Clamp(barTop, water.Y, water.Bottom - barHeightPx);

        DrawCatchZone(spriteBatch, new Rectangle(water.X + 4, barTop, water.Width - 8, barHeightPx));

        var center = water.Bottom - (int)Math.Round(water.Height * FishShown / 100f);
        var iconTop = Math.Clamp(center - (ICON_SIZE / 2), water.Y + 6, water.Bottom - ICON_SIZE - 2);
        var iconBox = new Rectangle(water.X + ((water.Width - ICON_SIZE) / 2), iconTop, ICON_SIZE, ICON_SIZE);

        DrawLine(spriteBatch, water, iconBox.X + MOUTH_X, iconBox.Y + MOUTH_Y);
        DrawFish(spriteBatch, iconBox);
        DrawMeter(spriteBatch, new Rectangle(wellX + WELL_WIDTH + METER_GAP, wellY + 6, METER_WIDTH, WELL_HEIGHT - 12));
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

    /// <summary>The water inside the recessed frame: depth, light rays, the surface, ripples, sand, seaweed and bubbles.</summary>
    private void DrawWater(SpriteBatch spriteBatch, Rectangle water)
    {
        var breathe = 0.5f + (0.5f * MathF.Sin(WaterTime * 0.8f));

        for (var y = 0; y < water.Height; y += 2)
        {
            var t = y / (float)Math.Max(1, water.Height - 1);
            var band = Math.Min(2, water.Height - y);

            DrawRect(
                spriteBatch,
                new Rectangle(water.X, water.Y + y, water.Width, band),
                WaterColor(t, breathe));
        }

        DrawRays(spriteBatch, water);
        DrawRipples(spriteBatch, water);
        DrawSand(spriteBatch, water);

        DrawSeaweed(spriteBatch, water, WeedOnLeft, WeedFade, 16);

        if (WeedPair)
            DrawSeaweed(spriteBatch, water, !WeedOnLeft, WeedFade * 0.65f, 11);

        DrawBubbles(spriteBatch, water);
        DrawSurface(spriteBatch, water, breathe);
    }

    private static Color WaterColor(float t, float breathe)
    {
        var top = Color.Lerp(WaterTop, new Color(64, 156, 182), breathe * 0.35f);

        return t < 0.35f
            ? Color.Lerp(top, WaterMid, t / 0.35f)
            : Color.Lerp(WaterMid, WaterDeep, (t - 0.35f) / 0.65f);
    }

    private void DrawRays(SpriteBatch spriteBatch, Rectangle water)
    {
        var depth = (int)(water.Height * 0.75f);

        for (var r = 0; r < Rays.Length; r++)
        {
            var (startX, width, alpha) = Rays[r];
            var drift = MathF.Sin((WaterTime * 0.3f) + (r * 1.9f)) * 3f;
            var pulse = 0.75f + (0.25f * MathF.Sin((WaterTime * 0.9f) + (r * 2.4f)));

            for (var y = 0; y < depth; y += 3)
            {
                var t = y / (float)depth;
                var x = (int)(water.X + (startX * water.Width) + drift + (t * 16f));
                var w = width + (int)(t * 6f);
                var left = Math.Max(water.X, x);
                var right = Math.Min(water.Right, x + w);

                if (right <= left)
                    continue;

                DrawRect(
                    spriteBatch,
                    new Rectangle(left, water.Y + y, right - left, Math.Min(3, depth - y)),
                    RayColor * (alpha * pulse * (1f - t)));
            }
        }
    }

    private void DrawSurface(SpriteBatch spriteBatch, Rectangle water, float breathe)
    {
        DrawRect(spriteBatch, new Rectangle(water.X, water.Y, water.Width, 1), SurfaceLip);
        DrawRect(spriteBatch, new Rectangle(water.X, water.Y + 1, water.Width, 1), SurfaceUnder * (0.7f + (0.3f * breathe)));

        for (var x = 0; x < (water.Width - 3); x += 7)
        {
            var twinkle = 0.5f + (0.5f * MathF.Sin((WaterTime * 2.6f) + (x * 0.9f)));

            DrawRect(
                spriteBatch,
                new Rectangle(water.X + x + ((x * 3) % 4), water.Y + 3 + (x % 3), 3, 1),
                Sparkle * (0.47f * twinkle));
        }
    }

    private void DrawRipples(SpriteBatch spriteBatch, Rectangle water)
    {
        (float Speed, float Phase, byte Alpha)[] ripples = [(0.28f, 0f, 60), (0.18f, 2.2f, 40), (0.40f, 4.1f, 80)];
        var height = water.Height - SAND_HEIGHT;

        foreach (var (speed, phase, alpha) in ripples)
        {
            var travel = (WaterTime * speed) % 1f;

            for (var x = 0; x < water.Width; x += 4)
            {
                var wave = MathF.Sin((x * 0.4f) + (WaterTime * 2.2f) + phase) * 2.5f;
                var y = water.Y + (int)Mod(((1f - travel) * height) + wave, height);
                var width = Math.Min(4, water.Width - x);

                DrawRect(
                    spriteBatch,
                    new Rectangle(water.X + x, y, width, 1),
                    RippleColor * (alpha / 255f));
            }
        }
    }

    /// <summary>A rippled sandy bed with lighter grains and three pebbles.</summary>
    private static void DrawSand(SpriteBatch spriteBatch, Rectangle water)
    {
        var bedTop = water.Bottom - SAND_HEIGHT;

        for (var x = 0; x < water.Width; x += 2)
        {
            var top = 3 + (int)Math.Round(2 * Math.Sin(x * 0.35));
            var width = Math.Min(2, water.Width - x);

            for (var y = top; y < SAND_HEIGHT; y += 3)
            {
                var color = Color.Lerp(SandTop, SandBottom, y / (float)SAND_HEIGHT);

                DrawRect(spriteBatch, new Rectangle(water.X + x, bedTop + y, width, Math.Min(3, SAND_HEIGHT - y)), color);
            }

            if (((x * 7) % 11) < 3)
                DrawRect(spriteBatch, new Rectangle(water.X + x, bedTop + top + 2 + (x % 5), 1, 1), SandGrain);
        }

        foreach (var (at, width) in Pebbles)
        {
            var x = water.X + (int)(at * (water.Width - width));

            DrawRect(spriteBatch, new Rectangle(x, water.Bottom - 4, width, 2), Pebble);
            DrawRect(spriteBatch, new Rectangle(x + 1, water.Bottom - 5, width - 2, 1), PebbleTop);
        }
    }

    private void DrawBubbles(SpriteBatch spriteBatch, Rectangle water)
    {
        foreach (var bubble in Bubbles)
        {
            var wobble = MathF.Sin((WaterTime * 1.6f) + bubble.Phase) * 2.5f;
            var x = (int)(water.X + 3 + (bubble.X * (water.Width - 8)) + wobble);
            var y = (int)(water.Bottom - SAND_HEIGHT - (bubble.Y * (water.Height - SAND_HEIGHT)));
            x = Math.Clamp(x, water.X + 1, water.Right - bubble.Size - 1);
            y = Math.Clamp(y, water.Y + 3, water.Bottom - bubble.Size - 1);

            var twinkle = 0.55f + (0.45f * (0.5f + (0.5f * MathF.Sin((WaterTime * 3f) + bubble.Phase))));

            DrawRect(
                spriteBatch,
                new Rectangle(x, y, bubble.Size, bubble.Size),
                BubbleColor * (0.47f * twinkle));

            DrawRect(spriteBatch, new Rectangle(x, y, 1, 1), BubbleHighlight * (0.9f * twinkle));
        }
    }

    private void DrawSeaweed(SpriteBatch spriteBatch, Rectangle water, bool left, float fade, int segments)
    {
        if (fade < 0.02f)
            return;

        var dir = left ? 1 : -1;
        var baseX = left ? water.X + 3 : water.Right - 6;

        for (var i = 0; i < segments; i++)
        {
            var sway = MathF.Sin((WaterTime * 1.4f) + (i * 0.45f) + (left ? 0f : 2f)) * (i * 0.3f);
            var x = (int)(baseX + sway);
            var y = water.Bottom - (SAND_HEIGHT - 2) - (i * 4);

            if (y < water.Y + 6)
                break;

            x = Math.Clamp(x, water.X, water.Right - 3);

            var color = Color.Lerp(WeedDeep, WeedTip, i / (float)segments) * fade;
            var width = i > (segments - 4) ? 2 : 3;

            DrawRect(spriteBatch, new Rectangle(x, y, width, 4), color);

            if ((i % 4) == 2)
            {
                var leafX = Math.Clamp(x + (dir * 3), water.X, water.Right - 3);

                DrawRect(spriteBatch, new Rectangle(leafX, y + 1, 3, 2), color);
            }
        }
    }

    /// <summary>A see-through box with bright top and bottom edges, green while the fish is in it and amber when not.</summary>
    private void DrawCatchZone(SpriteBatch spriteBatch, Rectangle zone)
    {
        var color = FishingLook.ZoneColor(ZoneInside);

        DrawRect(spriteBatch, zone, color * (60f / 255f));

        for (var ny = zone.Y + 5; ny < (zone.Bottom - 4); ny += 6)
            DrawRect(spriteBatch, new Rectangle(zone.X + 2, ny, zone.Width - 4, 1), color * (28f / 255f));

        for (var i = 0; i < 3; i++)
        {
            var edge = color * ((255f - (i * 70f)) / 255f);

            DrawRect(spriteBatch, new Rectangle(zone.X - 1 + i, zone.Y + i, zone.Width + 2 - (2 * i), 1), edge);
            DrawRect(spriteBatch, new Rectangle(zone.X - 1 + i, zone.Bottom - 1 - i, zone.Width + 2 - (2 * i), 1), edge);
        }

        DrawRect(spriteBatch, new Rectangle(zone.X - 1, zone.Y, 1, zone.Height), color * (140f / 255f));
        DrawRect(spriteBatch, new Rectangle(zone.Right, zone.Y, 1, zone.Height), color * (140f / 255f));
    }

    /// <summary>A red and white float bobbing on the surface, and the line from it straight down to the fish's mouth.</summary>
    private void DrawLine(SpriteBatch spriteBatch, Rectangle water, int mouthX, int mouthY)
    {
        var x = Math.Clamp(mouthX, water.X + 2, water.Right - 3);
        var bob = (int)MathF.Round(MathF.Sin(WaterTime * 2.4f) * 0.6f);
        var floatTop = water.Y - 1 + bob;
        var lineTop = floatTop + 6;

        if (mouthY > lineTop)
            DrawRect(spriteBatch, new Rectangle(x, lineTop, 1, mouthY - lineTop), LineColor);

        DrawRect(spriteBatch, new Rectangle(x - 1, Math.Max(water.Y, floatTop), 3, 1), FloatRedTop);
        DrawRect(spriteBatch, new Rectangle(x - 2, floatTop + 1, 5, 2), FloatRed);
        DrawRect(spriteBatch, new Rectangle(x - 2, floatTop + 3, 5, 2), FloatWhite);
        DrawRect(spriteBatch, new Rectangle(x - 1, floatTop + 5, 3, 1), FloatWhiteBottom);
    }

    /// <summary>The trout silhouette, with a faint light edge along its top so it reads against deep water.</summary>
    private void DrawFish(SpriteBatch spriteBatch, Rectangle iconBox)
    {
        var icon = UiRenderer.Instance!.GetItemIcon(SILHOUETTE_SPRITE);
        var mask = GetFishMask(icon);

        if (mask is not null)
            DrawTextureFitted(spriteBatch, mask, new Rectangle(iconBox.X, iconBox.Y - 1, iconBox.Width, iconBox.Height), FishRim);

        DrawTextureFitted(spriteBatch, icon, iconBox, Silhouette);
    }

    /// <summary>A white copy of the icon's shape, so the light edge can be any colour. Skipped for atlas-packed icons.</summary>
    private Texture2D? GetFishMask(Texture2D icon)
    {
        if (FishMaskTried)
            return FishMask;

        FishMaskTried = true;

        if (icon is CachedTexture2D { AtlasRegion: not null })
            return null;

        using var scope = new PixelBufferScope(icon);

        foreach (ref var pixel in scope.AsSpan())
            pixel = new Color(pixel.A, pixel.A, pixel.A, pixel.A);

        FishMask = new Texture2D(ChaosGame.Device, scope.Width, scope.Height);
        scope.CommitTo(FishMask);

        return FishMask;
    }

    /// <summary>A recessed groove in the frame's browns, filling upward from red through amber to green, with quarter ticks.</summary>
    private void DrawMeter(SpriteBatch spriteBatch, Rectangle meter)
    {
        DrawRect(spriteBatch, meter, MeterGroove);
        DrawRect(spriteBatch, new Rectangle(meter.X, meter.Y, 1, meter.Height), MeterShadow);
        DrawRect(spriteBatch, new Rectangle(meter.X, meter.Y, meter.Width, 1), MeterShadow);
        DrawRect(spriteBatch, new Rectangle(meter.Right - 1, meter.Y, 1, meter.Height), MeterLight);
        DrawRect(spriteBatch, new Rectangle(meter.X, meter.Bottom - 1, meter.Width, 1), MeterLight);

        var percent = Math.Clamp(ProgressShown, 0f, 100f);
        var fill = (int)((meter.Height - 4) * percent / 100f);

        if (fill > 0)
        {
            var color = FishingLook.MeterColor(percent);
            var fillTop = meter.Bottom - 2 - fill;

            DrawRect(spriteBatch, new Rectangle(meter.X + 2, fillTop, meter.Width - 4, fill), color);
            DrawRect(spriteBatch, new Rectangle(meter.X + 2, fillTop, 1, fill), Color.Lerp(color, Color.White, 0.35f));
            DrawRect(spriteBatch, new Rectangle(meter.X + 2, fillTop, meter.Width - 4, 1), Color.Lerp(color, Color.White, 0.25f));
        }

        foreach (var quarter in (ReadOnlySpan<float>)[0.25f, 0.5f, 0.75f])
            DrawRect(spriteBatch, new Rectangle(meter.X - 2, meter.Y + (int)(meter.Height * (1f - quarter)), 2, 1), MeterTick);
    }

    private static float Mod(float value, float length)
    {
        var wrapped = value % length;

        return wrapped < 0f ? wrapped + length : wrapped;
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
