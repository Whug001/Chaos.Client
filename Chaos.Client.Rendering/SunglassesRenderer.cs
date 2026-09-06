#region
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Rendering;

/// <summary>
///     Draws the sunglasses emote on top of an already-composited aisling. The pixel art, timing and placement all come
///     from <see cref="SunglassesEmote" />; this class only turns those pixel maps into textures and blits them.
/// </summary>
/// <remarks>
///     The glasses deliberately do not go through <see cref="AislingRenderer" />'s layer stack. That stack caches one
///     composited texture per entity and rebuilds it whenever any input changes, so a sprite that moves every frame would
///     force a full recomposite every frame. Drawing on top costs two quads instead.
/// </remarks>
public sealed class SunglassesRenderer : IDisposable
{
    private Texture2D? _glasses;
    private Texture2D[]? _sparkle;

    public void Dispose() => Clear();

    /// <summary>
    ///     Disposes the cached textures. They rebuild on the next draw.
    /// </summary>
    public void Clear()
    {
        _glasses?.Dispose();
        _glasses = null;

        if (_sparkle is not null)
            foreach (var texture in _sparkle)
                texture.Dispose();

        _sparkle = null;
    }

    /// <summary>
    ///     Draws the emote at <paramref name="elapsedMs" /> into the frame. Does nothing once the emote has finished.
    /// </summary>
    /// <param name="batch">The sprite batch the aisling was just drawn into.</param>
    /// <param name="camera">Camera used to place the aisling composite.</param>
    /// <param name="elapsedMs">Milliseconds since the emote started.</param>
    /// <param name="flip">
    ///     True when the aisling composite is drawn mirrored, so the glasses mirror about the same pivot.
    /// </param>
    /// <param name="frameIndex">Current animation frame, used to follow the head as it bobs during a walk.</param>
    /// <param name="animSuffix">Current animation suffix, used with <paramref name="frameIndex" />.</param>
    /// <param name="tileCenterX">World X of the entity's tile centre.</param>
    /// <param name="tileCenterY">World Y of the entity's tile centre.</param>
    /// <param name="visualOffset">The entity's sub-tile walk offset.</param>
    /// <param name="topPadding">
    ///     The cached composite's top padding — how far <see cref="AislingRenderer" /> shifts that texture up for an
    ///     oversized sprite. The glasses shift with it or they detach from the face.
    /// </param>
    /// <param name="alpha">Alpha the aisling itself was drawn with, so ghosts and faded players match.</param>
    public void Draw(
        SpriteBatch batch,
        Camera camera,
        float elapsedMs,
        bool flip,
        int frameIndex,
        string animSuffix,
        float tileCenterX,
        float tileCenterY,
        Vector2 visualOffset,
        int topPadding,
        float alpha)
    {
        var frame = SunglassesEmote.Resolve(elapsedMs);

        if (frame.IsFinished)
            return;

        var glasses = GetGlasses();

        if (glasses is null)
            return;

        //mirrors AislingRenderer.Draw: the composite's top-left corner in screen space. Composite pixel
        //coordinates then map one-to-one onto screen pixels from there.
        var baseX = tileCenterX + visualOffset.X - AislingRenderer.CANVAS_CENTER_X;
        var baseY = tileCenterY + visualOffset.Y - AislingRenderer.CANVAS_CENTER_Y - topPadding;
        var origin = camera.WorldToScreen(new Vector2(baseX, baseY));

        var leftX = SunglassesEmote.ResolveLeftX(flip);
        var topY = SunglassesEmote.SETTLED_TOP_Y + frame.RowOffset + SunglassesEmote.HeadBobOffset(frameIndex, animSuffix);
        var tint = Color.White * alpha;

        //the glasses art is drawn for the unflipped pose, so mirror the sprite as well as its position
        var effects = flip ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

        DrawSprite(batch, glasses, origin, leftX, topY, tint, effects);

        if (frame.SparkleStage < 0)
            return;

        var sparkles = GetSparkles();

        if (sparkles is null || frame.SparkleStage >= sparkles.Length)
            return;

        var sparkle = sparkles[frame.SparkleStage];

        //the sparkle hangs off the glasses' top-left corner; flipping puts it off the top-right instead, the
        //same distance from the mirrored edge.
        var sparkleX = flip
            ? leftX + SunglassesEmote.GLASSES_WIDTH - SunglassesEmote.SPARKLE_OFFSET_X - sparkle.Width
            : leftX + SunglassesEmote.SPARKLE_OFFSET_X;

        DrawSprite(
            batch,
            sparkle,
            origin,
            sparkleX,
            topY + SunglassesEmote.SPARKLE_OFFSET_Y,
            tint,
            effects);
    }

    private static void DrawSprite(
        SpriteBatch batch,
        Texture2D texture,
        Vector2 origin,
        int compositeX,
        int compositeY,
        Color tint,
        SpriteEffects effects)
        => batch.Draw(
            texture,
            new Vector2(origin.X + compositeX, origin.Y + compositeY),
            null,
            tint,
            0f,
            Vector2.Zero,
            1f,
            effects,
            0f);

    private Texture2D? GetGlasses() => _glasses ??= BuildTexture(SunglassesEmote.GlassesPixels);

    private Texture2D[]? GetSparkles()
    {
        if (_sparkle is not null)
            return _sparkle;

        var built = new List<Texture2D>(SunglassesEmote.SparklePixels.Count);

        foreach (var stage in SunglassesEmote.SparklePixels)
        {
            var texture = BuildTexture(stage);

            if (texture is null)
                return null;

            built.Add(texture);
        }

        return _sparkle = [..built];
    }

    /// <summary>
    ///     Turns a character pixel map into a texture. Colours are premultiplied to match every other texture in the
    ///     client, which all come through <see cref="TextureConverter" />.
    /// </summary>
    private static Texture2D? BuildTexture(IReadOnlyList<string> rows)
    {
        var device = TextureConverter.Device;

        if (device is null || rows.Count == 0)
            return null;

        var width = rows[0].Length;
        var height = rows.Count;
        var pixels = new Color[width * height];

        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            if (!SunglassesEmote.TryGetColor(rows[y][x], out var color) || (color.A == 0))
                continue;

            pixels[y * width + x] = Color.FromNonPremultiplied(color.R, color.G, color.B, color.A);
        }

        var texture = new Texture2D(device, width, height);
        texture.SetData(pixels);

        return texture;
    }
}
