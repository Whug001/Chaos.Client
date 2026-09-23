#region
using SkiaSharp;
#endregion

namespace Chaos.Client.Rendering.CustomEmotes;

/// <summary>
///     A client-side emote: one with no emot01 frame of its own. It rides on a body-animation byte the enum leaves
///     unused, and is drawn on top of the finished aisling composite rather than composited into it.
/// </summary>
public interface ICustomEmote
{
    /// <summary>
    ///     The body-animation byte the client sends and listens for. It must be in the 1-44 range the server relays
    ///     untouched, and must not be a defined <c>BodyAnimation</c> member.
    /// </summary>
    int BodyAnimation { get; }

    /// <summary>The name shown in the emote catalog.</summary>
    string Name { get; }

    /// <summary>
    ///     The icon code the wheel and catalog pass to <c>UiRenderer.GetEmoteFaceTexture</c>. It sits at 1000 or above,
    ///     past emot01's 50 frames, so it can never collide with a real frame.
    /// </summary>
    int PreviewFrame { get; }

    /// <summary>The emot01 frame the icon is painted onto.</summary>
    int IconSourceFrame { get; }

    /// <summary>Total length. Nothing is drawn from this point on.</summary>
    float DurationMs { get; }

    /// <summary>Draws the emote as it looks <paramref name="elapsedMs" /> after it started.</summary>
    void Draw(in CustomEmoteDrawContext context, float elapsedMs);

    /// <summary>
    ///     Paints the emote onto <paramref name="sourceFrame" /> — emot01 <see cref="IconSourceFrame" />, already
    ///     palettized with the viewer's body colour — for the wheel icon. Returns null when it can't, which leaves the
    ///     plain source frame as the icon.
    /// </summary>
    SKImage? BuildIcon(SKImage sourceFrame);
}
