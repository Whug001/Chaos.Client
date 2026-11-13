#region
using Chaos.Client.Collections;
using Chaos.Client.Controls.World.Popups.Tamer;
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Client.Screens;

/// <summary>
///     The stable window: built on first use, fed by TamerDisplay. A snapshot always follows a catalog on open,
///     so the window opens on the first snapshot after a catalog and otherwise only refreshes while visible.
/// </summary>
public sealed partial class WorldScreen
{
    private TamerStableControl? TamerStableWindow;

    private void WireTamer()
    {
        Game.Connection.OnTamerDisplay += HandleTamerDisplay;
    }

    private void UnwireTamer()
    {
        Game.Connection.OnTamerDisplay -= HandleTamerDisplay;

        if (TamerStableWindow is not null)
            TamerStableWindow.ActionRequested -= SendTamerAction;
    }

    private bool EnsureTamerWindow()
    {
        if (TamerStableWindow is not null)
            return true;

        var window = new TamerStableControl(Game.CreatureRenderer) { ZIndex = 2 };

        window.ActionRequested += SendTamerAction;

        Root!.AddChild(window);

        TamerStableWindow = window;

        return true;
    }

    private void HandleTamerDisplay(TamerDisplayArgs args)
    {
        if (Root is null)
            return;

        switch (args.Kind)
        {
            case TamerDisplayKind.Catalog:
                WorldState.TamerStable.ApplyCatalog(args);

                if (TamerStableWindow?.Visible ?? false)
                    TamerStableWindow.Refresh();

                break;
            case TamerDisplayKind.Snapshot:
                WorldState.TamerStable.ApplySnapshot(args);

                if (!EnsureTamerWindow())
                    return;

                if (TamerStableWindow!.Visible)
                    TamerStableWindow.Refresh();
                else
                    TamerStableWindow.Open();

                break;
            case TamerDisplayKind.Close:
                var wasOpen = TamerStableWindow?.Visible ?? false;

                TamerStableWindow?.CloseFromServer();

                if (wasOpen && !string.IsNullOrEmpty(args.CloseReason))
                    WorldState.Chat.AddOrangeBarMessage(args.CloseReason);

                break;
        }
    }

    private void SendTamerAction(TamerActionArgs args) => Game.Connection.SendTamerAction(args);
}
