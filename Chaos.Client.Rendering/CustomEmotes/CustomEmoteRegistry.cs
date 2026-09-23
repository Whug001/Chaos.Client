#region
using System.Diagnostics.CodeAnalysis;
#endregion

namespace Chaos.Client.Rendering.CustomEmotes;

/// <summary>
///     Every client-side emote, in the order the emote catalog lists them. Adding an emote means adding it here and
///     nowhere else.
/// </summary>
public static class CustomEmoteRegistry
{
    public static IReadOnlyList<ICustomEmote> All { get; } =
    [
        SunglassesEmote.Instance,
        MiddleFingerEmote.Instance,
        HeartEyesEmote.Instance,
        HaloEmote.Instance,
        LightbulbEmote.Instance,
        AngerSteamEmote.Instance,
        PartyHatEmote.Instance
    ];

    /// <summary>Finds the custom emote that rides on this body-animation byte.</summary>
    public static bool TryGet(int bodyAnimation, [NotNullWhen(true)] out ICustomEmote? emote)
    {
        foreach (var candidate in All)
            if (candidate.BodyAnimation == bodyAnimation)
            {
                emote = candidate;

                return true;
            }

        emote = null;

        return false;
    }

    /// <summary>Finds the custom emote whose wheel icon uses this icon code.</summary>
    public static bool TryGetByPreviewFrame(int previewFrame, [NotNullWhen(true)] out ICustomEmote? emote)
    {
        foreach (var candidate in All)
            if (candidate.PreviewFrame == previewFrame)
            {
                emote = candidate;

                return true;
            }

        emote = null;

        return false;
    }
}
