#region
using SkiaSharp;
#endregion

namespace Chaos.Client.Rendering.CustomEmotes;

/// <summary>
///     Base for emotes made of hand-drawn <see cref="PixelArt" />. An emote only describes what is on screen at a
///     moment, as a list of layers; drawing, flipping, icons and previews all come from that one description.
/// </summary>
public abstract class PixelArtEmote : ICustomEmote
{
    public abstract int BodyAnimation { get; }
    public abstract string Name { get; }
    public abstract int PreviewFrame { get; }
    public virtual int IconSourceFrame => CustomEmoteGeometry.PLAIN_FACE_FRAME;
    public abstract float DurationMs { get; }

    /// <summary>The moment whose layers become the wheel icon. Pick one where every layer is fully opaque.</summary>
    public abstract float IconTimeMs { get; }

    /// <summary>
    ///     What is on screen <paramref name="elapsedMs" /> after the emote started, in unflipped composite coordinates.
    ///     Returns an empty list outside 0 to <see cref="DurationMs" />.
    /// </summary>
    /// <param name="elapsedMs">Milliseconds since the emote started.</param>
    /// <param name="headBobRows">Rows the head sits lower on the current walk frame.</param>
    public abstract IReadOnlyList<PixelLayer> Compose(float elapsedMs, int headBobRows);

    public void Draw(in CustomEmoteDrawContext context, float elapsedMs)
    {
        if ((elapsedMs < 0f) || (elapsedMs >= DurationMs))
            return;

        var layers = Compose(elapsedMs, context.HeadBobRows);

        for (var i = 0; i < layers.Count; i++)
        {
            var layer = layers[i];
            var texture = context.Textures.GetOrBuild(layer.Art);

            if (texture is not null)
                context.DrawTexture(texture, layer.X, layer.Y, layer.Opacity);
        }
    }

    /// <summary>
    ///     An emot01 frame is composited into the aisling at an X offset of
    ///     <see cref="AislingRenderer.LAYER_OFFSET_PADDING" /> and a Y offset of zero, so the source frame is placed there
    ///     and the icon layers land on the same pixels they cover in the world.
    /// </summary>
    public SKImage? BuildIcon(SKImage sourceFrame)
        => PixelSprite.Stamp(sourceFrame, AislingRenderer.LAYER_OFFSET_PADDING, 0, Compose(IconTimeMs, 0));
}
