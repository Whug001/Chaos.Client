#region
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Rendering.CustomEmotes;

/// <summary>
///     Draws the custom emote an aisling is playing on top of its already-composited sprite.
/// </summary>
/// <remarks>
///     Custom emotes deliberately do not go through <see cref="AislingRenderer" />'s layer stack. That stack caches one
///     composited texture per entity and rebuilds it whenever any input changes, so art that moves every frame would
///     force a full recomposite every frame. Drawing on top costs a few quads instead.
/// </remarks>
public sealed class CustomEmoteRenderer : IDisposable
{
    private readonly CustomEmoteTextureCache Textures = new();

    public void Dispose() => Textures.Dispose();

    /// <summary>Disposes the cached textures. They rebuild on the next draw.</summary>
    public void Clear() => Textures.Clear();

    /// <param name="batch">The sprite batch the aisling was just drawn into.</param>
    /// <param name="camera">Camera used to place the aisling composite.</param>
    /// <param name="emote">The emote to draw.</param>
    /// <param name="elapsedMs">Milliseconds since the emote started.</param>
    /// <param name="flip">True when the aisling composite is drawn mirrored.</param>
    /// <param name="frameIndex">Current animation frame, used to follow the head as it bobs during a walk.</param>
    /// <param name="animSuffix">Current animation suffix, used with <paramref name="frameIndex" />.</param>
    /// <param name="bodyColor">The aisling's body colour.</param>
    /// <param name="tileCenterX">World X of the entity's tile centre.</param>
    /// <param name="tileCenterY">World Y of the entity's tile centre.</param>
    /// <param name="visualOffset">The entity's sub-tile walk offset.</param>
    /// <param name="topPadding">
    ///     The cached composite's top padding — how far <see cref="AislingRenderer" /> shifts that texture up for an
    ///     oversized sprite. The emote shifts with it or it detaches from the head.
    /// </param>
    /// <param name="alpha">Alpha the aisling itself was drawn with.</param>
    public void Draw(
        SpriteBatch batch,
        Camera camera,
        ICustomEmote emote,
        float elapsedMs,
        bool flip,
        int frameIndex,
        string animSuffix,
        int bodyColor,
        float tileCenterX,
        float tileCenterY,
        Vector2 visualOffset,
        int topPadding,
        float alpha)
    {
        //mirrors AislingRenderer.Draw: the composite's top-left corner in screen space
        var baseX = tileCenterX + visualOffset.X - AislingRenderer.CANVAS_CENTER_X;
        var baseY = tileCenterY + visualOffset.Y - AislingRenderer.CANVAS_CENTER_Y - topPadding;

        var context = new CustomEmoteDrawContext(
            batch,
            camera.WorldToScreen(new Vector2(baseX, baseY)),
            flip,
            CustomEmoteGeometry.HeadBobOffset(frameIndex, animSuffix),
            alpha,
            bodyColor,
            Textures);

        emote.Draw(in context, elapsedMs);
    }
}
