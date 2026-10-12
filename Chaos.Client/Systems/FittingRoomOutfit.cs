#region
using Chaos.Client.Rendering;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Client.Systems;

/// <summary>One tried-on cosmetic and the color chosen for it.</summary>
public readonly record struct FittingRoomPiece(TownImportLookInfo Look, DisplayColor Color);

/// <summary>
///     Composes the fitting room's preview figure: the base look with the tried-on overcoat, headwear and accessories
///     laid over it, the way the server's <c>AislingMapperProfile</c> would show them worn.
/// </summary>
public static class FittingRoomOutfit
{
    public const int MAX_ACCESSORIES = 3;

    public static AislingAppearance Compose(
        in AislingAppearance baseLook,
        DisplayColor hairColor,
        FittingRoomPiece? overcoat,
        FittingRoomPiece? head,
        IReadOnlyList<FittingRoomPiece> accessories,
        ushort hairStyle = 0)
    {
        var result = baseLook;

        //the player's own head-hiding costume arrives with no head drawn
        var headHidden = baseLook.HeadSprite == 0;

        if (overcoat is { } coat)
        {
            result = coat.Look.Layer == TownImportLayer.OvercoatAsArmor ? AsArmor(result, coat) : result with
            {
                OvercoatSprite = coat.Look.DisplaySprite,
                OvercoatColor = coat.Color
            };

            //the tried-on overcoat replaces that costume, so the hair shows again
            if (headHidden && (hairStyle != 0))
            {
                result = result with { HeadSprite = hairStyle, HeadColor = hairColor };
                headHidden = false;
            }
        }

        var count = Math.Min(accessories.Count, MAX_ACCESSORIES);

        for (var i = 0; i < count; i++)
            result = PlaceAccessory(result, i + 1, accessories[i].Look.DisplaySprite, accessories[i].Color);

        if (head is { } hat)
        {
            if (hat.Look.Layer == TownImportLayer.OverHair)
                result = PlaceAccessory(result, FirstEmptySlot(result), hat.Look.DisplaySprite, hat.Color);
            else if (!headHidden)
                result = result with { HeadSprite = hat.Look.DisplaySprite, HeadColor = HatColor(hat, hairColor) };
        }

        if (AnyWorn(overcoat, head, accessories, piece => piece.Look.HidesHead))
            result = result with { HeadSprite = 0, HeadColor = DisplayColor.Default };

        if (AnyWorn(overcoat, head, accessories, piece => piece.Look.HidesBoots))
            result = result with { BootsSprite = 0, BootsColor = DisplayColor.Default };

        return result;
    }

    /// <summary>
    ///     An overcoat drawn as armor moves into the armor slot. A colored one also stays in the overcoat slot, because that
    ///     is the only slot that carries a color (<c>AislingMapperProfile.ResolveBodyGarment</c>).
    /// </summary>
    public static AislingAppearance AsArmor(in AislingAppearance look, FittingRoomPiece coat)
        => look with
        {
            ArmorSprite = coat.Look.DisplaySprite,
            OvercoatSprite = coat.Color == DisplayColor.Default ? (ushort)0 : coat.Look.DisplaySprite,
            OvercoatColor = coat.Color
        };

    /// <summary>The accessory slot (1-3) an over-hair piece goes in: the first one with nothing shown, else the third.</summary>
    public static int FirstEmptySlot(in AislingAppearance look)
        => look.Accessory1Sprite == 0 ? 1 : look.Accessory2Sprite == 0 ? 2 : 3;

    /// <summary>Puts a sprite in accessory slot <paramref name="slot" /> (1-3); slot 1 also replaces the guild cloak.</summary>
    public static AislingAppearance PlaceAccessory(in AislingAppearance look, int slot, int sprite, DisplayColor color)
        => slot switch
        {
            1 => look with
            {
                Accessory1Sprite = sprite,
                Accessory1Color = color,
                GuildCloakDesignId = 0
            },
            2 => look with { Accessory2Sprite = sprite, Accessory2Color = color },
            _ => look with { Accessory3Sprite = sprite, Accessory3Color = color }
        };

    //the server groups a hat's Default and colored variants into one look, so hair color only shows through the Default swatch
    private static DisplayColor HatColor(FittingRoomPiece hat, DisplayColor hairColor)
        => hat.Look.KeepsHairColor && (hat.Color == DisplayColor.Default) ? hairColor : hat.Color;

    private static bool AnyWorn(
        FittingRoomPiece? overcoat,
        FittingRoomPiece? head,
        IReadOnlyList<FittingRoomPiece> accessories,
        Func<FittingRoomPiece, bool> test)
        => (overcoat is { } coat && test(coat)) || (head is { } hat && test(hat)) || accessories.Any(test);
}
