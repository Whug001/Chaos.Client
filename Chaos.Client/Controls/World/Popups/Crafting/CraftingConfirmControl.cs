using Chaos.Client.Collections;
using Chaos.Client.Controls.Components;
using Chaos.Client.Data;
using Chaos.Client.Definitions;
using Chaos.Client.Rendering;
using Chaos.Client.ViewModel;
using Chaos.Networking.Entities.Client;
using Microsoft.Xna.Framework;

namespace Chaos.Client.Controls.World.Popups.Crafting;

/// <summary>The confirm window (Korean "lmanu" panel): quantity, catalyst and the scaled ingredients.</summary>
public sealed class CraftingConfirmControl : PrefabPanel, IInventoryDropTarget
{
    public const string PREFAB = "lmanu";

    private readonly UIImage Tile;
    private readonly ClickableImage CatalystImage;
    private readonly UILabel NameLabel;
    private readonly UILabel DescLabel;
    private readonly UILabel NumLabel;
    private readonly UILabel IngredientLabel;
    private readonly Rectangle CatalystRect;

    private static CraftingWindowState State => WorldState.Crafting;

    public event Action<CraftingActionArgs>? ActionRequested;

    private CraftingConfirmControl()
        : base(PREFAB)
    {
        Name = "CraftingConfirm";
        Visible = false;
        UsesControlStack = true;

        var tileRect = GetRect("TILE");

        Tile = new UIImage
        {
            X = tileRect.X,
            Y = tileRect.Y,
            Width = tileRect.Width,
            Height = tileRect.Height,
            IsHitTestVisible = false
        };
        AddChild(Tile);

        CatalystRect = GetRect("ADDITIONAL");

        CatalystImage = new ClickableImage
        {
            X = CatalystRect.X,
            Y = CatalystRect.Y,
            Width = CatalystRect.Width,
            Height = CatalystRect.Height
        };

        CatalystImage.Clicked += () =>
        {
            State.ClearCatalyst();
            Refresh();
        };
        AddChild(CatalystImage);

        NameLabel = CreateLabel("NAME")!;
        DescLabel = CreateLabel("DESC")!;
        DescLabel.WordWrap = true;
        NumLabel = CreateLabel("NUM", HorizontalAlignment.Center)!;
        IngredientLabel = CreateLabel("INGREDIENT")!;
        IngredientLabel.WordWrap = true;

        CreateButton("PREV")!.Clicked += () => Step(-1);
        CreateButton("NEXT")!.Clicked += () => Step(+1);
        CreateButton("OK")!.Clicked += Confirm;
        CreateButton("CANCEL")!.Clicked += Hide;
    }

    public static CraftingConfirmControl? TryCreate()
        => DataContext.UserControls.Get(PREFAB) is null ? null : new CraftingConfirmControl();

    public bool AcceptsInventoryDrop(byte slot, int screenX, int screenY)
        => (slot != 0)
           && Visible
           && new Rectangle(
               ScreenX + CatalystRect.X,
               ScreenY + CatalystRect.Y,
               CatalystRect.Width,
               CatalystRect.Height).Contains(screenX, screenY);

    public void DropCatalyst(byte slot)
    {
        ref readonly var item = ref WorldState.Inventory.GetSlot(slot);

        if (!item.IsOccupied || !State.SetCatalystFromSlot(slot, in item))
            WorldState.Chat.AddOrangeBarMessage("Only a catalyst fits there.");

        Refresh();
    }

    public void Open()
    {
        Show();
        Refresh();
    }

    public void Refresh()
    {
        if (!Visible || (State.Detail is not { } detail))
            return;

        var ui = UiRenderer.Instance!;
        Tile.Texture = ui.GetItemIcon(detail.Sprite, detail.Color);
        NameLabel.Text = detail.RecipeName;

        var success = $"Success: {detail.SuccessRate}%";

        if (State.CatalystRate is { } withCatalyst)
            success += $" ({withCatalyst}% with catalyst)";

        DescLabel.Text = $"{success}\nMade: {detail.TimesMade}\nCrafting {State.Quantity} (one roll each)";
        NumLabel.Text = $"{State.Quantity} / {State.QuantityMax}";
        IngredientLabel.Text = State.IngredientText;

        if (State.CatalystIndex < 0)
            CatalystImage.Texture = null;
        else
        {
            ref readonly var catalyst = ref WorldState.Inventory.GetSlot(State.CatalystSlot);
            CatalystImage.Texture = ui.GetItemIcon(catalyst.Sprite, catalyst.Color);
        }
    }

    public override void OnKeyDown(KeyDownEvent e)
    {
        if (e.Keycode == Keycode.Escape)
        {
            Hide();
            e.Handled = true;

            return;
        }

        base.OnKeyDown(e);
    }

    public override void Dispose()
    {
        //shared cached textures
        Tile.Texture = null;
        CatalystImage.Texture = null;
        base.Dispose();
    }

    private void Step(int delta)
    {
        State.Step(delta);
        Refresh();
    }

    private void Confirm()
    {
        if (State.CraftAction() is not { } action)
            return;

        ActionRequested?.Invoke(action);
        Hide();
    }

    private sealed class ClickableImage : UIImage
    {
        public event Action? Clicked;

        public override void OnClick(ClickEvent e)
        {
            if (e.Button != MouseButton.Left)
                return;

            Clicked?.Invoke();
            e.Handled = true;
        }
    }
}
