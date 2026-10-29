#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Rendering;
using Chaos.Client.Systems.Karting;
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Karting;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Screens;

/// <summary>
///     The race display: the framed panel top left (place, lap, times, drift and speed), the minimap top right with the
///     race updates under it, and the item slot top centre.
/// </summary>
public sealed partial class WorldScreen
{
    private const int KART_MARGIN = 8;
    private const int KART_PANEL_WIDTH = 152;
    private const int KART_PANEL_HEIGHT = 94;
    private const int KART_MAP_WIDTH = 146;
    private const int KART_MAP_HEIGHT = 110;
    private const int KART_SPEED_SEGMENTS = 11;

    //the speed number reads like a speedometer rather than tiles per second
    private const double KART_SPEED_DISPLAY_SCALE = 5;

    private static readonly Color KartPanelFill = new Color(16, 14, 20) * 0.75f;
    private static readonly Color KartPanelBorder = new(120, 96, 50);
    private static readonly Color KartPanelInner = new(40, 32, 20);
    private static readonly Color KartDim = new(170, 160, 140);
    private static readonly Color KartUnlit = new(50, 46, 56);
    private static readonly Color KartRoad = new(150, 140, 120);
    private static readonly Color[] KartSparkColors = [new(80, 140, 255), new(250, 200, 60), new(190, 110, 255)];

    private readonly KartRaceFeed KartFeed = new();
    private KartMinimap? KartMap;
    private Texture2D? KartMapTexture;

    private void DrawKartHud(BatchBlendScope scope)
    {
        if (!KartRace.Active)
            return;

        var now = TumbleClock.Now;
        KartHudFrame++;
        var gloamingAlpha = KartRaceState.GloamingAlpha(KartGloamingUntil, now);

        if (gloamingAlpha > 0)
            UIElement.DrawRect(scope.Batch, new Rectangle(0, 0, Camera.ViewportWidth, Camera.ViewportHeight), Color.Black * gloamingAlpha);

        var estimate = KartClock.Estimate(now);
        var stage = KartRace.StageAt(estimate);

        if (KartRace.Spectating)
            DrawKartSpectatorPanel(scope.Batch, KartSpectatorLines(estimate, stage));
        else
            DrawKartRacePanel(scope.Batch, estimate, stage, now);

        DrawKartMinimap(scope.Batch, now);
        DrawKartFeed(scope.Batch, now);

        if (!KartRace.Spectating && KartRace.LocalIndex is { } localIndex && KartRace.Latest(localIndex) is { } local)
            DrawKartItemHud(scope.Batch, local.Wisps);

        DrawKartBanner(scope, estimate, stage, now);
    }

    private void DrawKartRacePanel(SpriteBatch batch, uint estimate, KartRaceStage stage, double now)
    {
        if (KartRace.LocalIndex is not { } index)
            return;

        var latest = KartRace.Latest(index);
        var x = KART_MARGIN;
        var y = KART_MARGIN;
        DrawKartPanel(batch, new Rectangle(x, y, KART_PANEL_WIDTH, KART_PANEL_HEIGHT));

        if (latest is { Place: > 0 })
        {
            var place = KartTime.Ordinal(latest.Place);
            TextRenderer.DrawText(batch, new Vector2(x + 6, y + 4), place, LegendColors.Gold, false, 1f, false, 2);
            TextRenderer.DrawText(batch, new Vector2(x + 10 + TextRenderer.MeasureWidth(place, 2), y + 14), $"/ {KartRace.Racers.Count}", KartDim, false);
        }

        TextRenderer.DrawText(batch, new Vector2(x + 96, y + 3), "LAP", KartDim, false);
        var lap = latest is null ? 1 : Math.Min(latest.LapsDone + 1, KartRace.Laps);
        TextRenderer.DrawText(batch, new Vector2(x + 96, y + 15), $"{lap} / {KartRace.Laps}", Color.White, false);
        UIElement.DrawRect(batch, new Rectangle(x + 6, y + 31, KART_PANEL_WIDTH - 12, 1), KartPanelBorder);

        var running = stage is KartRaceStage.Racing or KartRaceStage.Finishing or KartRaceStage.Results;
        var clock = running ? FormatRaceTime((estimate - KartRace.GoStep) / 30.0) : FormatRaceTime(0);

        if (KartRace.Finish(index) is { } finish)
            DrawKartRow(batch, y + 35, "Done", FormatRaceTime(finish.TotalMs / 1000.0), LegendColors.Gold);
        else
            DrawKartRow(batch, y + 35, "Time", clock, Color.White);

        var lapTimes = KartRace.LapTimes(index);
        DrawKartRow(batch, y + 48, "Best", lapTimes.Count > 0 ? FormatRaceTime(lapTimes.Min() / 1000.0) : "-", Color.White);

        if (latest?.Status == KartRacerStatus.Respawning && latest.Flags.HasFlag(KartRacerFlags.Held))
            DrawKartRow(batch, y + 61, "Drift", "Respawning", KartDim);
        else
            DrawKartDriftBar(batch, y + 61);

        DrawKartSpeed(batch, y + 74, index, now);
    }

    private void DrawKartRow(SpriteBatch batch, int y, string label, string value, Color color)
    {
        TextRenderer.DrawText(batch, new Vector2(KART_MARGIN + 6, y), label, KartDim, false);
        TextRenderer.DrawText(batch, new Vector2(KART_MARGIN + 50, y), value, color, false);
    }

    /// <summary>Three segments, lit up to the drift's spark level: blue, gold, violet.</summary>
    private void DrawKartDriftBar(SpriteBatch batch, int y)
    {
        TextRenderer.DrawText(batch, new Vector2(KART_MARGIN + 6, y), "Drift", KartDim, false);
        var spark = KartPredictor is not null && KartRace.Stats is { } stats ? DriftModel.SparkFor(KartPredictor.Mods.DriftCharge, stats) : SparkLevel.None;
        var lit = spark switch { SparkLevel.Blue => 1, SparkLevel.Gold => 2, SparkLevel.Violet => 3, _ => 0 };

        for (var i = 0; i < KartSparkColors.Length; i++)
            UIElement.DrawRect(batch, new Rectangle(KART_MARGIN + 50 + i * 30, y + 3, 26, 7), i < lit ? KartSparkColors[i] : KartUnlit);
    }

    /// <summary>The speed as a number and a bar of top speed; the bar turns gold past top speed, as on a boost.</summary>
    private void DrawKartSpeed(SpriteBatch batch, int y, byte index, double now)
    {
        TextRenderer.DrawText(batch, new Vector2(KART_MARGIN + 6, y), "Speed", KartDim, false);
        var speed = KartBodyFor(index, now)?.Speed ?? 0;
        var top = KartRace.Stats?.TopSpeed ?? 1;
        TextRenderer.DrawText(batch, new Vector2(KART_MARGIN + 50, y), Math.Round(speed * KART_SPEED_DISPLAY_SCALE).ToString("0"), Color.White, false);
        var lit = (int)Math.Round(Math.Clamp(speed / top, 0, 1) * KART_SPEED_SEGMENTS);
        var color = speed > top + 0.01 ? LegendColors.Gold : new Color(120, 200, 120);

        for (var i = 0; i < KART_SPEED_SEGMENTS; i++)
            UIElement.DrawRect(batch, new Rectangle(KART_MARGIN + 74 + i * 6, y + 2, 4, 9), i < lit ? color : KartUnlit);
    }

    private void DrawKartSpectatorPanel(SpriteBatch batch, List<string> lines)
    {
        if (lines.Count == 0)
            return;

        var width = Math.Max(KART_PANEL_WIDTH, lines.Max(line => TextRenderer.MeasureWidth(line)) + 14);
        DrawKartPanel(batch, new Rectangle(KART_MARGIN, KART_MARGIN, width, lines.Count * TextRenderer.CHAR_HEIGHT + 10));

        for (var i = 0; i < lines.Count; i++)
            TextRenderer.DrawText(batch, new Vector2(KART_MARGIN + 7, KART_MARGIN + 5 + i * TextRenderer.CHAR_HEIGHT), lines[i], i == 0 ? LegendColors.Gold : Color.White, false);
    }

    private void DrawKartMinimap(SpriteBatch batch, double now)
    {
        if (KartMap is null || KartMapTexture is null)
            return;

        var box = KartMapBox;
        DrawKartPanel(batch, box);
        batch.Draw(KartMapTexture, box.Location.ToVector2(), Color.White);

        //the racer the camera is on goes last so its dot is never covered
        var front = KartRace.Spectating ? KartFollowedIndex : KartRace.LocalIndex;

        foreach (var index in KartRace.DrawableIndexes().OrderBy(index => index == front))
        {
            if (KartBodyFor(index, now) is not { } body)
                continue;

            var at = box.Location.ToVector2() + KartMap.Project(body.X, body.Y);
            var color = index == front ? LegendColors.Gold : KartRace.Racers.TryGetValue(index, out var info) ? KartLayerTint.Dye(info.Dye) : Color.White;
            DrawKartDot(batch, at, index == front ? 3 : 2, color);
        }
    }

    private Rectangle KartMapBox => new(Camera.ViewportWidth - KART_MARGIN - KART_MAP_WIDTH, KART_MARGIN, KART_MAP_WIDTH, KART_MAP_HEIGHT);

    /// <summary>A round-ish dot: two crossed rectangles with a dark edge.</summary>
    private static void DrawKartDot(SpriteBatch batch, Vector2 at, int radius, Color color)
    {
        var x = (int)Math.Round(at.X);
        var y = (int)Math.Round(at.Y);
        UIElement.DrawRect(batch, new Rectangle(x - radius - 1, y - radius, radius * 2 + 3, radius * 2 + 1), Color.Black);
        UIElement.DrawRect(batch, new Rectangle(x - radius, y - radius - 1, radius * 2 + 1, radius * 2 + 3), Color.Black);
        UIElement.DrawRect(batch, new Rectangle(x - radius, y - radius + 1, radius * 2 + 1, radius * 2 - 1), color);
        UIElement.DrawRect(batch, new Rectangle(x - radius + 1, y - radius, radius * 2 - 1, radius * 2 + 1), color);
    }

    private void DrawKartFeed(SpriteBatch batch, double now)
    {
        var right = Camera.ViewportWidth - KART_MARGIN;
        var y = KartMap is null ? KART_MARGIN : KartMapBox.Bottom + 5;

        foreach (var (text, alpha) in KartFeed.Lines(now))
        {
            var width = TextRenderer.MeasureWidth(text);
            UIElement.DrawRect(batch, new Rectangle(right - width - 6, y - 1, width + 6, TextRenderer.CHAR_HEIGHT + 2), Color.Black * (0.55f * alpha));
            TextRenderer.DrawText(batch, new Vector2(right - width - 3, y), text, Color.White, false, alpha);
            y += TextRenderer.CHAR_HEIGHT + 3;
        }
    }

    private static void DrawKartPanel(SpriteBatch batch, Rectangle box)
    {
        UIElement.DrawRect(batch, box, KartPanelFill);
        DrawKartOutline(batch, box, KartPanelBorder);
        DrawKartOutline(batch, new Rectangle(box.X + 1, box.Y + 1, box.Width - 2, box.Height - 2), KartPanelInner);

        foreach (var corner in new[] { new Point(box.Left, box.Top), new Point(box.Right - 1, box.Top), new Point(box.Left, box.Bottom - 1), new Point(box.Right - 1, box.Bottom - 1) })
            UIElement.DrawRect(batch, new Rectangle(corner.X - 2, corner.Y - 2, 5, 5), LegendColors.Gold);
    }

    private static void DrawKartOutline(SpriteBatch batch, Rectangle box, Color color)
    {
        UIElement.DrawRect(batch, new Rectangle(box.X, box.Y, box.Width, 1), color);
        UIElement.DrawRect(batch, new Rectangle(box.X, box.Bottom - 1, box.Width, 1), color);
        UIElement.DrawRect(batch, new Rectangle(box.X, box.Y, 1, box.Height), color);
        UIElement.DrawRect(batch, new Rectangle(box.Right - 1, box.Y, 1, box.Height), color);
    }

    /// <summary>Paints the drivable tiles and the start line once per track; the dots are drawn over it each frame.</summary>
    private void BuildKartMinimap()
    {
        KartMapTexture?.Dispose();
        KartMapTexture = null;
        KartMap = null;

        if (KartRace.Surface is not { } surface)
            return;

        KartMap = new KartMinimap(surface, KART_MAP_WIDTH, KART_MAP_HEIGHT, 6);
        var pixels = new Color[KART_MAP_WIDTH * KART_MAP_HEIGHT];

        void Plot(Vector2 at, int size, Color color)
        {
            for (var dy = 0; dy < size; dy++)
                for (var dx = 0; dx < size; dx++)
                {
                    var px = (int)at.X + dx;
                    var py = (int)at.Y + dy;

                    if (px is >= 0 and < KART_MAP_WIDTH && py is >= 0 and < KART_MAP_HEIGHT)
                        pixels[py * KART_MAP_WIDTH + px] = color;
                }
        }

        foreach (var tile in KartMap.Tiles)
            Plot(KartMap.Project(tile.X + 0.5, tile.Y + 0.5) - Vector2.One, 2, KartRoad);

        if (KartRace.Gates.Count > 0)
        {
            var gate = KartRace.Gates[0];

            for (var t = 0.0; t <= 1; t += 0.02)
                Plot(KartMap.Project(gate.Ax + (gate.Bx - gate.Ax) * t, gate.Ay + (gate.By - gate.Ay) * t), 1, Color.White);
        }

        KartMapTexture = new Texture2D(Device, KART_MAP_WIDTH, KART_MAP_HEIGHT);
        KartMapTexture.SetData(pixels);
    }

    private void ClearKartHud()
    {
        KartFeed.Clear();
        KartMapTexture?.Dispose();
        KartMapTexture = null;
        KartMap = null;
    }
}
