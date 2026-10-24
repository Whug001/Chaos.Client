#region
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

    private void WireCinema()
    {
        Cinema = new CinemaController(() => new CinemaPlayer(Device), () => CinemaPlayer.IsReady);
        Cinema.SetShowVideo(!ClientSettings.CinemaHideVideo, Environment.TickCount64);
        if (!ClientSettings.CinemaHideVideo)
            CinemaPlayer.Warm();
        Game.Connection.OnCinemaState += HandleCinemaState;
    }

    private void UnwireCinema()
    {
        Game.Connection.OnCinemaState -= HandleCinemaState;
        ResetCinema();
        CinemaPixel?.Dispose();
        CinemaPixel = null;
        CinemaUnavailableText?.Dispose();
        CinemaUnavailableText = null;
    }

    private void HandleCinemaState(CinemaStateArgs args)
        => Cinema?.Apply(args, Environment.TickCount64, LatencyMonitor.LatencyMs);

    private void UpdateCinema()
    {
        if (Cinema is null)
            return;

        var now = Environment.TickCount64;
        Cinema.SetShowVideo(!ClientSettings.CinemaHideVideo, now);
        Cinema.SetVolume(ClientSettings.CinemaVolume, ClientSettings.CinemaMute);
        if (!ClientSettings.CinemaHideVideo)
            CinemaPlayer.Warm();

        Cinema.Update(now);
        Game.SoundSystem.SetCinemaDuck(Cinema.ShouldDuckMusic(ClientSettings.CinemaLowerMusic));
    }

    private void ResetCinema()
    {
        Cinema?.Stop();
        Game.SoundSystem.SetCinemaDuck(false);
    }

    private bool CinemaScreenOn
        => Cinema is { IsShowing: true }
           && (CurrentMapId == CinemaScreenLayout.HALL_MAP_ID)
           && !ClientSettings.CinemaHideVideo
           && ((Cinema.Status == CinemaStatus.Unavailable) || Cinema.Video?.Frame is not null);

    private void DrawCinemaScreen(BatchBlendScope scope)
    {
        var spot = CinemaScreenLayout.Hall;
        var tile = Camera.WorldToScreen(Camera.TileToWorld(spot.AnchorX, spot.AnchorY, MapFile!.Height));
        var area = new Rectangle((int)MathF.Floor(tile.X) + spot.OffsetX, (int)MathF.Floor(tile.Y) + spot.OffsetY, spot.Width, spot.Height);

        if (CinemaPixel is null)
        {
            CinemaPixel = new Texture2D(Device, 1, 1);
            CinemaPixel.SetData([Color.White]);
        }

        scope.Require(BlendState.AlphaBlend);

        if (Cinema!.Status == CinemaStatus.Unavailable)
        {
            scope.Batch.Draw(CinemaPixel, area, new Color(40, 40, 40));
            CinemaUnavailableText ??= TextRenderer.BuildSmallText(Device, "Film unavailable");

            if (CinemaUnavailableText is { } text)
                scope.Batch.Draw(
                    text,
                    new Vector2(area.X + (area.Width - text.Width) / 2, area.Y + (area.Height - text.Height) / 2),
                    Color.White);

            return;
        }

        scope.Batch.Draw(CinemaPixel, area, Color.Black);
        scope.Batch.Draw(Cinema.Video!.Frame!, CinemaScreenLayout.FitPicture(area, CinemaScreenLayout.VIDEO_WIDTH, CinemaScreenLayout.VIDEO_HEIGHT), Color.White);
    }
}