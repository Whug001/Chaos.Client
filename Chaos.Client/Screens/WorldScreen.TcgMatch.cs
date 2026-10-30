#region
using Chaos.Client.Collections;
using Chaos.Client.Controls.Generic;
using Chaos.Client.Controls.World.Popups.Tcg.Match;
using Chaos.Client.Systems;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Client.Screens;

public sealed partial class WorldScreen
{
    private const string TCG_MATCH_ART_MISSING_TEXT = "Card game art is missing. Update the game files.";

    private readonly TcgBattleMarkTable TcgBattleMarks = new();
    private TcgMatchWindow? TcgMatchWindow;
    private bool TcgMatchArtWarned;
    private OkPopupMessageControl? TcgSpectatePrompt;
    private string? TcgSpectateTarget;
    private (int TileX, int TileY)? TcgSpectateTile;
    private short TcgSpectateMapId;

    private bool TcgMatchOpen => TcgMatchWindow?.Visible ?? false;

    private void AskSpectate(string name, int tileX, int tileY)
    {
        if (TcgSpectatePrompt is null)
        {
            TcgSpectatePrompt = new OkPopupMessageControl(yesNo: true) { Name = "TcgSpectatePrompt" };
            Root!.AddChild(TcgSpectatePrompt);
            TcgSpectatePrompt.OnOk += () =>
            {
                TcgSpectatePrompt.Hide();

                if (TcgSpectateTarget is { } target)
                    Game.Connection.SendTcgAction(new TcgActionArgs { Type = TcgActionType.Spectate, TargetName = target });
            };
            TcgSpectatePrompt.OnCancel += () =>
            {
                TcgSpectatePrompt.Hide();

                //the prompt is hidden on a map change, but a late No must still never click a tile on the new map
                if ((TcgSpectateTile is { } tile)
                    && (CurrentMapId == TcgSpectateMapId)
                    && IsTileOnMap(tile.TileX, tile.TileY))
                    Game.Connection.ClickTile(tile.TileX, tile.TileY);
            };
        }

        TcgSpectateTarget = name;
        TcgSpectateTile = (tileX, tileY);
        TcgSpectateMapId = CurrentMapId;
        TcgSpectatePrompt.Show("Watch this card battle?");
    }

    private void EnsureTcgMatchWindow()
    {
        if (TcgMatchWindow is not null)
            return;

        TcgMatchWindow = new TcgMatchWindow(Game.CreatureRenderer, WorldState.TcgCollection, Game.SoundSystem);
        Root!.AddChild(TcgMatchWindow);
        TcgMatchWindow.Action += args => Game.Connection.SendTcgAction(args);
        TcgMatchWindow.SpectateStopped += () => Game.Connection.SendTcgAction(new TcgActionArgs { Type = TcgActionType.StopSpectate });
        TcgMatchWindow.Closed += ReleaseHeldTcgOffer;
        TcgMatchWindow.WhisperSent += (target, text) =>
        {
            WorldState.Chat.RecordWhisperTo(target);
            Game.Connection.SendWhisper(target, text);
        };

        if (!TcgMatchArt.ArtPresent() && !TcgMatchArtWarned)
        {
            TcgMatchArtWarned = true;
            WorldState.Chat.AddOrangeBarMessage(TCG_MATCH_ART_MISSING_TEXT);
        }
    }

    private void ShowTcgMatch(TcgDisplayArgs args)
    {
        if (Root is null)
            return;

        EnsureTcgMatchWindow();
        TcgMatchWindow!.ShowMatch(args);
    }

    private void EndTcgMatch(TcgDisplayArgs args) => TcgMatchWindow?.EndMatch(args);
}
