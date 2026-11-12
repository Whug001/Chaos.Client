#region
using Chaos.Client.Rendering;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Client.Systems;

/// <summary>Dresses the dye shop's mirror figure: the player's hair, and the selected item in the preview color.</summary>
public static class DyeShopFigure
{
    /// <summary>True when the item can be drawn on a figure of this gender.</summary>
    public static bool CanWear(DyeShopItem item, Gender player)
        => item.HasLook && ((item.Gender == Gender.Unisex) || (item.Gender == player));

    /// <summary>A plain figure with the player's hair, wearing <paramref name="item" /> dyed <paramref name="color" />.</summary>
    public static AislingAppearance Compose(
        Gender gender,
        ushort hairStyle,
        DisplayColor hairColor,
        DyeShopItem item,
        DisplayColor color)
    {
        var figure = TownImportPreviewLook.Plain(gender) with
        {
            HeadSprite = hairStyle,
            HeadColor = hairColor
        };

        return TownImportPreviewLook.Apply(figure, item.Look with { Color = color });
    }
}
