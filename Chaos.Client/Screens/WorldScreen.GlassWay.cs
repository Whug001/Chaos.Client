#region
using Chaos.Client.Collections;
using Chaos.Client.Controls.Components;
using Chaos.Client.Definitions;
using Chaos.Client.Models;
using Chaos.Client.Rendering;
using Chaos.Client.Systems;
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Screens;

/// <summary>
///     The Glass Way drawing. Lanes are plain floor on the map; the client draws the gold runes of the shown path, trail
///     marks, safe rows, broken panes and shards over them. A player who falls sinks through the pane; the local player
///     then dives into darkness and stays in the dark until their ghost appears.
/// </summary>
public sealed partial class WorldScreen
{
    private const float GLASS_SINK_PIXELS = 40f;
    private const double GLASS_FADE_TO_BLACK_SECONDS = 0.25;
    private const int GLASS_GO_SCALE = 4;
    private static readonly Color GlassGold = new(255, 205, 90);

    private readonly List<GlassCrack> GlassCracksBuffer = [];
    private GlassWayFrames? GlassArt;
    private UILabel? GlassLabel;
    private string? GlassLabelShown;

    /// <summary>The local player's fall this frame, worked out once at the start of <see cref="Draw" />; null when not falling.</summary>
    private GlassFallFrame? GlassLocalFall;

    private static GlassWayState Glass => WorldState.GlassWay;

    /// <summary>True while the local player dives or waits in the dark: nothing but the dive is drawn.</summary>
    private bool GlassHidesWorld => GlassLocalFall is { Phase: GlassFallPhase.Dive or GlassFallPhase.Land };

    private void WireGlassWay() => Game.Connection.OnGlassWayState += HandleGlassWayState;

    private void UnwireGlassWay()
    {
        Game.Connection.OnGlassWayState -= HandleGlassWayState;
        GlassArt?.Dispose();
        GlassArt = null;
    }

    private static void HandleGlassWayState(GlassWayStateArgs args) => Glass.Apply(args, TumbleClock.Now);

    private void ResetGlassWay()
    {
        Glass.Clear();
        GlassLocalFall = null;
    }

    private void RefreshGlassView() => GlassLocalFall = Glass.IsActive ? Glass.FallFrame(WorldState.PlayerEntityId, TumbleClock.Now) : null;

    /// <summary>True when the server holds the local player on their start ledge, so a walk would only be snapped back.</summary>
    private bool GlassRefusesStep()
    {
        if (!Glass.IsActive || (Glass.Phase == GlassWayPhase.Cross) || (WorldState.GetPlayerEntity() is not { } player))
            return false;

        //a player knocked out for never leaving the ledge is a ghost there and must be able to walk off
        if (player.IsDead || (Glass.LaneOf(player.Id) is null))
            return false;

        if (Glass.HasFallen(player.Id))
            return Glass.FallFrame(player.Id, TumbleClock.Now) is not { Phase: GlassFallPhase.Done };

        return GlassWayGeometry.Locate(player.TileX, player.TileY).Zone == GlassWayZone.StartLedge;
    }

    /// <summary>True when <paramref name="entity" /> is not drawn this frame; otherwise lowers and fades a sinking player.</summary>
    private bool GlassSkipsEntity(WorldEntity entity, ref float yOffset, ref float alphaScale)
    {
        if (!Glass.IsActive)
            return false;

        if (GlassHidesWorld)
            return true;

        if (Glass.FallFrame(entity.Id, TumbleClock.Now) is not { } fall)
            return false;

        switch (fall.Phase)
        {
            case GlassFallPhase.Sink:
                var k = (float)fall.Progress;
                yOffset += k * k * GLASS_SINK_PIXELS;
                alphaScale *= 1f - 0.6f * k;

                return false;

            case GlassFallPhase.Dive:
            case GlassFallPhase.Land:
                return true;

            default:
                return false;
        }
    }

    private bool GlassHidesOverlays(WorldEntity entity)
        => Glass.IsActive && (GlassHidesWorld || Glass.FallFrame(entity.Id, TumbleClock.Now) is { Phase: GlassFallPhase.Dive or GlassFallPhase.Land });

    /// <summary>Runs in the background pass after the map tiles: pane overlays, or the local player's dive.</summary>
    private void DrawGlassWay(SpriteBatch batch)
    {
        if (!Glass.IsActive || MapFile is null)
            return;

        GlassArt ??= new GlassWayFrames(Device);
        var now = TumbleClock.Now;

        switch (GlassLocalFall)
        {
            case { Phase: GlassFallPhase.Dive } dive:
                DrawGlassDive(batch, dive.Progress);

                return;

            //dark, then fading back in on the lanes just before the ghost appears
            case { Phase: GlassFallPhase.Land } land:
                DrawGlassBlack(batch, land.Progress < 0.5 ? 1f : (float)(2 - 2 * land.Progress));

                return;
        }

        Glass.CollectHoles(GlassCracksBuffer);

        foreach (var hole in GlassCracksBuffer)
            DrawOnPane(batch, GlassArt.Hole, hole.Lane, hole.Row, hole.Column, 1f);

        foreach (var lane in Glass.LanesInUse)
            for (var row = 0; row < GlassWayGeometry.MAX_GLASS_ROWS; row++)
                for (var column = 0; column < GlassWayGeometry.LANE_WIDTH; column++)
                {
                    if (Glass.IsSafeRow(row))
                        DrawOnPane(batch, GlassArt.Safe, lane, row, column, 1f);

                    if (Glass.IsMarked(lane, row, column))
                        DrawOnPane(batch, GlassArt.Mark, lane, row, column, 1f);

                    var glow = Glass.GlowAt(row, column, now);

                    if (glow > 0)
                        DrawOnPane(batch, GlassArt.Rune, lane, row, column, (float)glow);
                }

        Glass.CollectShattering(now, GlassCracksBuffer);

        foreach (var crack in GlassCracksBuffer)
            DrawOnPane(
                batch,
                GlassArt.ShardFrame((now - crack.StartedAt) / GlassWayState.SHATTER_SECONDS),
                crack.Lane,
                crack.Row,
                crack.Column,
                1f);
    }

    private void DrawOnPane(SpriteBatch batch, Texture2D texture, int lane, int row, int column, float alpha)
    {
        (var x, var y) = GlassWayGeometry.GlassTile(lane, row, column);
        var screen = Camera.WorldToScreen(Camera.TileToWorld(x, y, MapFile!.Height));

        if (((screen.X + DaLibConstants.TILE_WIDTH) <= 0)
            || (screen.X >= Camera.ViewportWidth)
            || ((screen.Y + DaLibConstants.TILE_HEIGHT) <= 0)
            || (screen.Y >= Camera.ViewportHeight))
            return;

        batch.Draw(texture, screen, Color.White * alpha);
    }

    private void DrawGlassDive(SpriteBatch batch, double progress)
    {
        DrawGlassBlack(batch, 1f);

        if (ClientSettings.ReduceTumbleMotion)
        {
            //"Reduce Arena Fall Motion": a quick fade to black instead of the zoom
            var t = progress * GlassWayTiming.DIVE_SECONDS;

            if (t < GLASS_FADE_TO_BLACK_SECONDS)
                DrawGlassLanes(batch, 1f, (float)(1 - t / GLASS_FADE_TO_BLACK_SECONDS));

            return;
        }

        var k = (float)progress;
        var e = k * k * (3f - 2f * k);
        DrawGlassLanes(batch, 1f + 0.9f * e, 1f - e);
    }

    /// <summary>Draws every lane's tiles scaled about the player's tile, over the black, so the lanes rush past and fade.</summary>
    private void DrawGlassLanes(SpriteBatch batch, float scale, float alpha)
    {
        if (alpha <= 0)
            return;

        var focus = TumbleFocus();
        var tint = Color.White * alpha;

        for (var lane = 0; lane < GlassWayGeometry.LANES; lane++)
            for (var column = 0; column < GlassWayGeometry.LANE_WIDTH; column++)
                for (var y = GlassWayGeometry.FAR_LEDGE_TOP_Y; y < GlassWayGeometry.START_LEDGE_TOP_Y + GlassWayGeometry.LEDGE_ROWS; y++)
                {
                    var x = GlassWayGeometry.LaneX(lane) + column;
                    var screen = Camera.WorldToScreen(Camera.TileToWorld(x, y, MapFile!.Height));
                    screen = focus + (screen - focus) * scale;

                    if (((screen.X + DaLibConstants.TILE_WIDTH * scale) <= 0)
                        || (screen.X >= Camera.ViewportWidth)
                        || ((screen.Y + DaLibConstants.TILE_HEIGHT * scale) <= 0)
                        || (screen.Y >= Camera.ViewportHeight))
                        continue;

                    MapRenderer.DrawBackgroundTile(batch, MapFile.Tiles[x, y].Background, screen, scale, tint, AnimationTick);
                }
    }

    private void DrawGlassBlack(SpriteBatch batch, float alpha)
    {
        if (alpha <= 0)
            return;

        batch.Draw(GlassArt!.Pixel, new Rectangle(0, 0, Camera.ViewportWidth, Camera.ViewportHeight), Color.Black * Math.Min(1f, alpha));
    }

    /// <summary>"Go!" and the crossing countdown bar under the label, in the world pass after entities.</summary>
    private void DrawGlassHud(BatchBlendScope scope)
    {
        if (!Glass.IsActive || GlassHidesWorld || (GlassArt is null))
            return;

        var now = TumbleClock.Now;

        if (Glass.ShowsGo(now))
        {
            scope.Require(BlendState.AlphaBlend);
            const string GO = "Go!";
            var width = TextRenderer.MeasureWidth(GO, GLASS_GO_SCALE);
            var at = new Vector2((Camera.ViewportWidth - width) / 2f, Camera.ViewportHeight / 3f);
            TextRenderer.DrawText(scope.Batch, at + new Vector2(GLASS_GO_SCALE, GLASS_GO_SCALE), GO, Color.Black, false, 0.7f, false, GLASS_GO_SCALE);
            TextRenderer.DrawText(scope.Batch, at, GO, GlassGold, false, 1f, false, GLASS_GO_SCALE);
        }

        if ((Glass.PhaseLengthSeconds <= 0) || (Glass.CrossSecondsLeft(now) is not { } left))
            return;

        scope.Require(BlendState.AlphaBlend);
        var bar = new Rectangle(8, 14 + TextRenderer.CHAR_HEIGHT, 200, 4);
        var filled = (int)(bar.Width * Math.Clamp(left / Glass.PhaseLengthSeconds, 0, 1));
        scope.Batch.Draw(GlassArt.Pixel, bar, Color.Black * 0.6f);
        scope.Batch.Draw(GlassArt.Pixel, new Rectangle(bar.X, bar.Y, filled, bar.Height), GlassGold);
    }

    private void UpdateGlassLabel()
    {
        if (!Glass.IsActive)
        {
            if (GlassLabel is not null)
                GlassLabel.Visible = false;

            GlassLabelShown = null;

            return;
        }

        var viewport = WorldHud.ViewportBounds;

        if (GlassLabel is null)
        {
            GlassLabel = new UILabel
            {
                Name = "GlassWayLabel",
                Width = 300,
                Height = TextRenderer.CHAR_HEIGHT,
                PaddingLeft = 0,
                PaddingRight = 0,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                ForegroundColor = Color.White,
                IsHitTestVisible = false
            };

            Root!.AddChild(GlassLabel);
        }

        //the HUD can switch size, which moves the viewport
        GlassLabel.X = viewport.X + 8;
        GlassLabel.Y = viewport.Top + 10;
        GlassLabel.Visible = true;

        var now = TumbleClock.Now;
        var text = Glass.Round == 0 ? "The Glass Way · get ready" : $"Round {Glass.Round} of {Glass.RoundCap} · {Glass.PlayersLeft} left";

        if (Glass.CrossSecondsLeft(now) is { } left)
            text += $" · {Math.Ceiling(left):0}s";

        if (text == GlassLabelShown)
            return;

        GlassLabelShown = text;
        GlassLabel.Text = text;
    }
}
