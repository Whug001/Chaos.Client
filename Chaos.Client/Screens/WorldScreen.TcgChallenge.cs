#region
using Chaos.Client.Collections;
using Chaos.Client.Controls.World.Popups.Tcg;
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Client.Screens;

public sealed partial class WorldScreen
{
    private TcgChallengeWindow? TcgChallengeWindow;
    private TcgOfferWindow? TcgOfferWindow;

    //an incoming challenge that arrived while a match window was open: held until the match closes
    //so it never sits hidden on top of the input stack, then shown if it has not expired yet
    private (TcgDisplayArgs Args, DateTime ReceivedAt)? HeldTcgOffer;

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

    /// <summary>Shows a challenge offer held while a match was open, if it has not expired yet.</summary>
    private void ReleaseHeldTcgOffer()
    {
        if (HeldTcgOffer is not { } held)
            return;

        HeldTcgOffer = null;

        if (TcgChallengeState.SecondsLeft(held.Args.ExpiresMs, held.ReceivedAt, DateTime.UtcNow) <= 0)
            return;

        EnsureTcgChallengeWindows();
        TcgOfferWindow!.ShowOffer(held.Args.OtherName, held.Args.Stakes, held.Args.StakeGold, held.Args.ExpiresMs, held.Args.DeckNames);
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

                if (TcgMatchOpen)
                {
                    //a match is already showing: the offer waits until it closes rather than sitting
                    //hidden behind the match window while sitting on top of the input stack
                    HeldTcgOffer = (args, DateTime.UtcNow);
                } else
                    TcgOfferWindow!.ShowOffer(args.OtherName, args.Stakes, args.StakeGold, args.ExpiresMs, args.DeckNames);

                break;

            case TcgDisplayType.ChallengeOffer:
                EnsureTcgChallengeWindows();
                TcgChallengeWindow!.ShowWaiting(args.Stakes, args.StakeGold, args.ExpiresMs);

                break;

            case TcgDisplayType.ChallengeClosed:
                HeldTcgOffer = null;
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

            case TcgDisplayType.SpectatorChat:
                if (TcgMatchOpen)
                    TcgMatchWindow!.AcceptSpectatorLine(args.OtherName, args.Reason);

                break;

            case TcgDisplayType.BattleMarks:
                TcgBattleMarks.Set(args.BattleMarks);

                break;
        }
    }
}
