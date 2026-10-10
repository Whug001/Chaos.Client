#region
using Chaos.Client.Collections;
using Chaos.Client.Controls.Components;
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
///     Tumble Tower drawing. Floors are side by side on one map; the client shows only the viewer's floor and draws the
///     floor below inside holes. A fall is a same-map move: the camera follows the player to the landing tile at once,
///     and the floor they fell from is drawn shifted by the same amount, so the move itself is invisible.
/// </summary>
public sealed partial class WorldScreen
{
    private const float HOLE_DIM = 0.45f;
    private const float DIVE_LOWER_DIM = 0.55f;
    private const float SINK_PIXELS = 40f;
    private const float DROP_PIXELS = 60f;
    private const double FUNNEL_SECONDS = 0.5;

    private readonly List<TumbleFall> TumbleFallsBuffer = [];
    private UILabel? TumbleLabel;
    private (int Floor, int Floors, int Left, int? Seconds)? TumbleLabelShown;
    private TumbleOozeFrames? TumbleOoze;

    /// <summary>What this frame shows, worked out once at the start of <see cref="Draw" />; null off the tower.</summary>
    private TumbleView? TumbleViewNow;

    private static TumbleTowerState Tumble => WorldState.TumbleTower;

    private void WireTumbleTower()
    {
        Game.Connection.OnTumbleTowerState += HandleTumbleTowerState;
        Overlays.IsHidden = id => TumbleViewNow is not null && WorldState.GetEntity(id) is { } e && TumbleHidesOverlays(e);
    }

    private void UnwireTumbleTower()
    {
        Game.Connection.OnTumbleTowerState -= HandleTumbleTowerState;
        Overlays.IsHidden = null;
        TumbleOoze?.Dispose();
        TumbleOoze = null;
    }

    private static void HandleTumbleTowerState(TumbleTowerStateArgs args) => Tumble.Apply(args, TumbleClock.Now);

    private void ResetTumbleTower()
    {
        Tumble.Clear();
        TumbleViewNow = null;
    }

    private void RefreshTumbleView() => TumbleViewNow = ComputeTumbleView(TumbleClock.Now);

    /// <summary>
    ///     The floor on screen, and the tile shift that puts it under the camera. While the local player sinks, the server
    ///     has already moved them to the landing tile, so the floor they are leaving is shown shifted by the fall. During
    ///     the dive (and a knockout's landing) nothing but the floors is drawn.
    /// </summary>
    private TumbleView? ComputeTumbleView(double now)
    {
        if (!Tumble.IsActive
            || MapFile is null
            || WorldState.GetPlayerEntity() is not { } player
            || Tumble.FloorAt(player.TileX, player.TileY) is not { } viewer)
            return null;

        if (FallFrame(WorldState.PlayerEntityId, now) is not { } dive)
            return new TumbleView(viewer, 0, 0, false);

        (var fall, var frame) = dive;

        switch (frame.Phase)
        {
            case TumbleDivePhase.Sink:
                (var sx, var sy) = SpotOn(fall, frame.UpperFloor);

                return new TumbleView(frame.UpperFloor, player.TileX - sx, player.TileY - sy, false);

            case TumbleDivePhase.Dive:
            case TumbleDivePhase.Land when fall.Outcome == TumbleTowerFallOutcome.KnockedOut:
                return new TumbleView(viewer, 0, 0, true);

            default:
                return new TumbleView(viewer, 0, 0, false);
        }
    }

    private (TumbleFall Fall, TumbleDiveFrame Frame)? FallFrame(uint entityId, double now)
    {
        if (Tumble.ActiveFall(entityId, now) is not { } fall)
            return null;

        var fromFloor = Tumble.FloorAt(fall.FromX, fall.FromY) ?? 0;

        return (fall, TumbleDiveTimeline.At(fall, now - fall.StartedAt, fromFloor));
    }

    /// <summary>The fall's starting tile as it sits on <paramref name="floor" />.</summary>
    private (int X, int Y) SpotOn(TumbleFall fall, int floor)
    {
        var fromFloor = Tumble.FloorAt(fall.FromX, fall.FromY) ?? floor;

        return Tumble.SameSpotOn(fromFloor, fall.FromX, fall.FromY, floor);
    }

    /// <summary>World-pixel shift that draws <paramref name="floor" />'s copy of the fall tile under the player.</summary>
    private Vector2 ShiftFor(TumbleFall fall, int floor, WorldEntity player)
    {
        (var sx, var sy) = SpotOn(fall, floor);

        return Camera.TileToWorld(player.TileX, player.TileY, MapFile!.Height) - Camera.TileToWorld(sx, sy, MapFile.Height);
    }

    private bool TumbleHidesTile(int x, int y)
        => TumbleViewNow is { } view && (view.HideWorld || view.Shifted || (Tumble.FloorAt(x, y) != view.Floor));

    /// <summary>True when no part of <paramref name="entity" /> is drawn this frame: another floor, or mid-fall.</summary>
    private bool TumbleHidesEntity(WorldEntity entity, TumbleView view)
    {
        if (view.HideWorld)
            return true;

        var local = entity.Id == WorldState.PlayerEntityId;

        if (!local && (Tumble.FloorAt(entity.TileX, entity.TileY) != view.Floor))
            return true;

        if (FallFrame(entity.Id, TumbleClock.Now) is not { } f)
            return false;

        return f.Frame.Phase switch
        {
            //other players' sinks are drawn on the tile they left, by DrawTumbleOverlays
            TumbleDivePhase.Sink => !local,
            TumbleDivePhase.Dive => true,
            TumbleDivePhase.Land => f.Fall.Outcome == TumbleTowerFallOutcome.KnockedOut,
            _                    => false
        };
    }

    /// <summary>
    ///     Name tags, bars, bubbles and effects sit on the entity's real tile, so they are also hidden while the screen
    ///     shows a shifted floor; the local player's real tile is the one under the camera, so theirs stay.
    /// </summary>
    private bool TumbleHidesOverlays(WorldEntity entity)
        => TumbleViewNow is { } view
           && (TumbleHidesEntity(entity, view) || (view.Shifted && (entity.Id != WorldState.PlayerEntityId)));

    /// <summary>
    ///     True when <paramref name="entity" /> should not be drawn; otherwise the tile to draw it on and the extra
    ///     downward offset and alpha for its part of a fall (sinking or dropping in).
    /// </summary>
    private bool TumbleSkipsEntity(
        WorldEntity entity,
        out int tileX,
        out int tileY,
        out float yOffset,
        out float alphaScale)
    {
        tileX = entity.TileX;
        tileY = entity.TileY;
        yOffset = 0;
        alphaScale = 1;

        if (TumbleViewNow is not { } view)
            return false;

        if (TumbleHidesEntity(entity, view))
            return true;

        if (entity.Id != WorldState.PlayerEntityId)
        {
            tileX += view.ShiftX;
            tileY += view.ShiftY;
        }

        if (FallFrame(entity.Id, TumbleClock.Now) is not { } f)
            return false;

        switch (f.Frame.Phase)
        {
            case TumbleDivePhase.Sink:
                var k = (float)f.Frame.Progress;
                yOffset = k * k * SINK_PIXELS;
                alphaScale = 1f - 0.6f * k;

                break;

            case TumbleDivePhase.Land:
                var landed = f.Frame.Progress * TumbleDiveTimeline.LAND_SECONDS;

                if (landed < TumbleDiveTimeline.LAND_DROP_SECONDS)
                {
                    var q = (float)(landed / TumbleDiveTimeline.LAND_DROP_SECONDS);
                    yOffset = -(1f - q * q) * DROP_PIXELS;
                }

                break;
        }

        return false;
    }

    /// <summary>Replaces <c>MapRenderer.DrawBackground</c> while a Tumble Tower match is on.</summary>
    private void DrawTumbleBackground(SpriteBatch batch)
    {
        TumbleOoze ??= new TumbleOozeFrames(Device);
        var now = TumbleClock.Now;

        if ((WorldState.GetPlayerEntity() is not { } player) || (Tumble.FloorAt(player.TileX, player.TileY) is not { } viewer))
        {
            MapRenderer.DrawBackground(batch, MapFile!, Camera, AnimationTick);

            return;
        }

        if (FallFrame(WorldState.PlayerEntityId, now) is not { } dive)
        {
            DrawTumbleFloor(batch, viewer, Vector2.Zero, 1f, 1f, 0f, true, now);

            return;
        }

        (var fall, var frame) = dive;

        switch (frame.Phase)
        {
            case TumbleDivePhase.Sink:
                DrawTumbleFloor(batch, frame.UpperFloor, ShiftFor(fall, frame.UpperFloor, player), 1f, 1f, 0f, true, now);

                break;

            case TumbleDivePhase.Dive:
                DrawDiveSegment(batch, fall, frame, player, now);

                break;

            //knocked out through the bottom: dark until the server moves them out
            case TumbleDivePhase.Land when fall.Outcome == TumbleTowerFallOutcome.KnockedOut:
                DrawBlack(batch, 1f);

                break;

            default:
                DrawTumbleFloor(batch, viewer, Vector2.Zero, 1f, 1f, 0f, true, now);

                //a respawn after a dive into darkness fades back in while the player drops in
                if (frame is { Phase: TumbleDivePhase.Land, IntoDarkness: true })
                    DrawBlack(
                        batch,
                        1f - (float)Math.Clamp(frame.Progress * TumbleDiveTimeline.LAND_SECONDS / TumbleDiveTimeline.LAND_DROP_SECONDS, 0, 1));

                break;
        }
    }

    private void DrawDiveSegment(SpriteBatch batch, TumbleFall fall, TumbleDiveFrame frame, WorldEntity player, double now)
    {
        if (TumbleDiveFade.StyleFor(ClientSettings.ReduceTumbleMotion) == TumbleDiveStyle.Fade)
        {
            var fade = TumbleDiveFade.At(frame);

            if (fade.ShowUpper)
                DrawTumbleFloor(batch, frame.UpperFloor, ShiftFor(fall, frame.UpperFloor, player), 1f, 1f, 0f, true, now);
            else if (frame.LowerFloor is { } lower)
                DrawTumbleFloor(batch, lower, ShiftFor(fall, lower, player), 1f, 1f, 0f, true, now);

            DrawBlack(batch, (float)fade.Black);

            return;
        }

        var k = (float)frame.Progress;
        var e = k * k * (3f - 2f * k);

        if (frame.LowerFloor is { } next)
            DrawTumbleFloor(batch, next, ShiftFor(fall, next, player), 0.85f + 0.15f * e, 1f, DIVE_LOWER_DIM * (1f - e), true, now);

        DrawTumbleFloor(batch, frame.UpperFloor, ShiftFor(fall, frame.UpperFloor, player), 1f + 0.9f * e, 1f - e, 0f, false, now);
    }

    private void DrawBlack(SpriteBatch batch, float alpha)
    {
        if (alpha <= 0)
            return;

        batch.Draw(TumbleOoze!.Pixel, new Rectangle(0, 0, Camera.ViewportWidth, Camera.ViewportHeight), Color.Black * Math.Min(1f, alpha));
    }

    private Vector2 TumbleFocus()
    {
        var player = WorldState.GetPlayerEntity();

        if (player is null)
            return new Vector2(Camera.ViewportWidth / 2f, Camera.ViewportHeight / 2f);

        var world = Camera.TileToWorld(player.TileX, player.TileY, MapFile!.Height);

        return Camera.WorldToScreen(world + new Vector2(DaLibConstants.HALF_TILE_WIDTH, DaLibConstants.HALF_TILE_HEIGHT));
    }

    /// <param name="shift">World pixels added to every tile, to draw another floor under the camera.</param>
    /// <param name="scale">Scale about the player's tile on screen.</param>
    /// <param name="dim">0 = normal, 1 = black.</param>
    /// <param name="holes">Draw the floor below inside open tiles; false leaves holes empty.</param>
    private void DrawTumbleFloor(
        SpriteBatch batch,
        int floor,
        Vector2 shift,
        float scale,
        float alpha,
        float dim,
        bool holes,
        double now)
    {
        if ((floor < 0) || (floor >= Tumble.FloorsInUse) || (alpha <= 0))
            return;

        var rect = Tumble.Floors[floor];
        var focus = TumbleFocus();
        var shade = 1f - dim;
        var tint = new Color(shade, shade, shade) * alpha;
        var hasBelow = floor + 1 < Tumble.FloorsInUse;

        for (var ly = 0; ly < rect.Size; ly++)
            for (var lx = 0; lx < rect.Size; lx++)
            {
                var mx = rect.X + lx;
                var my = rect.Y + ly;
                var screen = Camera.WorldToScreen(Camera.TileToWorld(mx, my, MapFile!.Height) + shift);
                screen = focus + (screen - focus) * scale;

                if (((screen.X + DaLibConstants.TILE_WIDTH * scale) <= 0)
                    || (screen.X >= Camera.ViewportWidth)
                    || ((screen.Y + DaLibConstants.TILE_HEIGHT * scale) <= 0)
                    || (screen.Y >= Camera.ViewportHeight))
                    continue;

                var tile = Tumble.TileAt(mx, my, now);

                if (tile.State == TumbleTowerTileState.Open)
                {
                    if (holes && hasBelow)
                    {
                        (var bx, var by) = Tumble.SameSpotOn(floor, mx, my, floor + 1);

                        if (Tumble.TileAt(bx, by, now).State != TumbleTowerTileState.Open)
                        {
                            var below = 1f - HOLE_DIM;

                            MapRenderer.DrawBackgroundTile(
                                batch,
                                MapFile.Tiles[bx, by].Background,
                                screen,
                                scale,
                                new Color(below, below, below) * alpha,
                                AnimationTick);
                        }
                    }

                    if (holes)
                        batch.Draw(TumbleOoze!.Shaft, screen, null, Color.White * alpha, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

                    if (tile.SecondsSinceChange < FUNNEL_SECONDS)
                        batch.Draw(
                            TumbleOoze!.FunnelFrame(tile.SecondsSinceChange / FUNNEL_SECONDS),
                            screen,
                            null,
                            Color.White * alpha,
                            0f,
                            Vector2.Zero,
                            scale,
                            SpriteEffects.None,
                            0f);

                    continue;
                }

                MapRenderer.DrawBackgroundTile(batch, MapFile.Tiles[mx, my].Background, screen, scale, tint, AnimationTick);

                if (tile.State == TumbleTowerTileState.Melting)
                    batch.Draw(
                        TumbleOoze!.MeltFrame(tile.Progress),
                        screen,
                        null,
                        Color.White * alpha,
                        0f,
                        Vector2.Zero,
                        scale,
                        SpriteEffects.None,
                        0f);
            }
    }

    /// <summary>Other players sinking on the shown floor (they are already on the floor below), then dust and stars.</summary>
    private void DrawTumbleOverlays(BatchBlendScope scope)
    {
        if (TumbleViewNow is not { } view || view.HideWorld)
            return;

        scope.Require(BlendState.AlphaBlend);
        var now = TumbleClock.Now;

        Tumble.CollectActiveFalls(now, TumbleFallsBuffer);

        foreach (var fall in TumbleFallsBuffer)
        {
            var fromFloor = Tumble.FloorAt(fall.FromX, fall.FromY) ?? 0;
            var frame = TumbleDiveTimeline.At(fall, now - fall.StartedAt, fromFloor);
            var local = fall.EntityId == WorldState.PlayerEntityId;

            if (!local
                && (frame.Phase == TumbleDivePhase.Sink)
                && (fromFloor == view.Floor)
                && (WorldState.GetEntity(fall.EntityId) is { } sinking))
            {
                var k = (float)frame.Progress;

                DrawEntityAt(
                    scope.Batch,
                    sinking,
                    fall.FromX + view.ShiftX,
                    fall.FromY + view.ShiftY,
                    k * k * SINK_PIXELS,
                    1f - 0.6f * k);
            }

            if ((frame.Phase == TumbleDivePhase.Land)
                && (fall.Outcome != TumbleTowerFallOutcome.KnockedOut)
                && (Tumble.FloorAt(fall.ToX, fall.ToY) == view.Floor))
                DrawLandingPuff(scope.Batch, fall.ToX + view.ShiftX, fall.ToY + view.ShiftY, frame.Progress * TumbleDiveTimeline.LAND_SECONDS);
        }
    }

    private void DrawLandingPuff(SpriteBatch batch, int tileX, int tileY, double sinceLanding)
    {
        var centre = Camera.WorldToScreen(
            Camera.TileToWorld(tileX, tileY, MapFile!.Height) + new Vector2(DaLibConstants.HALF_TILE_WIDTH, DaLibConstants.HALF_TILE_HEIGHT));
        var dust = (float)((sinceLanding - TumbleDiveTimeline.LAND_DROP_SECONDS) / 0.45);

        if (dust is > 0 and < 1)
            for (var i = 0; i < 10; i++)
            {
                var a = i / 10f * MathF.Tau;
                var pos = centre + new Vector2(MathF.Cos(a) * 22 * dust, MathF.Sin(a) * 8 * dust - 4 * dust);
                var size = (int)Math.Max(1, 3 * (1 - dust) + 1);
                batch.Draw(TumbleOoze!.Pixel, new Rectangle((int)pos.X, (int)pos.Y, size, size), new Color(170, 150, 120) * (0.7f * (1 - dust)));
            }

        var stars = (float)((sinceLanding - TumbleDiveTimeline.LAND_DROP_SECONDS) / 0.95);

        if (stars is > 0 and < 1)
            for (var i = 0; i < 3; i++)
            {
                var a = (float)(TumbleClock.Now * 3.3) + i * 2.1f;
                var pos = centre + new Vector2(MathF.Cos(a) * 12, -50 + MathF.Sin(a) * 4);
                batch.Draw(TumbleOoze!.Pixel, new Rectangle((int)pos.X - 1, (int)pos.Y - 1, 3, 3), new Color(255, 230, 120) * (1 - stars * 0.6f));
            }
    }

    private void UpdateTumbleLabel()
    {
        if (TumbleViewNow is not { } view)
        {
            if (TumbleLabel is not null)
                TumbleLabel.Visible = false;

            TumbleLabelShown = null;

            return;
        }

        var viewport = WorldHud.ViewportBounds;

        if (TumbleLabel is null)
        {
            TumbleLabel = new UILabel
            {
                Name = "TumbleTowerLabel",
                Width = 300,
                Height = TextRenderer.CHAR_HEIGHT,
                PaddingLeft = 0,
                PaddingRight = 0,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                ForegroundColor = Color.White,
                IsHitTestVisible = false
            };

            Root!.AddChild(TumbleLabel);
        }

        //the HUD can switch size, which moves the viewport
        TumbleLabel.X = viewport.X + 8;
        TumbleLabel.Y = viewport.Top + 10;

        TumbleLabel.Visible = true;
        var shown = (view.Floor, Tumble.FloorsInUse, Tumble.PlayersLeft, Tumble.SecondsLeft);

        if (TumbleLabelShown == shown)
            return;

        TumbleLabelShown = shown;
        var left = Tumble.PlayersLeft;
        var text = $"Floor {view.Floor + 1} of {Tumble.FloorsInUse} · {left} {(left == 1 ? "player" : "players")} left";

        if (Tumble.SecondsLeft is { } s)
            text += $" · {s / 60}:{s % 60:D2}";

        TumbleLabel.Text = text;
    }

    private readonly record struct TumbleView(int Floor, int ShiftX, int ShiftY, bool HideWorld)
    {
        public bool Shifted => (ShiftX != 0) || (ShiftY != 0);
    }
}
