#region
using Chaos.Client.Rendering;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Client.Systems;

/// <summary>
///     Puts an imported cosmetic on a figure for the import windows' preview: the viewer's look with one layer swapped, the
///     way the server's <c>AislingMapperProfile</c> would show the item worn.
/// </summary>
public static class TownImportPreviewLook
{
    /// <summary>Returns <paramref name="look" /> with the item's layer swapped in.</summary>
    public static AislingAppearance Apply(in AislingAppearance look, TownImportLookInfo item)
    {
        var result = item.Layer switch
        {
            TownImportLayer.Accessory => look with
            {
                Accessory1Sprite = item.DisplaySprite,
                Accessory1Color = item.Color,

                //the guild cloak draws through the first accessory slot
                GuildCloakDesignId = 0
            },
            TownImportLayer.Overcoat => look with
            {
                OvercoatSprite = item.DisplaySprite,
                OvercoatColor = item.Color
            },
            TownImportLayer.OvercoatAsArmor => look with
            {
                ArmorSprite = item.DisplaySprite,
                OvercoatSprite = 0,
                OvercoatColor = DisplayColor.Default
            },
            _ => look with
            {
                HeadSprite = item.DisplaySprite,
                HeadColor = item.KeepsHairColor ? look.HeadColor : item.Color
            }
        };

        if (item.HidesHead)
            result = result with { HeadSprite = 0, HeadColor = DisplayColor.Default };

        if (item.HidesBoots)
            result = result with { BootsSprite = 0, BootsColor = DisplayColor.Default };

        return result;
    }

    /// <summary>A plain figure, for when the viewer's own look is not known yet.</summary>
    public static AislingAppearance Plain(Gender gender)
        => new()
        {
            Gender = gender,
            BodySpriteId = AislingRenderer.BODY_ID,
            HeadSprite = 1
        };
}
