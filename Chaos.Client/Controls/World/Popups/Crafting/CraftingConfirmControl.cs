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
using Microsoft.Xna.Framework.Graphics;

namespace Chaos.Client.Controls.World.Popups.Crafting;

/// <summary>The confirm window (Korean "lmanu" panel): quantity, catalyst and the scaled ingredients.</summary>
public sealed class CraftingConfirmControl : PrefabPanel, IInventoryDropTarget
{
    public const string PREFAB = "lmanu";

    //a batch is sent as one byte, so three digits always fit
    private const int QUANTITY_MAX_DIGITS = 3;
    private const int QUANTITY_BOX_GAP = 4;
    private const int QUANTITY_BOX_HEIGHT = 22;

    //gap between the catalyst square and its cooking-mode arrows (the lmanu ADDITIONAL rect is 32x32
    //at [21,170], free on both sides: nothing else lives in x 0..71, y 140..225, so the 9x14 PREV/NEXT
    //art flanks the square at [10,179] and [55,179] without touching any control or text)
    private const int SWAP_ARROW_GAP = 2;

    private readonly UIImage Tile;
    private readonly ClickableImage CatalystImage;
    private readonly UIButton SwapPrevButton;
    private readonly UIButton SwapNextButton;
    private readonly UILabel NameLabel;
    private readonly UILabel DescLabel;
    private readonly UILabel NumLabel;
    private readonly CustomTextBox QuantityBox;
    private readonly VirtualizedRowList<IndexedIngredient> IngredientList;
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
            //in cooking mode the square is the active row's control (left = next); otherwise it clears the catalyst
            if (State.IsCooking)
            {
                SwapActive(true);

                return;
            }

            State.ClearCatalyst();
            Refresh();
        };
        CatalystImage.RightClicked += () =>
        {
            //right = previous for the active row; the other crafts keep right-click doing nothing here
            if (State.IsCooking)
                SwapActive(false);
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

        IngredientList = new VirtualizedRowList<IndexedIngredient>(
            rowWidth,
            ingredientRect.Height,
            CraftingIngredientRow.HEIGHT,
            () =>
            {
                //rows are recycled, so each row's swap event is wired once here and reads its current index
                var row = new CraftingIngredientRow(rowWidth);
                row.SwapRequested += forward => SwapIngredient(row, forward);

                return row;
            },
            (row, wrapped, _) => ((CraftingIngredientRow)row).Bind(
                wrapped.Ingredient,
                wrapped.Index,
                wrapped.Index >= 0 && State.IsCooking && (wrapped.Index == State.ActiveSwapRow)));

        AddChild(
            new ScrollViewerControl(IngredientList)
            {
                X = ingredientRect.X,
                Y = ingredientRect.Y,
                Width = ingredientRect.Width,
                Height = ingredientRect.Height
            });

        var prevButton = CreateButton("PREV")!;
        prevButton.Clicked += () => Step(-1);
        var nextButton = CreateButton("NEXT")!;
        nextButton.Clicked += () => Step(+1);

        //cooking-mode arrows: manual buttons reusing the quantity stepper art, flanking the catalyst
        //square and vertically centred on it; hidden unless cooking (Refresh owns Visible)
        SwapPrevButton = new UIButton
        {
            Name = "SwapPrev",
            X = CatalystRect.X - SWAP_ARROW_GAP - prevButton.Width,
            Y = CatalystRect.Y + ((CatalystRect.Height - prevButton.Height) / 2),
            Width = prevButton.Width,
            Height = prevButton.Height,
            NormalTexture = prevButton.NormalTexture,
            PressedTexture = prevButton.PressedTexture,
            DisabledTexture = prevButton.DisabledTexture,
            SelectedTexture = prevButton.SelectedTexture,
            Visible = false
        };
        SwapPrevButton.Clicked += () => SwapActive(false);
        AddChild(SwapPrevButton);

        SwapNextButton = new UIButton
        {
            Name = "SwapNext",
            X = CatalystRect.Right + SWAP_ARROW_GAP,
            Y = CatalystRect.Y + ((CatalystRect.Height - nextButton.Height) / 2),
            Width = nextButton.Width,
            Height = nextButton.Height,
            NormalTexture = nextButton.NormalTexture,
            PressedTexture = nextButton.PressedTexture,
            DisabledTexture = nextButton.DisabledTexture,
            SelectedTexture = nextButton.SelectedTexture,
            Visible = false
        };
        SwapNextButton.Clicked += () => SwapActive(true);
        AddChild(SwapNextButton);

        CreateButton("OK")!.Clicked += Confirm;
        CreateButton("CANCEL")!.Clicked += Hide;
    }

    public static CraftingConfirmControl? TryCreate()
        => DataContext.UserControls.Get(PREFAB) is null ? null : new CraftingConfirmControl();

    public bool AcceptsInventoryDrop(byte slot, int screenX, int screenY)
        => !State.IsCooking
           && (slot != 0)
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
        //an empty window with no detail helps no one: the detail arrives first through TakePendingConfirm
        if (State.Detail is null)
            return;

        Show();
        Refresh();
    }

    public void Refresh()
    {
        if (!Visible)
            return;

        //the arrows only exist in cooking mode, and only while a row can be rotated
        var showSwap = State.IsCooking && (State.ActiveSwapRow >= 0);
        SwapPrevButton.Visible = showSwap;
        SwapNextButton.Visible = showSwap;

        if (State.Detail is not { } detail)
            return;

        var ui = UiRenderer.Instance!;
        Tile.Texture = ui.GetItemIcon(detail.Sprite, detail.Color);
        NameLabel.Text = detail.RecipeName;

        var success = $"Success: {detail.SuccessRate}%";

        if (State.CatalystRate is { } withCatalyst)
            success += $" ({withCatalyst}% with catalyst)";

        DescLabel.Text = $"{success}\nMade: {State.TimesMadeText}\nCrafting {State.Quantity} (one roll each)";
        NumLabel.Text = $"{State.Quantity}/{State.QuantityMax}";

        if (!QuantityBox.IsFocused)
            QuantityBox.Text = State.Quantity.ToString();

        var ingredients = State.ScaledIngredients.Select((ingredient, index) => new IndexedIngredient(index, ingredient)).ToList();

        //cooking never carries a catalyst (the state clears it and refuses new ones): the square shows
        //the active row's ingredient icon instead (empty with no choosable row); the catalyst line is not
        //an ingredient row and takes no index
        if (State.IsCooking)
            CatalystImage.Texture = ActiveIngredientIcon();
        else if (State.CatalystLine is not { } catalystLine)
            CatalystImage.Texture = null;
        else
        {
            ref readonly var catalyst = ref WorldState.Inventory.GetSlot(State.CatalystSlot);
            CatalystImage.Texture = ui.GetItemIcon(catalyst.Sprite, catalyst.Color);
            ingredients.Add(new IndexedIngredient(-1, catalystLine with { Sprite = catalyst.Sprite, Color = catalyst.Color }));
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
        //shared cached textures (the swap arrows reuse the PREV/NEXT button art)
        Tile.Texture = null;
        CatalystImage.Texture = null;
        SwapPrevButton.NormalTexture = null;
        SwapPrevButton.PressedTexture = null;
        SwapPrevButton.SelectedTexture = null;
        SwapNextButton.NormalTexture = null;
        SwapNextButton.PressedTexture = null;
        SwapNextButton.SelectedTexture = null;
        base.Dispose();
    }

    private void SwapIngredient(CraftingIngredientRow row, bool forward)
    {
        //a captured row can still click after Escape hid the window; a hidden window sends no swaps
        if (!Visible || (row.Index < 0) || (State.SwapAction((byte)row.Index, forward) is not { } action))
            return;

        //clicking a choosable row makes it the active row the square controls (cooking only; ignored elsewhere)
        if (State.IsCooking)
        {
            State.SetActiveSwapRow(row.Index);
            Refresh();
        }

        ActionRequested?.Invoke(action);
    }

    private void SwapActive(bool forward)
    {
        //a batch sends no swaps (SwapActiveAction is null while running); a hidden window sends nothing
        if (!Visible || (State.SwapActiveAction(forward) is not { } action))
            return;

        ActionRequested?.Invoke(action);
    }

    /// <summary>The active row's icon from the same source its row uses; null with no active row.</summary>
    private static Texture2D? ActiveIngredientIcon()
    {
        if ((UiRenderer.Instance is not { } ui) || (State.ActiveSwapRow < 0))
            return null;

        var ingredients = State.ScaledIngredients;

        if (State.ActiveSwapRow >= ingredients.Count)
            return null;

        var ingredient = ingredients[State.ActiveSwapRow];

        return ingredient.Sprite == 0 ? null : ui.GetItemIcon(ingredient.Sprite, ingredient.Color);
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

    /// <summary>An ingredient row with its position, so a swap click names the server's row.</summary>
    private sealed record IndexedIngredient(int Index, CraftingIngredientInfo Ingredient);

    private sealed class ClickableImage : UIImage
    {
        public event Action? Clicked;
        public event Action? RightClicked;

        public override void OnClick(ClickEvent e)
        {
            if (e.Button == MouseButton.Left)
            {
                Clicked?.Invoke();
                e.Handled = true;
            } else if (e.Button == MouseButton.Right)
            {
                RightClicked?.Invoke();
                e.Handled = true;
            }

            //panels absorb their clicks either way, so a square click never reaches the world behind
            base.OnClick(e);
        }
    }
}
