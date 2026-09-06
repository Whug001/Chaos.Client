#region
using Chaos.Client.Data;
using Chaos.Client.Data.Repositories;
using DALib.Drawing;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SkiaSharp;
#endregion

namespace Chaos.Client.Rendering;

/// <summary>
///     Draws the middle finger gesture bubble above an already-composited aisling. The bubble is built by folding
///     emot01's Stop hand down to one finger — see <see cref="MiddleFingerEmote" /> for why, and for the geometry.
/// </summary>
/// <remarks>
///     One texture is cached per body colour, since the source frame is palettized through the body palette and a green
///     aisling needs a green hand. The textures are small (about 34x24) and there are at most ten body colours.
/// </remarks>
public sealed class MiddleFingerRenderer : IDisposable
{
    private readonly Dictionary<int, Texture2D?> Cache = [];
    private readonly AislingDrawDataRepository DrawData = DataContext.AislingDrawData;

    public void Dispose() => Clear();

    /// <summary>
    ///     Disposes the cached bubble textures. They rebuild on the next draw.
    /// </summary>
    public void Clear()
    {
        foreach (var texture in Cache.Values)
            texture?.Dispose();

        Cache.Clear();
    }

    /// <summary>
    ///     Draws the bubble above the head. Does nothing when the source art is missing or the wrong shape, so a repacked
    ///     emot01 makes the emote silent rather than garbled.
    /// </summary>
    /// <param name="batch">The sprite batch the aisling was just drawn into.</param>
    /// <param name="camera">Camera used to place the aisling composite.</param>
    /// <param name="bodyColor">The aisling's body colour, which the hand's skin tone follows.</param>
    /// <param name="flip">True when the aisling composite is drawn mirrored.</param>
    /// <param name="tileCenterX">World X of the entity's tile centre.</param>
    /// <param name="tileCenterY">World Y of the entity's tile centre.</param>
    /// <param name="visualOffset">The entity's sub-tile walk offset.</param>
    /// <param name="topPadding">The cached composite's top padding, so an oversized sprite keeps its bubble.</param>
    /// <param name="alpha">Alpha the aisling itself was drawn with.</param>
    public void Draw(
        SpriteBatch batch,
        Camera camera,
        int bodyColor,
        bool flip,
        float tileCenterX,
        float tileCenterY,
        Vector2 visualOffset,
        int topPadding,
        float alpha)
    {
        var texture = GetBubble(bodyColor);

        if (texture is null)
            return;

        //mirrors AislingRenderer.Draw: the composite's top-left corner in screen space, from which composite
        //pixel coordinates map one-to-one onto screen pixels.
        var baseX = tileCenterX + visualOffset.X - AislingRenderer.CANVAS_CENTER_X;
        var baseY = tileCenterY + visualOffset.Y - AislingRenderer.CANVAS_CENTER_Y - topPadding;
        var origin = camera.WorldToScreen(new Vector2(baseX, baseY));
        var leftX = MiddleFingerEmote.ResolveLeftX(flip, texture.Width);

        batch.Draw(
            texture,
            new Vector2(origin.X + leftX, origin.Y),
            null,
            Color.White * alpha,
            0f,
            Vector2.Zero,
            1f,
            flip ? SpriteEffects.FlipHorizontally : SpriteEffects.None,
            0f);
    }

    /// <summary>
    ///     Builds and caches the folded bubble for a body colour. Returns null — cached, so the work is not retried every
    ///     frame — when the source frame is absent or too small to fold.
    /// </summary>
    private Texture2D? GetBubble(int bodyColor)
    {
        if (Cache.TryGetValue(bodyColor, out var cached))
            return cached;

        var texture = Build(bodyColor);
        Cache[bodyColor] = texture;

        return texture;
    }

    private Texture2D? Build(int bodyColor)
    {
        var epf = DrawData.EmotionsEpf;

        if (epf is null || (MiddleFingerEmote.SOURCE_FRAME >= epf.Count))
            return null;

        if (!DrawData.BodyPalettes.TryGetValue(bodyColor, out var palette)
            && !DrawData.BodyPalettes.TryGetValue(0, out palette))
            return null;

        using var image = Graphics.RenderImage(epf[MiddleFingerEmote.SOURCE_FRAME], palette);

        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (image is null)
            return null;

        //the fold works in composite space; the frame itself sits at LAYER_OFFSET_PADDING within that canvas
        const int OFFSET = AislingRenderer.LAYER_OFFSET_PADDING;
        var height = MiddleFingerEmote.BUBBLE_BOTTOM_Y + 1;

        if (image.Height < height)
            return null;

        //crop to the bubble and its tail; rows 24 and below are the frame's own face, which would paint over
        //the character's real one
        using var bitmap = new SKBitmap(image.Width, height);

        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Transparent);
            canvas.DrawImage(image, 0, 0);
        }

        //a repacked or resized frame would put the fold over the wrong pixels — draw nothing rather than garbage
        if (!MiddleFingerEmote.TryFold(bitmap, OFFSET))
            return null;

        return TextureConverter.ToTexture2D(SKImage.FromBitmap(bitmap));
    }

}
