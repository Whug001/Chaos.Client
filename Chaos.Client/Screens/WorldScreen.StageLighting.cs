#region
using Chaos.Client.Collections;
using Chaos.Client.Controls.World.Popups.Theatre;
using Chaos.Client.Rendering;
using Chaos.Client.Systems;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Screens;

/// <summary>Theatre stage lighting: the setup from the server, per-frame spotlights, and their drawing.</summary>
public sealed partial class WorldScreen
{
    private readonly List<StageLightFrame> SpotlightFrames = [];
    private SpotlightDraw[] SpotlightDraws = new SpotlightDraw[16];
    private int SpotlightDrawCount;
    private float HouseDarknessNow;
    private SpotlightRenderer SpotlightRenderer = null!;

    //the director's Stage Lighting window — opened by the server's StageLightingBoard Open from Theatre Options
    private StageLightingControl StageLightingWindow = null!;

    private void WireStageLighting()
    {
        Game.Connection.OnStageLightingState += HandleStageLightingState;
        Game.Connection.OnStageLightingBoard += HandleStageLightingBoard;
        StageLightingWindow.EditRequested += SendStageLightingEdit;
    }

    private void UnwireStageLighting()
    {
        Game.Connection.OnStageLightingState -= HandleStageLightingState;
        Game.Connection.OnStageLightingBoard -= HandleStageLightingBoard;
        StageLightingWindow.EditRequested -= SendStageLightingEdit;
    }

    private void SendStageLightingEdit(StageLightingInteractionArgs args) => Game.Connection.SendStageLightingInteraction(args);

    private void HandleStageLightingState(StageLightingStateArgs args)
    {
        WorldState.StageLights.Apply(args, Environment.TickCount64);

        if (StageLightingWindow.Visible)
            StageLightingWindow.RefreshFromState();
    }

    private void HandleStageLightingBoard(StageLightingBoardArgs args)
    {
        switch (args.Type)
        {
            case StageLightingBoardType.Open:
                WorldState.StageLightingPanel.SetScenes(args.SceneNames);
                StageLightingWindow.Show();

                break;

            case StageLightingBoardType.Scenes:
                WorldState.StageLightingPanel.SetScenes(args.SceneNames);

                if (StageLightingWindow.Visible)
                    StageLightingWindow.RefreshFromState();

                break;

            case StageLightingBoardType.Close:
                StageLightingWindow.Hide();

                break;
        }
    }

    /// <summary>Forgets the Theatre lighting and closes the window. Called on a real map change only, before the darkness layer resets.</summary>
    private void ResetStageLighting()
    {
        //state first, window last: closing the window ends any drag, and with the panel state already reset that
        //drag has no held edit left to send to the server from the new map
        WorldState.StageLights.Clear();
        WorldState.StageLightingPanel.ResetForNewMap();
        StageLightingWindow.Hide();
        DarknessRenderer.SetHouseDarkness(null);
    }

    /// <summary>Works out this frame's spotlights, the house darkness, and where to draw each light's colour.</summary>
    private void UpdateSpotlights()
    {
        var now = Environment.TickCount64;
        var lights = WorldState.StageLights;

        lights.Evaluate(now, StageLightAnimator.EntityTileOf, SpotlightFrames);
        HouseDarknessNow = lights.CurrentHouseDarkness(now);
        DarknessRenderer.SetHouseDarkness(lights.HasSetup ? HouseDarknessNow : null);

        SpotlightDrawCount = 0;

        if (MapFile is null || (SpotlightFrames.Count == 0))
            return;

        if (SpotlightDraws.Length < SpotlightFrames.Count)
            SpotlightDraws = new SpotlightDraw[SpotlightFrames.Count * 2];

        foreach (var frame in SpotlightFrames)
            SpotlightDraws[SpotlightDrawCount++] = new SpotlightDraw(
                LightingSystem.FloorToScreen(frame.Tile, MapFile.Height, Camera),
                frame.Color,
                frame.Strength,
                SpotlightMasks.RadiusTiles(frame.Size),
                frame.Beam);
    }

    /// <summary>Spotlight colour, additive, in screen space right after the darkness layer.</summary>
    private void DrawSpotlights(SpriteBatch spriteBatch)
    {
        if (SpotlightDrawCount == 0)
            return;

        spriteBatch.Begin(blendState: BlendState.Additive, samplerState: GlobalSettings.Sampler, rasterizerState: ScissorRasterizerState);
        SpotlightRenderer.Draw(spriteBatch, WorldHud.ViewportBounds, SpotlightDraws.AsSpan(0, SpotlightDrawCount), HouseDarknessNow);
        spriteBatch.End();
    }
}
