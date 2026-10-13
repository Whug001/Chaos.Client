#region
using Chaos.Client.Controls.World.Popups.ArenaHost;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Client.Screens;

/// <summary>The Arena Host Panel.</summary>
public sealed partial class WorldScreen
{
    //built on first use
    private ArenaHostPanelControl? ArenaHostPanel;

    private void WireArenaHost() => Game.Connection.OnArenaHostDisplay += HandleArenaHostDisplay;

    private void UnwireArenaHost()
    {
        Game.Connection.OnArenaHostDisplay -= HandleArenaHostDisplay;

        if (ArenaHostPanel is not null)
            ArenaHostPanel.ActionRequested -= SendArenaHostAction;
    }

    private void HandleArenaHostDisplay(ArenaHostDisplayArgs args)
    {
        //the world screen has not built its controls yet
        if (Root is null)
            return;

        if (ArenaHostPanel is null)
        {
            if (args.Type != ArenaHostDisplayType.Open)
                return;

            ArenaHostPanel = new ArenaHostPanelControl
            {
                ZIndex = 2
            };

            ArenaHostPanel.ActionRequested += SendArenaHostAction;
            Root.AddChild(ArenaHostPanel);
        }

        switch (args.Type)
        {
            case ArenaHostDisplayType.Open:
                ArenaHostPanel.Open(args);

                break;
            case ArenaHostDisplayType.Close:
                ArenaHostPanel.CloseFromServer();

                break;
            default:
                ArenaHostPanel.Refresh(args);

                break;
        }
    }

    private void SendArenaHostAction(ArenaHostActionArgs args) => Game.Connection.SendArenaHostAction(args);
}
