#region
using Chaos.Client.Collections;
using Chaos.Client.Controls.World.Popups.Mounts;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Client.Screens;

/// <summary>The mount window.</summary>
public sealed partial class WorldScreen
{
    //built on first use
    private MountWindowControl? MountWindow;

    private void WireMounts() => Game.Connection.OnMountWindowDisplay += HandleMountWindowDisplay;

    private void UnwireMounts()
    {
        Game.Connection.OnMountWindowDisplay -= HandleMountWindowDisplay;

        if (MountWindow is not null)
            MountWindow.Chosen -= SendMountWindowChoose;
    }

    private void HandleMountWindowDisplay(MountWindowDisplayArgs args)
    {
        //the world screen has not built its controls yet
        if (Root is null)
            return;

        if (MountWindow is null)
        {
            if (args.Type != MountWindowDisplayType.Open)
                return;

            MountWindow = new MountWindowControl(Game.AislingRenderer, Game.CreatureRenderer)
            {
                ZIndex = 2
            };

            MountWindow.Chosen += SendMountWindowChoose;
            Root.AddChild(MountWindow);
        }

        if (args.Type == MountWindowDisplayType.Open)
            MountWindow.Open(args, WorldState.GetPlayerEntity()?.Appearance);
        else
            MountWindow.CloseFromServer();
    }

    private void SendMountWindowChoose(byte mount, DisplayColor color) => Game.Connection.SendMountWindowChoose(mount, color);
}
