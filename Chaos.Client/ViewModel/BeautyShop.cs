#region
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Client.ViewModel;

/// <summary>
///     Authoritative beauty shop state: the catalog and prices the server sent on Open, the player's current look,
///     and the look they are trying on. Pure -- no textures, no packets -- so the arithmetic is unit-tested and the
///     control only reads from it.
/// </summary>
public sealed class BeautyShop
{
    public bool IsOpen { get; private set; }

    //catalog
    public IReadOnlyList<BeautyShopHairstyleEntry> MaleHairstyles { get; private set; } = [];
    public IReadOnlyList<BeautyShopHairstyleEntry> FemaleHairstyles { get; private set; } = [];
    public IReadOnlyList<BeautyShopFaceEntry> Faces { get; private set; } = [];
    public IReadOnlyList<DisplayColor> HairColors { get; private set; } = [];
    public IReadOnlyList<BodyColor> BodyColors { get; private set; } = [];
    public int GenderPrice { get; private set; }
    public int HairDyePrice { get; private set; }
    public int BodyDyePrice { get; private set; }
    public int Gold { get; private set; }

    //current look (what the server says the player has now)
    public Gender CurrentGender { get; private set; }
    public int CurrentHairStyle { get; private set; }
    public DisplayColor CurrentHairColor { get; private set; }
    public BodyColor CurrentBodyColor { get; private set; }
    public int CurrentFaceSprite { get; private set; }

    //selection (what the preview shows)
    public Gender Gender { get; private set; }
    public int HairStyle { get; private set; }
    public DisplayColor HairColor { get; private set; }
    public BodyColor BodyColor { get; private set; }
    public int FaceSprite { get; private set; }

    public IReadOnlyList<BeautyShopHairstyleEntry> Hairstyles => Gender == Gender.Male ? MaleHairstyles : FemaleHairstyles;

    public IReadOnlyList<BeautyShopFaceEntry> AvailableFaces
        => Gender == Gender.Male ? Faces.Where(f => !f.FemaleOnly).ToList() : Faces;

    public bool GenderChanged => Gender != CurrentGender;
    public bool HairstyleChanged => HairStyle != CurrentHairStyle;
    public bool HairColorChanged => HairColor != CurrentHairColor;
    public bool BodyColorChanged => BodyColor != CurrentBodyColor;
    public bool FaceChanged => FaceSprite != CurrentFaceSprite;

    public int HairstylePrice => Hairstyles.FirstOrDefault(h => h.Sprite == HairStyle)?.Price ?? 0;
    public int FacePrice => Faces.FirstOrDefault(f => f.Sprite == FaceSprite)?.Price ?? 0;

    /// <summary>
    ///     Whether the face difference is actually charged. Mirrors the server's free forced-face-reset rule in
    ///     BeautyShopCheckout.TryApply: switching gender away from a face the new gender can't wear forces a reset
    ///     to the default face (the catalog's first entry) for free -- picking any other face is still a purchase.
    /// </summary>
    public bool FaceCharged
        => FaceChanged && !(GenderChanged && !IsFaceAvailable(Gender, CurrentFaceSprite) && (Faces.Count > 0) && (FaceSprite == Faces[0].Sprite));

    public int Total => PriceOf(Gender, HairStyle, HairColor, BodyColor, FaceSprite);

    public bool CanAfford => Total <= Gold;
    public bool CanApply => (Total > 0) && CanAfford;

    //hover: at most one category at a time; the preview shows hovered ?? selected, money never does
    public int? HoveredHairStyle { get; private set; }
    public DisplayColor? HoveredHairColor { get; private set; }
    public BodyColor? HoveredBodyColor { get; private set; }
    public int? HoveredFaceSprite { get; private set; }

    public bool IsHovering => HoveredHairStyle.HasValue || HoveredHairColor.HasValue || HoveredBodyColor.HasValue || HoveredFaceSprite.HasValue;

    public int EffectiveHairStyle => HoveredHairStyle ?? HairStyle;
    public DisplayColor EffectiveHairColor => HoveredHairColor ?? HairColor;
    public BodyColor EffectiveBodyColor => HoveredBodyColor ?? BodyColor;
    public int EffectiveFaceSprite => HoveredFaceSprite ?? FaceSprite;

    public bool HasUnsavedChanges => GenderChanged || HairstyleChanged || HairColorChanged || BodyColorChanged || FaceChanged;

    /// <summary>The total if the hovered item were chosen; <see cref="Total" /> when nothing is hovered.</summary>
    public int HoverTotal
        => PriceOf(Gender, EffectiveHairStyle, EffectiveHairColor, EffectiveBodyColor, EffectiveFaceSprite);

    public void SetHoverHairStyle(int sprite) { ClearHover(); HoveredHairStyle = sprite; }
    public void SetHoverHairColor(DisplayColor color) { ClearHover(); HoveredHairColor = color; }
    public void SetHoverBodyColor(BodyColor color) { ClearHover(); HoveredBodyColor = color; }
    public void SetHoverFace(int sprite) { ClearHover(); HoveredFaceSprite = sprite; }

    public void ClearHover()
    {
        HoveredHairStyle = null;
        HoveredHairColor = null;
        HoveredBodyColor = null;
        HoveredFaceSprite = null;
    }

    /// <summary>Prices an arbitrary look against the current one with the same rules as <see cref="Total" />.</summary>
    private int PriceOf(Gender gender, int hairStyle, DisplayColor hairColor, BodyColor bodyColor, int faceSprite)
    {
        var genderChanged = gender != CurrentGender;
        var faceChanged = faceSprite != CurrentFaceSprite;

        var faceCharged = faceChanged
                          && !(genderChanged && !IsFaceAvailable(gender, CurrentFaceSprite) && (Faces.Count > 0) && (faceSprite == Faces[0].Sprite));

        var hairstylePrice = (gender == Gender.Male ? MaleHairstyles : FemaleHairstyles).FirstOrDefault(h => h.Sprite == hairStyle)?.Price ?? 0;
        var facePrice = Faces.FirstOrDefault(f => f.Sprite == faceSprite)?.Price ?? 0;

        return (genderChanged ? GenderPrice : 0)
               + (hairStyle != CurrentHairStyle ? hairstylePrice : 0)
               + (hairColor != CurrentHairColor ? HairDyePrice : 0)
               + (bodyColor != CurrentBodyColor ? BodyDyePrice : 0)
               + (faceCharged ? facePrice : 0);
    }

    public void ApplyOpen(BeautyShopDisplayArgs args)
    {
        MaleHairstyles = args.MaleHairstyles;
        FemaleHairstyles = args.FemaleHairstyles;
        Faces = args.Faces;
        HairColors = args.HairColors;
        BodyColors = args.BodyColors;
        GenderPrice = args.GenderPrice;
        HairDyePrice = args.HairDyePrice;
        BodyDyePrice = args.BodyDyePrice;
        Gold = args.Gold;

        CurrentGender = args.Gender;
        CurrentHairStyle = args.HairStyle;
        CurrentHairColor = args.HairColor;
        CurrentBodyColor = args.BodyColor;
        CurrentFaceSprite = args.FaceSprite;

        Reset();
        IsOpen = true;
    }

    public void Clear()
    {
        ClearHover();
        IsOpen = false;
        MaleHairstyles = [];
        FemaleHairstyles = [];
        Faces = [];
        HairColors = [];
        BodyColors = [];
        GenderPrice = HairDyePrice = BodyDyePrice = Gold = 0;
        CurrentGender = Gender = Gender.Male;
        CurrentHairStyle = HairStyle = 0;
        CurrentHairColor = HairColor = DisplayColor.Default;
        CurrentBodyColor = BodyColor = BodyColor.White;
        CurrentFaceSprite = FaceSprite = 0;
    }

    public void Reset()
    {
        ClearHover();
        Gender = CurrentGender;
        HairStyle = CurrentHairStyle;
        HairColor = CurrentHairColor;
        BodyColor = CurrentBodyColor;
        FaceSprite = CurrentFaceSprite;
    }

    /// <summary>
    ///     Switches gender. The hairstyle keeps its id if the other list has it (most ids exist for both), else
    ///     falls back to the first entry; a female-only face falls back to the first face a male may wear -- the
    ///     same rules the server applies, so the preview never shows something Apply would refuse.
    /// </summary>
    public void SetGender(Gender gender)
    {
        ClearHover();

        if (Gender == gender)
            return;

        Gender = gender;

        if (Hairstyles.All(h => h.Sprite != HairStyle))
            HairStyle = Hairstyles.Count > 0 ? Hairstyles[0].Sprite : 0;

        if (AvailableFaces.All(f => f.Sprite != FaceSprite))
            FaceSprite = AvailableFaces.Count > 0 ? AvailableFaces[0].Sprite : 0;
    }

    public void StepHairstyle(int delta)
    {
        ClearHover();
        HairStyle = StepEntry(Hairstyles, h => h.Sprite == HairStyle, delta)?.Sprite ?? HairStyle;
    }

    public void StepFace(int delta)
    {
        ClearHover();
        FaceSprite = StepEntry(AvailableFaces, f => f.Sprite == FaceSprite, delta)?.Sprite ?? FaceSprite;
    }

    public void StepHairColor(int delta)
    {
        ClearHover();
        var next = StepValue(HairColors, c => c == HairColor, delta);

        if (next.HasValue)
            HairColor = next.Value;
    }

    public void StepBodyColor(int delta)
    {
        ClearHover();
        var next = StepValue(BodyColors, c => c == BodyColor, delta);

        if (next.HasValue)
            BodyColor = next.Value;
    }

    /// <summary>Wrapping step through <paramref name="list" /> from the entry matching <paramref name="isCurrent" /> (or from the start when none matches).</summary>
    private static T? StepEntry<T>(IReadOnlyList<T> list, Func<T, bool> isCurrent, int delta) where T: class
    {
        if (list.Count == 0)
            return null;

        var index = IndexOfCurrent(list, isCurrent);
        var next = WrapIndex(index, delta, list.Count);

        return list[next];
    }

    /// <summary>Wrapping step through <paramref name="list" /> from the entry matching <paramref name="isCurrent" /> (or from the start when none matches).</summary>
    private static T? StepValue<T>(IReadOnlyList<T> list, Func<T, bool> isCurrent, int delta) where T: struct
    {
        if (list.Count == 0)
            return null;

        var index = IndexOfCurrent(list, isCurrent);
        var next = WrapIndex(index, delta, list.Count);

        return list[next];
    }

    /// <summary>Whether <paramref name="sprite" /> is a face <paramref name="gender" /> may wear -- exists in the catalog and isn't female-only on a male.</summary>
    private bool IsFaceAvailable(Gender gender, int sprite)
    {
        var entry = Faces.FirstOrDefault(f => f.Sprite == sprite);

        return (entry != null) && !(entry.FemaleOnly && (gender == Gender.Male));
    }

    private static int IndexOfCurrent<T>(IReadOnlyList<T> list, Func<T, bool> isCurrent)
    {
        for (var i = 0; i < list.Count; i++)
            if (isCurrent(list[i]))
                return i;

        return -1;
    }

    private static int WrapIndex(int index, int delta, int count)
    {
        var next = index < 0 ? 0 : (index + delta) % count;

        if (next < 0)
            next += count;

        return next;
    }
}
