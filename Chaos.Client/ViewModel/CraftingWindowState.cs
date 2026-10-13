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

    public string RankBarText
        => string.IsNullOrEmpty(NextRankTitle) ? RankTitle : $"Next rank: {NextRankTitle} ({RankCount} / {RankThreshold})";

    public string IngredientText
    {
        get
        {
            if (Detail is not { } detail)
                return string.Empty;

            var quantity = Math.Max(Quantity, 1);
            var lines = detail.Ingredients.Select(i =>
            {
                var need = i.Need * quantity;

                return (i.Have >= need ? "{=u" : "{=b") + $"{i.Name} {i.Have} / {need}";
            });

            if (CatalystIndex >= 0)
                lines = lines.Append("{=u" + $"{CraftingProtocol.Catalysts[CatalystIndex].Name} {CatalystCount} / {quantity}");

            return string.Join("\n", lines);
        }
    }

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

        SelectedId = id;
        Changed?.Invoke();

        return new CraftingActionArgs { Type = CraftingActionType.Select, RecipeId = id };
    }

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
    }
}
