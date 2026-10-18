#region
using Chaos.Client.Collections;
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.World.Popups.Crafting;
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Screens;

/// <summary>The crafting windows: the recipe browser and the confirm window.</summary>
public sealed partial class WorldScreen
{
    //built on first use
    private CraftingBrowserControl? CraftingBrowser;
    private CraftingConfirmControl? CraftingConfirm;
    private Point CraftingBrowserOrigin;
    private Point CraftingConfirmOrigin;

    private void WireCrafting()
    {
        Game.Connection.OnCraftingDisplay += HandleCraftingDisplay;
        WorldState.Inventory.SlotChanged += OnCraftingInventorySlotChanged;
        WorldState.Inventory.Cleared += OnCraftingInventoryCleared;
    }

    private void UnwireCrafting()
    {
        Game.Connection.OnCraftingDisplay -= HandleCraftingDisplay;
        WorldState.Inventory.SlotChanged -= OnCraftingInventorySlotChanged;
        WorldState.Inventory.Cleared -= OnCraftingInventoryCleared;

        if (CraftingBrowser is not null)
        {
            CraftingBrowser.ActionRequested -= SendCraftingAction;
            CraftingBrowser.ConfirmRequested -= OpenCraftingConfirm;
        }

        if (CraftingConfirm is not null)
            CraftingConfirm.ActionRequested -= SendCraftingAction;
    }

    private bool EnsureCraftingWindows()
    {
        if ((CraftingBrowser is not null) && (CraftingConfirm is not null))
            return true;

        var browser = CraftingBrowserControl.TryCreate();
        var confirm = CraftingConfirmControl.TryCreate();

        if ((browser is null) || (confirm is null))
        {
            browser?.Dispose();
            confirm?.Dispose();

            return false;
        }

        CraftingBrowser = browser;
        CraftingConfirm = confirm;
        CraftingBrowserOrigin = new Point(browser.X, browser.Y);
        CraftingConfirmOrigin = new Point(confirm.X, confirm.Y);
        browser.ZIndex = 2;
        confirm.ZIndex = 3;
        browser.ActionRequested += SendCraftingAction;
        browser.ConfirmRequested += OpenCraftingConfirm;
        confirm.ActionRequested += SendCraftingAction;
        Root!.AddChild(browser);
        Root.AddChild(confirm);
        InventoryDropTargets.Add((confirm, confirm.DropCatalyst));

        return true;
    }

    private void PlaceAboveOrangeBar(PrefabPanel window, Point origin)
    {
        var orangeBar = WorldHud.OrangeBarBounds;

        if (orangeBar == Rectangle.Empty)
            return;

        window.Y = Math.Max(0, Math.Min(origin.Y, orangeBar.Top - window.Height));
    }

    private void HandleCraftingDisplay(CraftingDisplayArgs args)
    {
        //the world screen has not built its controls yet
        if (Root is null)
            return;

        if ((args.Type == CraftingDisplayType.Open) && !EnsureCraftingWindows())
        {
            WorldState.Chat.AddOrangeBarMessage("Update your game files to craft here.");
            SendCraftingAction(CraftingWindowState.CloseAction());

            return;
        }

        if ((CraftingBrowser is null) || (CraftingConfirm is null))
            return;

        WorldState.Crafting.Apply(args);

        switch (args.Type)
        {
            case CraftingDisplayType.Open:
                CraftingConfirm.Hide();
                PlaceAboveOrangeBar(CraftingBrowser, CraftingBrowserOrigin);
                CraftingBrowser.Open();

                break;
            case CraftingDisplayType.List:
                //the selected recipe left the list, so there is nothing left to confirm
                if (WorldState.Crafting.SelectedId is null)
                    CraftingConfirm.Hide();

                CraftingBrowser.ReloadList();

                break;
            case CraftingDisplayType.Close:
                CraftingConfirm.Hide();
                CraftingBrowser.CloseFromServer();

                if (!string.IsNullOrEmpty(args.Message))
                    WorldState.Chat.AddOrangeBarMessage(args.Message);

                break;
            default:
                CraftingBrowser.Refresh();
                CraftingConfirm.Refresh();

                if ((args.Type == CraftingDisplayType.Detail) && CraftingBrowser.Visible && WorldState.Crafting.TakePendingConfirm())
                {
                    PlaceAboveOrangeBar(CraftingConfirm, CraftingConfirmOrigin);
                    CraftingConfirm.Open();
                }

                break;
        }
    }

    private void OpenCraftingConfirm()
    {
        //a double-click on a new recipe arrives before the server's detail; the confirm then opens when it does
        if ((CraftingConfirm is null) || !WorldState.Crafting.RequestConfirm())
            return;

        PlaceAboveOrangeBar(CraftingConfirm, CraftingConfirmOrigin);
        CraftingConfirm.Open();
    }

    private void SendCraftingAction(CraftingActionArgs args)
    {
        //closing the browser takes the confirm window with it
        if (args.Type == CraftingActionType.Close)
        {
            CraftingConfirm?.Hide();
            WorldState.Crafting.CancelPendingConfirm();
        }

        Game.Connection.SendCraftingAction(args);
    }

    private void OnCraftingInventorySlotChanged(byte slot)
    {
        var state = WorldState.Crafting;

        if ((CraftingConfirm is null) || (state.CatalystIndex < 0) || (slot != state.CatalystSlot))
            return;

        ref readonly var item = ref WorldState.Inventory.GetSlot(slot);

        if (!item.IsOccupied || !state.SetCatalystFromSlot(slot, in item))
            state.ClearCatalyst();

        CraftingConfirm.Refresh();
    }

    private void OnCraftingInventoryCleared()
    {
        if ((CraftingConfirm is null) || (WorldState.Crafting.CatalystIndex < 0))
            return;

        WorldState.Crafting.ClearCatalyst();
        CraftingConfirm.Refresh();
    }

    private void HideCraftingWindows()
    {
        CraftingConfirm?.Hide();
        CraftingBrowser?.CloseFromServer();
        WorldState.Crafting.Clear();
    }
}
