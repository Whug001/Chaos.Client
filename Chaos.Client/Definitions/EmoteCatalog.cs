using Chaos.Client.Systems;
using Chaos.DarkAges.Definitions;

namespace Chaos.Client.Definitions;

public readonly record struct EmoteCatalogEntry(BodyAnimation Animation, string Name, int PreviewFrame);

public static class EmoteCatalog
{
    public const int SLOT_COUNT = 6;

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
        var list = new List<EmoteCatalogEntry>(33);

        foreach (var anim in CtrlTier)
            list.Add(Make(anim));

        for (var i = 0; i < 11; i++)
            list.Add(Make((BodyAnimation)(23 + i)));

        for (var i = 0; i < 11; i++)
            list.Add(Make((BodyAnimation)(34 + i)));

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
