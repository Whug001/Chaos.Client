using Chaos.Client.Collections;
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Controls.Generic;
using Chaos.Client.Controls.Scrolling;
using Chaos.Client.Data;
using Chaos.Client.Definitions;
using Chaos.Client.Rendering;
using Chaos.Client.ViewModel;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;

namespace Chaos.Client.Controls.World.Popups.Crafting;

/// <summary>The confirm window (Korean "lmanu" panel): quantity, catalyst and the scaled ingredients.</summary>
public sealed class CraftingConfirmControl : PrefabPanel, IInventoryDropTarget
{
    public const string PREFAB = "lmanu";

    //a batch is sent as one byte, so three digits always fit
    private const int QUANTITY_MAX_DIGITS = 3;
    private const int QUANTITY_BOX_GAP = 4;
    private const int QUANTITY_BOX_HEIGHT = 22;

    private readonly UIImage Tile;
    private readonly ClickableImage CatalystImage;
    private readonly UILabel NameLabel;
    private readonly UILabel DescLabel;
    private readonly UILabel NumLabel;
    private readonly CustomTextBox QuantityBox;
    private readonly VirtualizedRowList<CraftingIngredientInfo> IngredientList;
    private readonly Rectangle CatalystRect;
    private ushort? IngredientsRecipeId;

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

        //"255/255" is 42 px and NUM is 43 px wide, so the default 1 px side padding would cut it to "..."
        NumLabel.PaddingLeft = 0;
        NumLabel.PaddingRight = 0;

        var numRect = GetRect("NUM");

        QuantityBox = new CustomTextBox
        {
            X = numRect.X,
            Y = numRect.Bottom + QUANTITY_BOX_GAP,
            Width = numRect.Width,
            Height = QUANTITY_BOX_HEIGHT,
            MaxLength = QUANTITY_MAX_DIGITS,
            DigitsOnly = true,
            HintText = "Qty"
        };
        QuantityBox.LostFocus += _ => CommitQuantity();
        AddChild(QuantityBox);

        //the art's scroll column sits inside the ingredient box, so the scroll bar covers it and the rows stop short of it
        var ingredientRect = GetRect("INGREDIENT");
        var rowWidth = ingredientRect.Width - ScrollBarControl.DEFAULT_WIDTH - 4;

        IngredientList = new VirtualizedRowList<CraftingIngredientInfo>(
            rowWidth,
            ingredientRect.Height,
            CraftingIngredientRow.HEIGHT,
            () => new CraftingIngredientRow(rowWidth),
            (row, ingredient, _) => ((CraftingIngredientRow)row).Bind(ingredient));

        AddChild(
            new ScrollViewerControl(IngredientList)
            {
                X = ingredientRect.X,
                Y = ingredientRect.Y,
                Width = ingredientRect.Width,
                Height = ingredientRect.Height
            });

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
        NumLabel.Text = $"{State.Quantity}/{State.QuantityMax}";

        if (!QuantityBox.IsFocused)
            QuantityBox.Text = State.Quantity.ToString();

        var ingredients = State.ScaledIngredients.ToList();

        if (State.CatalystLine is not { } catalystLine)
            CatalystImage.Texture = null;
        else
        {
            ref readonly var catalyst = ref WorldState.Inventory.GetSlot(State.CatalystSlot);
            CatalystImage.Texture = ui.GetItemIcon(catalyst.Sprite, catalyst.Color);
            ingredients.Add(catalystLine with { Sprite = catalyst.Sprite, Color = catalyst.Color });
        }

        //quantity steps and batch progress resend the same recipe; only a new recipe starts the list at the top
        if (detail.RecipeId == IngredientsRecipeId)
            IngredientList.ReplaceItems(ingredients);
        else
            IngredientList.SetItems(ingredients);

        IngredientsRecipeId = detail.RecipeId;
    }

    public override void OnKeyDown(KeyDownEvent e)
    {
        if (e.Keycode == Keycode.Escape)
        {
            Hide();
            e.Handled = true;

            return;
        }

        //a single-line box lets Enter bubble up; leaving the box applies the typed number
        if ((e.Keycode == Keycode.Enter) && QuantityBox.IsFocused)
        {
            ReleaseQuantityBox();
            e.Handled = true;

            return;
        }

        base.OnKeyDown(e);
    }

    public override void Hide()
    {
        ReleaseQuantityBox();
        base.Hide();
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

    private void CommitQuantity()
    {
        if (int.TryParse(QuantityBox.Text, out var typed))
            State.SetQuantity(typed);

        Refresh();
    }

    //the dispatcher keeps its own pointer to the focused box, so blurring the box alone would leave keys going to it
    private void ReleaseQuantityBox()
    {
        if (QuantityBox.IsFocused)
            QuantityBox.IsFocused = false;

        var dispatcher = InputDispatcher.Instance;

        if (dispatcher?.ExplicitFocus == QuantityBox)
            dispatcher.ClearExplicitFocus();
    }

    private void Confirm()
    {
        ReleaseQuantityBox();

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
