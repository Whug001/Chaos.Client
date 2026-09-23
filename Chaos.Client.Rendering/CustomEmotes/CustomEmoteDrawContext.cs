#region
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Rendering.CustomEmotes;

/// <summary>
///     What a custom emote needs to draw itself on top of one aisling's finished composite.
/// </summary>
/// <param name="Batch">The sprite batch the aisling was just drawn into.</param>
/// <param name="Origin">
///     Screen position of the composite's top-left corner. Composite pixel coordinates map one-to-one onto screen pixels
///     from here.
/// </param>
/// <param name="Flip">True when the aisling composite is drawn mirrored.</param>
/// <param name="HeadBobRows">Rows the head sits lower on the current walk frame. See <see cref="CustomEmoteGeometry.HeadBobOffset" />.</param>
/// <param name="Alpha">Alpha the aisling itself was drawn with, so transparent and faded players match.</param>
/// <param name="BodyColor">The aisling's body colour, for art built from the body palette.</param>
/// <param name="Textures">The shared custom-emote texture cache.</param>
public readonly record struct CustomEmoteDrawContext(
    SpriteBatch Batch,
    Vector2 Origin,
    bool Flip,
    int HeadBobRows,
    float Alpha,
    int BodyColor,
    CustomEmoteTextureCache Textures)
{
    /// <summary>
    ///     Composite X of a texture's left edge once flipping is applied. Mirroring maps the texture's right edge onto its
    ///     new left edge, about the same pivot the aisling sprite uses.
    /// </summary>
    public static int ResolveLeftX(bool flip, int compositeX, int width)
        => flip ? AislingRenderer.MirrorX(compositeX + width - 1) : compositeX;

    /// <summary>
    ///     Draws a texture whose unflipped top-left corner is at composite (<paramref name="compositeX" />,
    ///     <paramref name="compositeY" />). When the aisling is flipped, the texture is mirrored and moved to match.
    /// </summary>
    public void DrawTexture(Texture2D texture, int compositeX, int compositeY, float opacity = 1f)
    {
        var x = ResolveLeftX(Flip, compositeX, texture.Width);

        Batch.Draw(
            texture,
            new Vector2(Origin.X + x, Origin.Y + compositeY),
            null,
            Color.White * (Alpha * opacity),
            0f,
            Vector2.Zero,
            1f,
            Flip ? SpriteEffects.FlipHorizontally : SpriteEffects.None,
            0f);
    }
}
