#region
using Chaos.Client.Collections;
using Chaos.Client.Controls.World.Popups.Tcg;
using Chaos.Client.ViewModel;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Client.Screens;

/// <summary>
///     The ranked queue bar: built once, fed by TcgRankedDisplay, hidden while a match is open. Challenge actions go
///     through the same send as the challenge windows; the binder's ranked sends live in <c>WorldScreen.Tcg.cs</c>.
/// </summary>
public sealed partial class WorldScreen
{
    private TcgQueueBar? TcgQueueBar;

    private void EnsureTcgQueueBar()
    {
        if ((TcgQueueBar is not null) || (Root is null))
            return;

        TcgQueueBar = new TcgQueueBar(Root.Width, () => TcgMatchOpen) { ZIndex = 60 };
        TcgQueueBar.LeaveClicked += OnTcgQueueLeave;
        TcgQueueBar.AcceptClicked += OnTcgQueueAccept;
        TcgQueueBar.DeclineClicked += OnTcgQueueDecline;
        Root.AddChild(TcgQueueBar);
    }

    private void HandleTcgRankedDisplay(TcgRankedDisplayArgs args)
    {
        //the world screen has not built its controls yet
        if (Root is null)
            return;

        EnsureTcgQueueBar();

        if (WorldState.TcgRanked.Apply(args, DateTime.UtcNow) is { } message)
            WorldState.Chat.AddOrangeBarMessage(message);

        if ((TcgBinderWindow is not null) && TcgBinderWindow.Visible)
            TcgBinderWindow.Refresh();
    }

    private void OnTcgQueueAccept()
    {
        SendTcgAction(TcgRankedState.AcceptAction());
        WorldState.TcgRanked.MarkAccepted();
    }

    private void OnTcgQueueDecline() => SendTcgAction(TcgRankedState.DeclineAction());

    private void OnTcgQueueLeave() => SendTcgAction(TcgRankedState.LeaveAction());

    private void SendTcgAction(TcgActionArgs args) => Game.Connection.SendTcgAction(args);

    private void UnwireTcgRanked()
    {
        Game.Connection.OnTcgRankedDisplay -= HandleTcgRankedDisplay;

        if (TcgQueueBar is not null)
        {
            TcgQueueBar.LeaveClicked -= OnTcgQueueLeave;
            TcgQueueBar.AcceptClicked -= OnTcgQueueAccept;
            TcgQueueBar.DeclineClicked -= OnTcgQueueDecline;
        }
    }

    private void WireTcgRanked()
    {
        EnsureTcgQueueBar();
        Game.Connection.OnTcgRankedDisplay += HandleTcgRankedDisplay;
    }
}
