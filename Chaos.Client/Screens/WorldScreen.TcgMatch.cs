#region
using Chaos.Client.Collections;
using Chaos.Client.Controls.World.Popups.Tcg.Match;
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Client.Screens;

public sealed partial class WorldScreen
{
    private const string TCG_MATCH_ART_MISSING_TEXT = "Card game art is missing. Update the game files.";

    private TcgMatchWindow? TcgMatchWindow;
    private bool TcgMatchArtWarned;

    private bool TcgMatchOpen => TcgMatchWindow?.Visible ?? false;

    private void EnsureTcgMatchWindow()
    {
        if (TcgMatchWindow is not null)
            return;

        TcgMatchWindow = new TcgMatchWindow(Game.CreatureRenderer, WorldState.TcgCollection);
        Root!.AddChild(TcgMatchWindow);
        TcgMatchWindow.Action += args => Game.Connection.SendTcgAction(args);
        TcgMatchWindow.WhisperSent += text => Game.Connection.SendWhisper(TcgMatchWindow.OpponentName, text);

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
