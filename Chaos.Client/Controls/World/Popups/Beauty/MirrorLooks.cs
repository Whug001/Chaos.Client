#region
using Chaos.Client.Collections;
using Chaos.Client.Rendering;
using Chaos.DarkAges.Definitions;
#endregion

namespace Chaos.Client.Controls.World.Popups.Beauty;

/// <summary>
///     The looks the mirror draws. Bare body + hair + face by default so every change is visible; with Show gear on,
///     the player's live world appearance (armor, helmet, weapon...) with the five editable fields swapped in.
/// </summary>
public static class MirrorLooks
{
    public static AislingAppearance Bare(Gender gender, int hairStyle, DisplayColor hairColor, BodyColor bodyColor, int faceSprite)
        => new()
        {
            Gender = gender,
            BodySpriteId = AislingRenderer.BODY_ID,
            BodyColor = (int)bodyColor,
            HeadSprite = hairStyle,
            HeadColor = hairColor,
            FaceSprite = faceSprite
        };

    /// <summary>The look being tried on. Uses the effective (hover-aware) values so the figure shows what the player is pointing at.</summary>
    public static AislingAppearance New(ViewModel.BeautyShop vm, bool showGear)
        => Dressed(showGear, vm.Gender, vm.EffectiveHairStyle, vm.EffectiveHairColor, vm.EffectiveBodyColor, vm.EffectiveFaceSprite);

    /// <summary>The look the player had when the mirror opened.</summary>
    public static AislingAppearance Now(ViewModel.BeautyShop vm, bool showGear)
        => Dressed(showGear, vm.CurrentGender, vm.CurrentHairStyle, vm.CurrentHairColor, vm.CurrentBodyColor, vm.CurrentFaceSprite);

    private static AislingAppearance Dressed(bool showGear, Gender gender, int hairStyle, DisplayColor hairColor, BodyColor bodyColor, int faceSprite)
    {
        var bare = Bare(gender, hairStyle, hairColor, bodyColor, faceSprite);

        if (!showGear)
            return bare;

        var live = WorldState.GetPlayerEntity()?.Appearance;

        if (live is null)
            return bare;

        return live.Value with
        {
            Gender = gender,
            BodyColor = (int)bodyColor,
            HeadSprite = hairStyle,
            HeadColor = hairColor,
            FaceSprite = faceSprite
        };
    }
}
