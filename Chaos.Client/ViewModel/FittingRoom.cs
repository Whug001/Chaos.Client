#region
using Chaos.Client.Rendering;
using Chaos.Client.Systems;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Client.ViewModel;

public enum FittingRoomTab
{
    Overcoats,
    Hats,
    Accessories,
    Wearing
}

/// <summary>A cosmetic being tried on and the color chosen for it.</summary>
public sealed record FittingRoomWorn(FittingRoomLook Look, DisplayColor Color);

/// <summary>
///     Authoritative fitting room state: the catalog the server sent on Open, what is being tried on, and the
///     browsing state of the window. Pure -- no textures, no packets. Nothing here is bought or sent back.
/// </summary>
public sealed class FittingRoom
{
    public const int PAGE_SIZE = 30;

    private readonly List<FittingRoomWorn> _accessories = [];
    private AislingAppearance? _liveLook;
    private FittingRoomWorn? _overcoat;
    private FittingRoomWorn? _head;
    private FittingRoomTab _tab;
    private string _search = string.Empty;

    public bool IsOpen { get; private set; }

    public Gender Gender { get; private set; } = Gender.Male;
    public ushort HairStyle { get; private set; }
    public DisplayColor HairColor { get; private set; }
    public IReadOnlyList<FittingRoomLook> Looks { get; private set; } = [];

    public int Page { get; private set; }

    /// <summary>The look under the cursor, previewed on top of what is worn. Null when none.</summary>
    public FittingRoomLook? Hovered { get; set; }

    /// <summary>
    ///     Shows the figure without the player's overcoat, accessories and guild cloak, wearing only their hair.
    ///     Turning it on also takes off every piece being tried on.
    /// </summary>
    public bool Stripped
    {
        get;
        set
        {
            field = value;

            if (!value)
                return;

            _overcoat = null;
            _head = null;
            _accessories.Clear();
        }
    }

    public FittingRoomTab Tab
    {
        get => _tab;
        set
        {
            _tab = value;
            Page = 0;
        }
    }

    public string Search
    {
        get => _search;
        set
        {
            _search = value ?? string.Empty;
            Page = 0;
        }
    }

    public IReadOnlyList<FittingRoomWorn> Worn
    {
        get
        {
            var worn = new List<FittingRoomWorn>(5);

            if (_overcoat is not null)
                worn.Add(_overcoat);

            if (_head is not null)
                worn.Add(_head);

            worn.AddRange(_accessories);

            return worn;
        }
    }

    /// <summary>The looks of the current tab that match the gender and search filters.</summary>
    public IReadOnlyList<FittingRoomLook> FilteredLooks
    {
        get
        {
            var source = Tab == FittingRoomTab.Wearing ? Worn.Select(w => w.Look) : Looks.Where(InTab);

            source = source.Where(look => (look.Gender == Gender) || (look.Gender == Gender.Unisex));

            if (Search.Length > 0)
                source = source.Where(look => look.Name.Contains(Search, StringComparison.OrdinalIgnoreCase));

            return source.ToList();
        }
    }

    public int PageCount => Math.Max(1, (FilteredLooks.Count + PAGE_SIZE - 1) / PAGE_SIZE);

    public IReadOnlyList<FittingRoomLook> VisibleLooks => FilteredLooks.Skip(Page * PAGE_SIZE).Take(PAGE_SIZE).ToList();

    /// <summary>The figure the window shows: the base look with the worn pieces, and the hovered look on top.</summary>
    public AislingAppearance Effective
    {
        get
        {
            var overcoat = _overcoat;
            var head = _head;
            var accessories = _accessories.ToList();

            if (Hovered is { } hovered && !IsWorn(hovered))
                Put(hovered, ColorOf(hovered), ref overcoat, ref head, accessories);

            return FittingRoomOutfit.Compose(
                BaseLook(),
                HairColor,
                Piece(overcoat),
                Piece(head),
                accessories.Select(a => new FittingRoomPiece(a.Look.Look, a.Color)).ToList(),
                HairStyle);
        }
    }

    public void ApplyOpen(FittingRoomDisplayArgs args, AislingAppearance? liveLook)
    {
        Clear();
        Gender = args.Gender;
        HairStyle = args.HairStyle;
        HairColor = args.HairColor;
        Looks = args.Looks;
        //a rider is a head-only body with the mount drawn as its overcoat, so stripping it left no legs; show them dismounted
        _liveLook = liveLook is { BodySpriteId: AislingRenderer.HEAD_BODY_ID } riding
            ? riding with { BodySpriteId = AislingRenderer.BODY_ID, OvercoatSprite = 0, OvercoatColor = DisplayColor.Default }
            : liveLook;
        IsOpen = true;
    }

    public void Clear()
    {
        IsOpen = false;
        Gender = Gender.Male;
        HairStyle = 0;
        HairColor = DisplayColor.Default;
        Looks = [];
        _liveLook = null;
        _overcoat = null;
        _head = null;
        _accessories.Clear();
        _tab = FittingRoomTab.Overcoats;
        _search = string.Empty;
        Stripped = false;
        Page = 0;
        Hovered = null;
    }

    public void StepPage(int delta)
    {
        var count = PageCount;
        Page = (((Page + delta) % count) + count) % count;
    }

    public bool IsWorn(FittingRoomLook look) => WornColor(look).HasValue;

    /// <summary>The color <paramref name="look" /> is worn in, or null when it is not worn.</summary>
    public DisplayColor? WornColor(FittingRoomLook look) => Worn.FirstOrDefault(w => w.Look == look)?.Color;

    /// <summary>The color a tile for <paramref name="look" /> shows: the worn color, else the one the server lists first.</summary>
    public DisplayColor ColorOf(FittingRoomLook look) => WornColor(look) ?? look.Look.Color;

    /// <summary>The swatches offered for <paramref name="look" />: every color when it can be dyed, else the colors that exist.</summary>
    public IReadOnlyList<DisplayColor> SwatchColors(FittingRoomLook look) => look.Dyeable ? Enum.GetValues<DisplayColor>() : look.Colors;

    /// <summary>Puts <paramref name="look" /> on, or takes it off when it is already worn.</summary>
    public void Toggle(FittingRoomLook look)
    {
        if (IsWorn(look))
        {
            Remove(look);

            return;
        }

        Put(look, look.Look.Color, ref _overcoat, ref _head, _accessories);
    }

    public void Remove(FittingRoomLook look)
    {
        if (_overcoat?.Look == look)
            _overcoat = null;

        if (_head?.Look == look)
            _head = null;

        _accessories.RemoveAll(a => a.Look == look);
    }

    public void SetColor(FittingRoomLook look, DisplayColor color)
    {
        if (_overcoat?.Look == look)
            _overcoat = _overcoat with { Color = color };

        if (_head?.Look == look)
            _head = _head with { Color = color };

        for (var i = 0; i < _accessories.Count; i++)
            if (_accessories[i].Look == look)
                _accessories[i] = _accessories[i] with { Color = color };
    }

    private bool InTab(FittingRoomLook look)
        => (Tab, look.Look.Layer) switch
        {
            (FittingRoomTab.Overcoats, TownImportLayer.Overcoat or TownImportLayer.OvercoatAsArmor) => true,
            (FittingRoomTab.Hats, TownImportLayer.Head or TownImportLayer.OverHair)                 => true,
            (FittingRoomTab.Accessories, TownImportLayer.Accessory)                                 => true,
            _                                                                                       => false
        };

    private static void Put(
        FittingRoomLook look,
        DisplayColor color,
        ref FittingRoomWorn? overcoat,
        ref FittingRoomWorn? head,
        List<FittingRoomWorn> accessories)
    {
        var worn = new FittingRoomWorn(look, color);

        switch (look.Look.Layer)
        {
            case TownImportLayer.Overcoat or TownImportLayer.OvercoatAsArmor:
                overcoat = worn;

                break;
            case TownImportLayer.Head or TownImportLayer.OverHair:
                head = worn;
                KeepAccessories(accessories, AccessoryRoom(head));

                break;
            default:
                accessories.RemoveAll(a => a.Look == look);
                KeepAccessories(accessories, AccessoryRoom(head) - 1);
                accessories.Add(worn);

                break;
        }
    }

    //a crown takes one of the three accessory slots, so it leaves room for two accessories
    private static int AccessoryRoom(FittingRoomWorn? head)
        => head?.Look.Look.Layer == TownImportLayer.OverHair ? FittingRoomOutfit.MAX_ACCESSORIES - 1 : FittingRoomOutfit.MAX_ACCESSORIES;

    private static void KeepAccessories(List<FittingRoomWorn> accessories, int max)
    {
        while (accessories.Count > max)
            accessories.RemoveAt(0);
    }

    private static FittingRoomPiece? Piece(FittingRoomWorn? worn)
        => worn is null ? null : new FittingRoomPiece(worn.Look.Look, worn.Color);

    private AislingAppearance BaseLook()
    {
        var live = _liveLook ?? TownImportPreviewLook.Plain(Gender) with { HeadSprite = HairStyle, HeadColor = HairColor };

        if (!Stripped)
            return live;

        return live with
        {
            OvercoatSprite = 0,
            OvercoatColor = DisplayColor.Default,
            Accessory1Sprite = 0,
            Accessory1Color = DisplayColor.Default,
            Accessory2Sprite = 0,
            Accessory2Color = DisplayColor.Default,
            Accessory3Sprite = 0,
            Accessory3Color = DisplayColor.Default,
            GuildCloakDesignId = 0,
            HeadSprite = HairStyle,
            HeadColor = HairColor
        };
    }
}
