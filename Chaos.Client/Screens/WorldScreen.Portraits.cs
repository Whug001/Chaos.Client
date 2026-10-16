#region
using Chaos.Client.Controls.World.Popups.Portraits;
using Chaos.Client.Data;
#endregion

namespace Chaos.Client.Screens;

/// <summary>Player portraits: the portrait window, opened from the own status-book portrait.</summary>
public sealed partial class WorldScreen
{
    //built on first use, then shown again
    private PortraitWindowControl? PortraitWindow;

    private void OpenPortraitWindow()
    {
        if (Root is null)
            return;

        PortraitWindow ??= BuildPortraitWindow();

        //already open: just come to the front, keeping the drawing and its undo history
        if (PortraitWindow.Visible)
        {
            BringToFront(PortraitWindow);

            return;
        }

        BringToFront(PortraitWindow);
        PortraitWindow.Open(OwnPortraitPicture);
    }

    private PortraitWindowControl BuildPortraitWindow()
    {
        var window = new PortraitWindowControl(Game.Window.Handle)
        {
            ZIndex = 2
        };

        window.SetRequested += png => Game.Connection.SendPortraitSet(png);
        window.RemoveRequested += HandlePortraitRemove;
        Root!.AddChild(window);

        return window;
    }

    private void HandlePortraitRemove()
    {
        Game.Connection.SendPortraitSet([]);
        DeletePortraitFilesForRemove(Game.Connection.AislingName);
    }

    //Remove deletes the character-folder files directly: unlike the own-picture sync it makes no .old.jpg backup
    private static void DeletePortraitFilesForRemove(string name)
    {
        if (!DataContext.LocalPlayerSettings.IsInitialized || string.IsNullOrEmpty(name))
            return;

        try
        {
            var jpgPath = DataContext.LocalPlayerSettings.GetFilePath($"{name}.jpg");
            var noExtPath = DataContext.LocalPlayerSettings.GetFilePath(name);

            if (File.Exists(jpgPath))
                File.Delete(jpgPath);

            if (File.Exists(noExtPath))
                File.Delete(noExtPath);
        } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            //a locked portrait file must never crash the client
        }
    }
}
