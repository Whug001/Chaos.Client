using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;

namespace Chaos.Client.ViewModel;

/// <summary>The crafting windows' state. Pure: the windows bind to it and send what it returns.</summary>
public sealed class CraftingWindowState
{
    private IReadOnlyList<CraftingRecipeInfo> Recipes = [];
    private string FilterText = string.Empty;
    private uint CatalystCount;

    public CraftingWindowMode Mode { get; private set; }
    public string CraftName { get; private set; } = string.Empty;
    public string RankTitle { get; private set; } = string.Empty;
    public string NextRankTitle { get; private set; } = string.Empty;
    public int RankCount { get; private set; }
    public int RankThreshold { get; private set; }

    public ushort? SelectedId { get; private set; }
    public CraftingDisplayArgs? Detail { get; private set; }

    /// <summary>The cooking ingredient row the catalyst square controls (-1 when none). Cooking only.</summary>
    public int ActiveSwapRow { get; private set; } = -1;
    public int Quantity { get; private set; }
    public byte CatalystSlot { get; private set; } = CraftingProtocol.NO_CATALYST;
    public int CatalystIndex { get; private set; } = -1;
    public bool Running { get; private set; }
    public string ProgressText { get; private set; } = string.Empty;

    public event Action? Changed;

    public string Filter
    {
        get => FilterText;
        set
        {
            FilterText = value ?? string.Empty;
            Changed?.Invoke();
        }
    }

    public IReadOnlyList<CraftingRecipeInfo> VisibleRecipes
        => FilterText.Trim() is { Length: > 0 } text
            ? Recipes.Where(r => r.Name.Contains(text, StringComparison.OrdinalIgnoreCase)).ToList()
            : Recipes;

    public CraftingRecipeInfo? SelectedRecipe => Recipes.FirstOrDefault(r => r.Id == SelectedId);

    public int QuantityMax
    {
        get
        {
            var max = (int)(Detail?.MaxQuantity ?? 0);

            if (CatalystIndex >= 0)
                max = (int)Math.Min(max, CatalystCount);

            return Mode == CraftingWindowMode.WeaponsmithingUpgrade ? Math.Min(max, 1) : max;
        }
    }

    public byte? CatalystRate
        => (CatalystIndex >= 0) && (Detail is { } d) && (CatalystIndex < d.CatalystRates.Count) ? d.CatalystRates[CatalystIndex] : null;

    public float RankBarPercent => RankThreshold <= 0 ? 1f : Math.Clamp((float)RankCount / RankThreshold, 0f, 1f);

    /// <summary>True while the stove's window is open. Cooking shows full rank titles and no catalyst.</summary>
    public bool IsCooking => Mode == CraftingWindowMode.Cooking;

    /// <summary>The rank word alone ("Novice" of "Novice Alchemist"), short enough for the rank cell.</summary>
    public string RankShortTitle => IsCooking ? RankTitle : RankWord(RankTitle);

    //the bar holds about 28 characters, so the counts come first and only the next rank's word follows.
    //cooking keeps the full next title ("to Sous Chef"), since the first word alone ("to Sous") says nothing
    public string RankBarText
        => string.IsNullOrEmpty(NextRankTitle)
            ? RankTitle
            : $"{RankCount} / {RankThreshold} to {(IsCooking ? NextRankTitle : RankWord(NextRankTitle))}";

    /// <summary>"-" while cooking (the server sends -1 and keeps no per-recipe count), else the count.</summary>
    public string TimesMadeText
        => Detail is not { } detail ? string.Empty : detail.TimesMade < 0 ? "-" : detail.TimesMade.ToString();

    /// <summary>The recipe whose confirm window opens once its detail arrives (a double-click beat the server's reply).</summary>
    public ushort? PendingConfirmId { get; private set; }

    /// <summary>The selected recipe's ingredients for the chosen quantity.</summary>
    public IReadOnlyList<CraftingIngredientInfo> ScaledIngredients
    {
        get
        {
            if (Detail is not { } detail)
                return [];

            var quantity = Math.Max(Quantity, 1);

            return detail.Ingredients.Select(i => i with { Need = i.Need * quantity }).ToList();
        }
    }

    /// <summary>The catalyst's line under the ingredients, without an icon (the window takes it from the bag slot).</summary>
    public CraftingIngredientInfo? CatalystLine
        => CatalystIndex < 0
            ? null
            : new CraftingIngredientInfo
            {
                Name = CraftingProtocol.Catalysts[CatalystIndex].Name,
                Have = (int)CatalystCount,
                Need = Math.Max(Quantity, 1)
            };

    public void Apply(CraftingDisplayArgs args)
    {
        switch (args.Type)
        {
            case CraftingDisplayType.Open:
                Clear();
                ApplyList(args);

                break;
            case CraftingDisplayType.List:
                ApplyList(args);

                break;
            case CraftingDisplayType.Detail:
                if (args.RecipeId != SelectedId)
                    return;

                Detail = args;
                Quantity = QuantityMax >= 1 ? Math.Clamp(Math.Max(Quantity, 1), 1, QuantityMax) : 0;
                RefreshActiveSwapRow();

                break;
            case CraftingDisplayType.Progress:
                ProgressText = args.Finished ? args.Message : $"{(IsCooking ? "Cooking" : "Crafting")} {args.Done} / {args.Total}";

                if (args.Finished)
                    Running = false;

                break;
            case CraftingDisplayType.Close:
                Clear();

                break;
        }

        Changed?.Invoke();
    }

    public CraftingActionArgs Select(ushort id)
    {
        if (SelectedId != id)
        {
            Detail = null;
            Quantity = 0;
            ActiveSwapRow = -1;
        }

        if (PendingConfirmId != id)
            PendingConfirmId = null;

        SelectedId = id;
        Changed?.Invoke();

        return new CraftingActionArgs { Type = CraftingActionType.Select, RecipeId = id };
    }

    /// <summary>
    ///     True when the selected recipe's detail is here and the confirm window can open now. Otherwise the selected
    ///     recipe is remembered and <see cref="TakePendingConfirm" /> says when its detail has arrived.
    /// </summary>
    public bool RequestConfirm()
    {
        if (SelectedId is not { } id)
            return false;

        if (Detail?.RecipeId == id)
        {
            PendingConfirmId = null;

            return true;
        }

        PendingConfirmId = id;

        return false;
    }

    /// <summary>True once, when the detail of the recipe waiting for its confirm window has arrived.</summary>
    public bool TakePendingConfirm()
    {
        if ((PendingConfirmId is not { } id) || (id != SelectedId) || (Detail?.RecipeId != id))
            return false;

        PendingConfirmId = null;

        return true;
    }

    public void CancelPendingConfirm() => PendingConfirmId = null;

    public void Step(int delta)
    {
        if (QuantityMax < 1)
            return;

        Quantity = Math.Clamp(Quantity + delta, 1, QuantityMax);
        Changed?.Invoke();
    }

    public void SetQuantity(int quantity)
    {
        if (QuantityMax < 1)
            return;

        Quantity = Math.Clamp(quantity, 1, QuantityMax);
        Changed?.Invoke();
    }

    public bool SetCatalyst(byte slot, string? itemName, uint count)
    {
        if (IsCooking)
            return false;

        var index = CraftingProtocol.CatalystIndexByName(itemName);

        if ((index < 0) || (count == 0))
            return false;

        CatalystSlot = slot;
        CatalystIndex = index;
        CatalystCount = count;
        Quantity = QuantityMax >= 1 ? Math.Clamp(Quantity, 1, QuantityMax) : 0;
        Changed?.Invoke();

        return true;
    }

    public bool SetCatalystFromSlot(byte slot, in Inventory.InventorySlotData data)
        => SetCatalyst(slot, Inventory.BaseName(in data), data.Count);

    public void ClearCatalyst()
    {
        //the stove takes no catalyst; clearing one there changes nothing and raises nothing
        if (IsCooking)
            return;

        CatalystSlot = CraftingProtocol.NO_CATALYST;
        CatalystIndex = -1;
        CatalystCount = 0;
        Quantity = QuantityMax >= 1 ? Math.Clamp(Math.Max(Quantity, 1), 1, QuantityMax) : 0;
        Changed?.Invoke();
    }

    /// <summary>
    ///     Moves one choosable ingredient row to its next (forward) or previous choice. Null while a batch
    ///     runs or with nothing selected; the confirm window sends it only when non-null.
    /// </summary>
    public CraftingActionArgs? SwapAction(byte row, bool forward)
    {
        if (Running || (SelectedId is not { } id))
            return null;

        return new CraftingActionArgs
        {
            Type = CraftingActionType.SwapIngredient,
            RecipeId = id,
            IngredientRow = row,
            Forward = forward
        };
    }

    /// <summary>
    ///     Makes a choosable ingredient row the active one the catalyst square controls. Ignored when not
    ///     cooking, with no detail, or for a row that is out of range or not choosable.
    /// </summary>
    public void SetActiveSwapRow(int row)
    {
        if (!IsCooking || (Detail is null))
            return;

        if ((row < 0) || (row >= Detail.Ingredients.Count) || !Detail.Ingredients[row].Choosable)
            return;

        if (ActiveSwapRow == row)
            return;

        ActiveSwapRow = row;
        Changed?.Invoke();
    }

    /// <summary>
    ///     The swap for the active row. Null with no active row or while a batch runs; the confirm window
    ///     sends it only when non-null.
    /// </summary>
    public CraftingActionArgs? SwapActiveAction(bool forward)
        => ActiveSwapRow < 0 ? null : SwapAction((byte)ActiveSwapRow, forward);

    public CraftingActionArgs? CraftAction()
    {
        if (Running || (SelectedId is not { } id) || (Quantity < 1))
            return null;

        Running = true;
        ProgressText = $"{(IsCooking ? "Cooking" : "Crafting")} 0 / {Quantity}";
        Changed?.Invoke();

        return new CraftingActionArgs
        {
            Type = CraftingActionType.Craft,
            RecipeId = id,
            Quantity = (byte)Quantity,
            CatalystSlot = CatalystSlot
        };
    }

    public static CraftingActionArgs CancelAction() => new() { Type = CraftingActionType.Cancel };

    public static CraftingActionArgs CloseAction() => new() { Type = CraftingActionType.Close };

    public void Clear()
    {
        Recipes = [];
        FilterText = string.Empty;
        SelectedId = null;
        Detail = null;
        Quantity = 0;
        ActiveSwapRow = -1;
        CatalystSlot = CraftingProtocol.NO_CATALYST;
        CatalystIndex = -1;
        CatalystCount = 0;
        Running = false;
        ProgressText = string.Empty;
        PendingConfirmId = null;
    }

    private void ApplyList(CraftingDisplayArgs args)
    {
        Mode = args.Mode;
        CraftName = args.CraftName;
        RankTitle = args.RankTitle;
        NextRankTitle = args.NextRankTitle;
        RankCount = args.RankCount;
        RankThreshold = args.RankThreshold;
        Recipes = args.Recipes;
        PendingConfirmId = null;

        //the stove takes no catalyst; drop any the player held from another craft
        if (Mode == CraftingWindowMode.Cooking)
        {
            CatalystSlot = CraftingProtocol.NO_CATALYST;
            CatalystIndex = -1;
            CatalystCount = 0;
        }

        //an upgrade can move the item to another recipe; the old one is gone from the list
        if ((SelectedId is { } id) && Recipes.All(r => r.Id != id))
        {
            SelectedId = null;
            Detail = null;
            Quantity = 0;
        }

        //only cooking has an active row; a list that drops the selection drops its detail too
        if ((Mode != CraftingWindowMode.Cooking) || (SelectedId is null))
            ActiveSwapRow = -1;
    }

    /// <summary>
    ///     Keeps the active row by index across a detail rebuild for the same recipe: a swap rebuilds the
    ///     detail, and the same index stays active while it is still choosable, otherwise the first
    ///     choosable row takes over (-1 when none is choosable or not cooking).
    /// </summary>
    private void RefreshActiveSwapRow()
    {
        if (!IsCooking || (Detail is null))
        {
            ActiveSwapRow = -1;

            return;
        }

        var ingredients = Detail.Ingredients;

        if ((ActiveSwapRow >= 0) && (ActiveSwapRow < ingredients.Count) && ingredients[ActiveSwapRow].Choosable)
            return;

        ActiveSwapRow = FirstChoosableRow(ingredients);
    }

    private static int FirstChoosableRow(IReadOnlyList<CraftingIngredientInfo> ingredients)
    {
        for (var i = 0; i < ingredients.Count; i++)
            if (ingredients[i].Choosable)
                return i;

        return -1;
    }

    private static string RankWord(string title)
    {
        var space = title.IndexOf(' ');

        return space > 0 ? title[..space] : title;
    }
}
