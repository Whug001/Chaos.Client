#region
using System.Globalization;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Client.ViewModel;

/// <summary>
///     Authoritative dye shop state: the items, colours and prices the server sent on Open, the player's selection,
///     and whether a reply is pending. Pure -- no textures, no packets -- so the rules are unit-tested and the panel
///     only reads from it.
/// </summary>
public sealed class DyeShop
{
    public const int PAGE_SIZE = 12;

    public bool IsOpen { get; private set; }
    public Gender Gender { get; private set; }
    public ushort HairStyle { get; private set; }
    public DisplayColor HairColor { get; private set; }
    public int Gold { get; private set; }
    public int DyePrice { get; private set; }
    public int OmbreDyePrice { get; private set; }
    public IReadOnlyList<DisplayColor> Colors { get; private set; } = [];
    public IReadOnlyList<DisplayColor> OmbreColors { get; private set; } = [];
    public IReadOnlyList<DyeShopItem> Items { get; private set; } = [];
    public DyeShopItem? SelectedItem { get; private set; }
    public DisplayColor? SelectedColor { get; private set; }
    public DisplayColor? HoveredColor { get; set; }
    public int Page { get; private set; }
    public DyeShopRejectReason? LastRejectReason { get; private set; }
    public bool AwaitingReply { get; private set; }

    public int PageCount => Math.Max(1, (Items.Count + PAGE_SIZE - 1) / PAGE_SIZE);

    public IReadOnlyList<DyeShopItem> VisibleItems => Items.Skip(Page * PAGE_SIZE).Take(PAGE_SIZE).ToList();

    /// <summary>The colour the preview shows: hovered, else selected, else the selected item's own. Null with no item.</summary>
    public DisplayColor? PreviewColor
        => SelectedItem is null ? null : HoveredColor ?? SelectedColor ?? SelectedItem.Color;

    public bool CanDye => DisabledReason is null;

    /// <summary>Why the Dye button is off, or null when <see cref="CanDye" />.</summary>
    public string? DisabledReason
    {
        get
        {
            if (SelectedItem is null)
                return "Pick an item to dye.";

            if (SelectedColor is not { } color)
                return "Pick a colour.";

            if (color == SelectedItem.Color)
                return "It is already that colour.";

            var cost = CostOf(color);

            if (Gold < cost)
                return $"You need {cost.ToString("N0", CultureInfo.InvariantCulture)} gold.";

            return AwaitingReply ? "Dyeing..." : null;
        }
    }

    /// <summary>What dyeing with <paramref name="color" /> costs.</summary>
    public int CostOf(DisplayColor color) => OmbreDyes.IsOmbre(color) ? OmbreDyePrice : DyePrice;

    public void ApplyOpen(DyeShopDisplayArgs args)
    {
        Gender = args.Gender;
        HairStyle = args.HairStyle;
        HairColor = args.HairColor;
        Gold = args.Gold;
        DyePrice = args.DyePrice;
        OmbreDyePrice = args.OmbreDyePrice;
        Colors = args.Colors;
        OmbreColors = args.OmbreColors;
        Items = args.Items;

        var keptId = SelectedItem?.UniqueId;
        SelectedItem = keptId is null ? null : Items.FirstOrDefault(i => i.UniqueId == keptId);
        SelectedColor = null;
        HoveredColor = null;
        LastRejectReason = null;
        AwaitingReply = false;
        Page = Math.Clamp(Page, 0, PageCount - 1);
        IsOpen = true;
    }

    public void ApplyRejected(DyeShopRejectReason reason)
    {
        LastRejectReason = reason;
        AwaitingReply = false;
    }

    public void SelectItem(DyeShopItem item)
    {
        SelectedItem = item;
        SelectedColor = null;
        HoveredColor = null;
        LastRejectReason = null;
    }

    public void SelectColor(DisplayColor color)
    {
        SelectedColor = color;
        LastRejectReason = null;
    }

    public void StepPage(int delta) => Page = Math.Clamp(Page + delta, 0, PageCount - 1);

    public void MarkSent() => AwaitingReply = true;

    public void Clear()
    {
        IsOpen = false;
        Gender = Gender.Male;
        HairStyle = 0;
        HairColor = DisplayColor.Default;
        Gold = DyePrice = OmbreDyePrice = 0;
        Colors = [];
        OmbreColors = [];
        Items = [];
        SelectedItem = null;
        SelectedColor = null;
        HoveredColor = null;
        Page = 0;
        LastRejectReason = null;
        AwaitingReply = false;
    }
}
