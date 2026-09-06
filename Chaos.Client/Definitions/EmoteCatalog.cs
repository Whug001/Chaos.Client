using Chaos.Client.Rendering;
using Chaos.Client.Systems;
using Chaos.DarkAges.Definitions;

namespace Chaos.Client.Definitions;

public readonly record struct EmoteCatalogEntry(BodyAnimation Animation, string Name, int PreviewFrame);

public static class EmoteCatalog
{
    public const int SLOT_COUNT = 6;

    /// <summary>
    ///     Preview-frame sentinel for the sunglasses emote. Every other entry's <see cref="EmoteCatalogEntry.PreviewFrame" />
    ///     indexes emot01 directly; the sunglasses have no emot01 frame, so this value tells
    ///     <c>UiRenderer.GetEmoteFaceTexture</c> to build the icon by stamping the glasses onto a plain face instead. It sits
    ///     well past emot01's 50 frames so it can never collide with a real one.
    /// </summary>
    public const int SUNGLASSES_PREVIEW_FRAME = SunglassesEmote.PREVIEW_FRAME;

    /// <summary>
    ///     Preview-frame sentinel for the middle finger emote. Same mechanism as
    ///     <see cref="SUNGLASSES_PREVIEW_FRAME" />.
    /// </summary>
    public const int MIDDLE_FINGER_PREVIEW_FRAME = MiddleFingerEmote.PREVIEW_FRAME;

    //matches WorldScreen.InputHandlers CtrlEmotes + CTRL_ALT_EMOTE_BASE(23) + ALT_EMOTE_BASE(34)
    private static readonly BodyAnimation[] CtrlTier =
    [
        BodyAnimation.Smile,
        BodyAnimation.Cry,
        BodyAnimation.Frown,
        BodyAnimation.Wink,
        BodyAnimation.Surprise,
        BodyAnimation.Tongue,
        BodyAnimation.Pleasant,
        BodyAnimation.Snore,
        BodyAnimation.Mouth,
        BodyAnimation.BlowKiss,
        BodyAnimation.Wave
    ];

    public static IReadOnlyList<EmoteCatalogEntry> All { get; } = BuildAll();

    public static BodyAnimation[] DefaultWheelSlots { get; } =
    [
        BodyAnimation.Smile,
        BodyAnimation.Wink,
        BodyAnimation.Frown,
        BodyAnimation.Cry,
        BodyAnimation.Surprise,
        BodyAnimation.Tongue
    ];

    public static bool TryGet(BodyAnimation animation, out EmoteCatalogEntry entry)
    {
        foreach (var e in All)
        {
            if (e.Animation == animation)
            {
                entry = e;
                return true;
            }
        }

        entry = default;
        return false;
    }

    private static IReadOnlyList<EmoteCatalogEntry> BuildAll()
    {
        var list = new List<EmoteCatalogEntry>(35);

        foreach (var anim in CtrlTier)
            list.Add(Make(anim));

        for (var i = 0; i < 11; i++)
            list.Add(Make((BodyAnimation)(23 + i)));

        for (var i = 0; i < 11; i++)
            list.Add(Make((BodyAnimation)(34 + i)));

        //client-side emotes with no emot01 frame of their own — see SunglassesEmote and MiddleFingerEmote
        //for why they ride on unused bytes
        list.Add(new EmoteCatalogEntry((BodyAnimation)SunglassesEmote.BODY_ANIMATION, "Sunglasses", SUNGLASSES_PREVIEW_FRAME));

        list.Add(
            new EmoteCatalogEntry((BodyAnimation)MiddleFingerEmote.BODY_ANIMATION, "Middle Finger", MIDDLE_FINGER_PREVIEW_FRAME));

        return list;
    }

    private static EmoteCatalogEntry Make(BodyAnimation anim)
    {
        var (frame, _, _) = AnimationSystem.ResolveEmoteFrames(anim);
        return new EmoteCatalogEntry(anim, FormatName(anim), frame);
    }

    private static string FormatName(BodyAnimation anim)
        => anim switch
        {
            BodyAnimation.SweatDrop   => "Sweat Drop",
            BodyAnimation.PuppyDog    => "Puppy Dog",
            BodyAnimation.StoneFaced  => "Stone Faced",
            BodyAnimation.FiredUp     => "Fired Up",
            BodyAnimation.BlowKiss    => "Blow Kiss",
            BodyAnimation.RockOn      => "Rock On",
            _                         => anim.ToString()
        };
}
