#region
using Chaos.Client.Collections;
using Chaos.Client.Rendering;
using Chaos.Client.Systems;
using Chaos.Client.Systems.Cinema;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Screens;

public sealed partial class WorldScreen
{
    private CinemaController? Cinema;
    private Texture2D? CinemaPixel;
    private Texture2D? CinemaUnavailableText;
    private IReadOnlyList<CinemaWallColumn>? CinemaWallColumns;

    //the house-dim holes and rectangles, rebuilt only when the viewport or the camera moves
    private readonly List<Rectangle> CinemaHoles = [];
    private readonly List<Rectangle> CinemaDimRects = [];
    private Rectangle CinemaDimViewport;
    private Vector2 CinemaDimCamPos;
    private Vector2 CinemaDimCamOffset;
    private float CinemaDimZoom;
    private bool CinemaDimSmooth;

    private static float CinemaSnap(float value, bool smooth) => smooth ? MathF.Round(value) : MathF.Floor(value);

    private void WireCinema()
    {
        Cinema = new CinemaController(() => new CinemaPlayer(Device), () => CinemaPlayer.IsReady);
        Cinema.SetShowVideo(!ClientSettings.CinemaHideVideo, Environment.TickCount64);
        if (!ClientSettings.CinemaHideVideo)
            CinemaPlayer.Warm();
        SeatedBackdrop = CinemaAssets.Load(Device, "backdrop.png");
        SeatedCurtains = CinemaAssets.Load(Device, "curtains.png");
        SeatBackTexture = CinemaAssets.Load(Device, "seat_back.png");
        SeatPanelTexture = CinemaAssets.Load(Device, "seat_panel.png");
        CinemaHeads = new CinemaHeadRenderer(Game.AislingRenderer);
        WorldState.Chat.MessageAdded += HandleSeatedChat;
        Game.Connection.OnCinemaState += HandleCinemaState;
    }

    private void UnwireCinema()
    {
        LeaveSeatedUi();
        WorldState.Chat.MessageAdded -= HandleSeatedChat;
        Game.Connection.OnCinemaState -= HandleCinemaState;
        ResetCinema();
        CinemaPixel?.Dispose();
        CinemaPixel = null;
        CinemaUnavailableText?.Dispose();
        CinemaUnavailableText = null;
        SeatedBackdrop?.Dispose();
        SeatedBackdrop = null;
        SeatedCurtains?.Dispose();
        SeatedCurtains = null;
        SeatBackTexture?.Dispose();
        SeatPanelTexture?.Dispose();
        SeatBackTexture = null;
        SeatPanelTexture = null;
        SeatedWaitingText?.Dispose();
        SeatedWaitingText = null;
        SeatedHiddenText?.Dispose();
        SeatedHiddenText = null;
        CinemaHeads?.Dispose();
        CinemaHeads = null;
    }

    private void HandleCinemaState(CinemaStateArgs args)
        => Cinema?.Apply(args, Environment.TickCount64, LatencyMonitor.LatencyMs);

    private void UpdateCinema()
    {
        //the view opens without a film, so it updates even when Cinema is null
        if (Cinema is null)
        {
            UpdateSeatedView();

            return;
        }

        var now = Environment.TickCount64;
        Cinema.SetShowVideo(!ClientSettings.CinemaHideVideo, now);
        Cinema.SetVolume(ClientSettings.CinemaVolume, ClientSettings.CinemaMute);
        if (!ClientSettings.CinemaHideVideo)
            CinemaPlayer.Warm();

        Cinema.Update(now);
        Game.SoundSystem.SetCinemaDuck(Cinema.ShouldDuckMusic(ClientSettings.CinemaLowerMusic));

        UpdateSeatedView();
    }

    private void ResetCinema()
    {
        Cinema?.Stop();
        Game.SoundSystem.SetCinemaDuck(false);
    }

    private bool CinemaScreenOn
        => Cinema is { IsShowing: true }
           && (CurrentMapId == CinemaScreenLayout.ROOM_MAP_ID)
           && !ClientSettings.CinemaHideVideo
           && ((Cinema.Status == CinemaStatus.Unavailable) || Cinema.Video?.Frame is not null);

    /// <summary>
    ///     Loures Cinema house dim, like the Theatre's house lights: the room goes dark while the film on the wall
    ///     stays at full brightness. Drawn in screen space right after the darkness overlay (before the spotlights);
    ///     the seated view draws later over the whole window, so it looks unchanged.
    /// </summary>
    private void DrawCinemaDim(SpriteBatch spriteBatch, Matrix surfaceMatrix)
    {
        if (CurrentMapId != CinemaSeats.MAP_ID)
            return;

        var viewport = WorldHud.ViewportBounds;

        spriteBatch.Begin(
            blendState: BlendState.NonPremultiplied,
            samplerState: GlobalSettings.Sampler,
            rasterizerState: ScissorRasterizerState,
            transformMatrix: surfaceMatrix);

        try
        {
            if (!CinemaScreenOn)
            {
                RenderHelper.DrawRect(spriteBatch, viewport, Color.Black * CinemaScreenLayout.CINEMA_HOUSE_DARKNESS);

                return;
            }

            CinemaWallColumns ??= CinemaScreenLayout.WallColumns(
                CinemaScreenLayout.Wall,
                CinemaScreenLayout.VIDEO_WIDTH,
                CinemaScreenLayout.VIDEO_HEIGHT);

            //same floor + viewport-offset mapping as DrawCinemaWall's transformed batch, so the hole
            //lines up exactly with the drawn screen
            var smooth = ClientSettings.ScrollLevel > 0;

            if ((CinemaDimViewport != viewport)
                || (CinemaDimCamPos != Camera.Position)
                || (CinemaDimCamOffset != Camera.Offset)
                || (CinemaDimZoom != Camera.Zoom)
                || (CinemaDimSmooth != smooth)
                || (CinemaDimRects.Count == 0))
            {
                CinemaHoles.Clear();

                foreach (var column in CinemaWallColumns)
                {
                    var top = Camera.WorldToScreen(new Vector2(column.Back.X, column.Back.Y));

                    CinemaHoles.Add(
                        new Rectangle(
                            viewport.X + (int)CinemaSnap(top.X, smooth),
                            viewport.Y + (int)CinemaSnap(top.Y, smooth),
                            column.Back.Width,
                            column.Back.Height));
                }

                CinemaDimViewport = viewport;
                CinemaDimCamPos = Camera.Position;
                CinemaDimCamOffset = Camera.Offset;
                CinemaDimZoom = Camera.Zoom;
                CinemaDimSmooth = smooth;
                CinemaScreenLayout.DimAroundColumnsInto(viewport, CinemaHoles, CinemaDimRects);
            }

            foreach (var rect in CinemaDimRects)
                RenderHelper.DrawRect(spriteBatch, rect, Color.Black * CinemaScreenLayout.CINEMA_HOUSE_DARKNESS);
        } finally
        {
            spriteBatch.End();
        }
    }

    private void DrawCinemaWall(BatchBlendScope scope, int depth, int minDepth, int maxDepth)
    {
        CinemaWallColumns ??= CinemaScreenLayout.WallColumns(CinemaScreenLayout.Wall, CinemaScreenLayout.VIDEO_WIDTH, CinemaScreenLayout.VIDEO_HEIGHT);

        if (CinemaPixel is null)
        {
            CinemaPixel = new Texture2D(Device, 1, 1);
            CinemaPixel.SetData([Color.White]);
        }

        var smooth = ClientSettings.ScrollLevel > 0;
        scope.Require(BlendState.AlphaBlend);

        var unavailable = Cinema!.Status == CinemaStatus.Unavailable;

        foreach (var column in CinemaWallColumns)
        {
            if (CinemaScreenLayout.DrawStripe(CinemaScreenLayout.StripeOf(column.Cell), minDepth, maxDepth) != depth)
                continue;

            var backTop = Camera.WorldToScreen(new Vector2(column.Back.X, column.Back.Y));
            var back = new Rectangle((int)CinemaSnap(backTop.X, smooth), (int)CinemaSnap(backTop.Y, smooth), column.Back.Width, column.Back.Height);
            scope.Batch.Draw(CinemaPixel, back, unavailable ? new Color(40, 40, 40) : Color.Black);

            if (!unavailable && (column.Picture != Rectangle.Empty))
            {
                var pictureTop = Camera.WorldToScreen(new Vector2(column.Picture.X, column.Picture.Y));
                var picture = new Rectangle(
                    (int)CinemaSnap(pictureTop.X, smooth),
                    (int)CinemaSnap(pictureTop.Y, smooth),
                    column.Picture.Width,
                    column.Picture.Height);
                scope.Batch.Draw(Cinema.Video!.Frame!, picture, column.Source, Color.White);
            }
        }

        if (unavailable)
        {
            var last = CinemaWallColumns[CinemaWallColumns.Count - 1];

            if (CinemaScreenLayout.DrawStripe(CinemaScreenLayout.StripeOf(last.Cell), minDepth, maxDepth) == depth)
            {
                var wall = CinemaScreenLayout.Wall;
                var centre = Camera.WorldToScreen(new Vector2(wall.Left + wall.Width / 2, wall.Bottom - wall.Height / 2 + wall.Columns / 2));
                CinemaUnavailableText ??= TextRenderer.BuildSmallText(Device, "Film unavailable");

                if (CinemaUnavailableText is { } text)
                    scope.Batch.Draw(
                        text,
                        new Vector2(CinemaSnap(centre.X, smooth) - text.Width / 2, CinemaSnap(centre.Y, smooth) - text.Height / 2),
                        Color.White);
            }
        }
    }
}
