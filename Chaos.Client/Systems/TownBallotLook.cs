#region
using Chaos.Client.Collections;
using Chaos.Client.Data.Utilities;
using Chaos.Client.Rendering;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Client.Systems;

/// <summary>Turns a candidate's ballot look into the figure the window draws, the way the world draws an aisling.</summary>
public static class TownBallotLook
{
    /// <summary>A candidate not seen since the town hall release (null look) is drawn as a plain figure.</summary>
    public static AislingAppearance ToAppearance(TownBallotLookInfo? look)
    {
        if (look is null)
            return TownImportPreviewLook.Plain(Gender.Male);

        return new AislingAppearance
        {
            Gender = DataUtilities.DetermineGender(look.BodySprite),
            BodySpriteId = WorldState.GetBodySpriteId(look.BodySprite),
            BodyColor = look.BodySprite is BodySprite.MaleGhost or BodySprite.FemaleGhost ? WorldState.GHOST_BODY_COLOR : (int)look.BodyColor,
            HeadSprite = look.HeadSprite,
            HeadColor = look.HeadColor,
            FaceSprite = look.FaceSprite,
            ArmorSprite = look.ArmorSprite,
            ArmorColor = DisplayColor.Default,
            OvercoatSprite = look.OvercoatSprite,
            OvercoatColor = look.OvercoatColor,
            BootsSprite = look.BootsSprite,
            BootsColor = look.BootsColor,
            PantsColor = look.PantsColor,
            WeaponSprite = look.WeaponSprite,
            ShieldSprite = look.ShieldSprite,
            Accessory1Sprite = look.AccessorySprite1,
            Accessory1Color = look.AccessoryColor1,
            Accessory2Sprite = look.AccessorySprite2,
            Accessory2Color = look.AccessoryColor2,
            Accessory3Sprite = look.AccessorySprite3,
            Accessory3Color = look.AccessoryColor3
        };
    }
}
