#region
using Chaos.Client.Collections;
using Chaos.Client.Controls.World.Popups.Tcg;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Client.Screens;

public sealed partial class WorldScreen
{
    private TcgChallengeWindow? TcgChallengeWindow;
    private TcgOfferWindow? TcgOfferWindow;

    private void EnsureTcgChallengeWindows()
    {
        if (TcgChallengeWindow is not null)
            return;

        TcgChallengeWindow = new TcgChallengeWindow(Root!.Width, Root.Height) { ZIndex = 50 };
        TcgOfferWindow = new TcgOfferWindow(Root.Width, Root.Height) { ZIndex = 50 };
        TcgChallengeWindow.Action += args => Game.Connection.SendTcgAction(args);
        TcgOfferWindow.Action += args => Game.Connection.SendTcgAction(args);
        Root.AddChild(TcgChallengeWindow);
        Root.AddChild(TcgOfferWindow);
    }

    private void HideTcgChallengeWindows()
    {
        TcgChallengeWindow?.Hide();
        TcgOfferWindow?.Hide();
    }

    private void OnTcgDisplay(TcgDisplayArgs args)
    {
        switch (args.Type)
        {
            case TcgDisplayType.ChallengeSetup:
                EnsureTcgChallengeWindows();
                TcgChallengeWindow!.ShowSetup(args.OtherName, args.DeckNames);

                break;

            case TcgDisplayType.ChallengeOffer when args.Incoming:
                EnsureTcgChallengeWindows();
                TcgOfferWindow!.ShowOffer(args.OtherName, args.Stakes, args.StakeGold, args.ExpiresMs, args.DeckNames);

                break;

            case TcgDisplayType.ChallengeOffer:
                EnsureTcgChallengeWindows();
                TcgChallengeWindow!.ShowWaiting(args.Stakes, args.StakeGold, args.ExpiresMs);

                break;

            case TcgDisplayType.ChallengeClosed:
                HideTcgChallengeWindows();
                WorldState.Chat.AddOrangeBarMessage(args.Reason);

                break;

            case TcgDisplayType.Update:
                HideTcgChallengeWindows();
                ShowTcgMatch(args);

                break;

            case TcgDisplayType.Close:
                EndTcgMatch(args);

                if (!string.IsNullOrEmpty(args.Reason))
                    WorldState.Chat.AddOrangeBarMessage(args.Reason);

                break;
        }
    }
}
