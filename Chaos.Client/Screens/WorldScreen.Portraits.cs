#region
using Chaos.Client.Controls.World.Popups.Portraits;
using Chaos.Client.Systems.Portraits;
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Client.Screens;

/// <summary>Player portraits: the portrait window, opened from the own status-book portrait.</summary>
public sealed partial class WorldScreen
{
    //built on first use, then shown again
    private PortraitWindowControl? PortraitWindow;

    //fetched painted portrait pictures for inventory and exchange tooltips; built on first tooltip use
    private PortraitPictureTransfers? PortraitPictures;

    //built on first use, then shown again
    private PaintedPortraitWindowControl? PaintedPortraitWindow;

    //the portrait hash the tooltip currently shows, if any; its texture is held until the tooltip hides
    private string? TooltipPortraitHash;

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
        window.MakeRequested += png => Game.Connection.SendPaintedPortraitMake(png);
        window.RemoveRequested += HandlePortraitRemove;
        Root!.AddChild(window);

        return window;
    }

    private PortraitPictureTransfers PortraitTransfers()
    {
        if (PortraitPictures is null)
        {
            PortraitPictures = new PortraitPictureTransfers(h => Game.Connection.SendPortraitFetch(h));
            PortraitPictures.PictureReady += HandlePortraitPictureReady;
            PortraitPictures.PictureFailed += HandlePortraitPictureFailed;
        }

        return PortraitPictures;
    }

    private void ShowPortraitTooltip(
        string name,
        int currentDurability,
        int maxDurability,
        string hash,
        string? painter)
    {
        HidePortraitTooltip();

        var transfers = PortraitTransfers();
        transfers.TryGetTexture(hash, out var texture);
        transfers.Hold(hash);
        TooltipPortraitHash = hash;
        ItemTooltip.Show(name, currentDurability, maxDurability, InputBuffer.MouseX, InputBuffer.MouseY, hash, painter, texture);
    }

    private void HidePortraitTooltip()
    {
        if (TooltipPortraitHash is { } hash)
        {
            TooltipPortraitHash = null;
            PortraitPictures?.Release(hash);
        }

        ItemTooltip.Hide();
    }

    private void HandleExchangePortraitHovered(string name, string hash, string? painter)
        => ShowPortraitTooltip(name, 0, 0, hash, painter);

    private void HandleExchangePortraitUnhovered() => HidePortraitTooltip();

    private void OpenPaintedPortraitWindow(PaintedPortraitDisplayArgs args)
    {
        if (Root is null)
            return;

        PaintedPortraitWindow ??= BuildPaintedPortraitWindow();

        BringToFront(PaintedPortraitWindow);
        PaintedPortraitWindow.Open(args.UniqueId, args.Painter, args.Picture);
    }

    private PaintedPortraitWindowControl BuildPaintedPortraitWindow()
    {
        var window = new PaintedPortraitWindowControl
        {
            ZIndex = 2
        };

        window.SetRequested += id => Game.Connection.SendPaintedPortraitSet(id);
        Root!.AddChild(window);

        return window;
    }

    private void HandlePortraitRemove() => Game.Connection.SendPortraitSet([]);
}
