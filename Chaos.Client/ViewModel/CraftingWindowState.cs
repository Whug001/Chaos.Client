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

    /// <summary>The rank word alone ("Novice" of "Novice Alchemist"), short enough for the rank cell.</summary>
    public string RankShortTitle => RankWord(RankTitle);

    //the bar holds about 28 characters, so the counts come first and only the next rank's word follows
    public string RankBarText
        => string.IsNullOrEmpty(NextRankTitle) ? RankTitle : $"{RankCount} / {RankThreshold} to {RankWord(NextRankTitle)}";

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

                break;
            case CraftingDisplayType.Progress:
                ProgressText = args.Finished ? args.Message : $"Crafting {args.Done} / {args.Total}";

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

    public bool SetCatalyst(byte slot, string? itemName, uint count)
    {
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
        CatalystSlot = CraftingProtocol.NO_CATALYST;
        CatalystIndex = -1;
        CatalystCount = 0;
        Quantity = QuantityMax >= 1 ? Math.Clamp(Math.Max(Quantity, 1), 1, QuantityMax) : 0;
        Changed?.Invoke();
    }

    public CraftingActionArgs? CraftAction()
    {
        if (Running || (SelectedId is not { } id) || (Quantity < 1))
            return null;

        Running = true;
        ProgressText = $"Crafting 0 / {Quantity}";
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

        //an upgrade can move the item to another recipe; the old one is gone from the list
        if ((SelectedId is { } id) && Recipes.All(r => r.Id != id))
        {
            SelectedId = null;
            Detail = null;
            Quantity = 0;
        }
    }

    private static string RankWord(string title)
    {
        var space = title.IndexOf(' ');

        return space > 0 ? title[..space] : title;
    }
}
