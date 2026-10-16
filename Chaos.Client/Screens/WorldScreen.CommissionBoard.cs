#region
using Chaos.Client.Collections;
using Chaos.Client.Controls.World.Popups.CommissionBoard;
using Chaos.Networking.Entities.Client;
#endregion

namespace Chaos.Client.Screens;

/// <summary>The commission board window: built on first use, opened by CommissionBoardDisplay, fed with review pictures from the portrait transfers.</summary>
public sealed partial class WorldScreen
{
    //built on first use, then shown again
    private CommissionBoardControl? CommissionBoardWindow;

    private void WireCommissionBoard()
    {
        Game.Connection.OnCommissionBoardDisplay += HandleCommissionBoardDisplay;
        WorldState.Inventory.SlotChanged += OnCommissionBoardInventoryChanged;
        WorldState.Inventory.Cleared += OnCommissionBoardInventoryCleared;
    }

    private void UnwireCommissionBoard()
    {
        Game.Connection.OnCommissionBoardDisplay -= HandleCommissionBoardDisplay;
        WorldState.Inventory.SlotChanged -= OnCommissionBoardInventoryChanged;
        WorldState.Inventory.Cleared -= OnCommissionBoardInventoryCleared;

        if (CommissionBoardWindow is not null)
        {
            CommissionBoardWindow.ActionRequested -= SendCommissionBoardAction;

            if (PortraitPictures is not null)
            {
                PortraitPictures.PictureReady -= CommissionBoardWindow.OnReviewPictureReady;
                PortraitPictures.PictureFailed -= CommissionBoardWindow.OnReviewPictureFailed;
            }
        }
    }

    private void SendCommissionBoardAction(CommissionBoardActionArgs args) => Game.Connection.SendCommissionBoardAction(args);

    private CommissionBoardControl BuildCommissionBoardWindow()
    {
        var transfers = PortraitTransfers();
        var window = new CommissionBoardControl(transfers)
        {
            ZIndex = 2
        };

        window.ActionRequested += SendCommissionBoardAction;
        transfers.PictureReady += window.OnReviewPictureReady;
        transfers.PictureFailed += window.OnReviewPictureFailed;
        Root!.AddChild(window);

        return window;
    }

    private void OnCommissionBoardInventoryChanged(byte _) => CommissionBoardWindow?.RefreshDeliverPicker();

    private void OnCommissionBoardInventoryCleared() => CommissionBoardWindow?.RefreshDeliverPicker();
}
