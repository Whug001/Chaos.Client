using Chaos.Client.Controls.World.Popups.BrassRing;
using Chaos.DarkAges.BrassRing;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;

namespace Chaos.Client.Screens;

public sealed partial class WorldScreen
{
    //built on the first Open, so players who never box never load its art
    private BrassRingControl? BrassRingWindow;

    private void WireBrassRing() => Game.Connection.OnBrassRingDisplay += HandleBrassRingDisplay;

    private void UnwireBrassRing()
    {
        Game.Connection.OnBrassRingDisplay -= HandleBrassRingDisplay;

        if (BrassRingWindow is not null)
        {
            BrassRingWindow.MoveRequested -= SendBrassRingMove;
            BrassRingWindow.ForfeitRequested -= SendBrassRingForfeit;
            BrassRingWindow.CheerRequested -= SendBrassRingCheer;
            BrassRingWindow.StopWatchingRequested -= SendBrassRingStopWatching;
        }
    }

    private void HandleBrassRingDisplay(BrassRingDisplayArgs args)
    {
        //the world screen has not built its controls yet
        if (Root is null)
            return;

        if (BrassRingWindow is null)
        {
            if (args.Type != BrassRingDisplayType.Open)
                return;

            BrassRingWindow = new BrassRingControl(Game.AislingRenderer, Game.SoundSystem) { ZIndex = 2 };
            BrassRingWindow.MoveRequested += SendBrassRingMove;
            BrassRingWindow.ForfeitRequested += SendBrassRingForfeit;
            BrassRingWindow.CheerRequested += SendBrassRingCheer;
            BrassRingWindow.StopWatchingRequested += SendBrassRingStopWatching;
            Root.AddChild(BrassRingWindow);
        }

        switch (args.Type)
        {
            case BrassRingDisplayType.Open:
                WorldHud.ChatInput.Unfocus();
                BrassRingWindow.Open(args);

                break;
            case BrassRingDisplayType.Tick when BrassRingWindow.Visible:
                BrassRingWindow.ApplyTick(args);

                break;
            case BrassRingDisplayType.Close when BrassRingWindow.Visible:
                BrassRingWindow.CloseFromServer(args.Reason, args.Leaver);

                break;
        }
    }

    private void SendBrassRingMove(BrassRingMove move)
        => Game.Connection.SendBrassRingAction(new BrassRingActionArgs { Type = BrassRingActionType.Move, Move = move });

    private void SendBrassRingForfeit()
        => Game.Connection.SendBrassRingAction(new BrassRingActionArgs { Type = BrassRingActionType.Forfeit });

    private void SendBrassRingCheer(BrassRingCheer kind)
        => Game.Connection.SendBrassRingAction(new BrassRingActionArgs { Type = BrassRingActionType.Cheer, Cheer = kind });

    private void SendBrassRingStopWatching()
        => Game.Connection.SendBrassRingAction(new BrassRingActionArgs { Type = BrassRingActionType.StopWatching });
}
