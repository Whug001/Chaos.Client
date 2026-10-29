#region
using Chaos.Client.Rendering;
using Chaos.DarkAges.Definitions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Systems.Karting;

/// <summary>
///     A racer's own head and hair, seated on the kart's driver in place of the helmet. Each head is the head-only body
///     form (<see cref="AislingRenderer.HEAD_BODY_ID" />, which hides all gear) in one idle facing, cut to its pixels.
///     The hair comes from the race roster, because a worn helmet replaces it in the racer's normal look. This class
///     owns every texture it hands out; <see cref="Clear" /> runs when the race state resets.
/// </summary>
public sealed class KartHeadRenderer(AislingRenderer renderer) : IDisposable
{
    private const int CROP_WIDTH = 30;
    private const int BACK_IDLE_FRAME = 0;
    private const int FRONT_IDLE_FRAME = 5;

    private readonly Dictionary<(AislingAppearance Look, bool Front, bool Flip), KartHeadSprite?> Cache = [];

    public static AislingAppearance HeadLook(in AislingAppearance look, ushort hairStyle, DisplayColor hairColor)
        => new()
        {
            Gender = look.Gender,
            BodySpriteId = AislingRenderer.HEAD_BODY_ID,
            BodyColor = look.BodyColor,
            FaceSprite = look.FaceSprite,
            HeadSprite = hairStyle,
            HeadColor = hairColor
        };

    public KartHeadSprite? Get(in AislingAppearance headLook, bool front, bool flip)
    {
        var key = (headLook, front, flip);

        if (Cache.TryGetValue(key, out var cached))
            return cached;

        var sprite = Build(in headLook, front, flip);
        Cache[key] = sprite;

        return sprite;
    }

    private KartHeadSprite? Build(in AislingAppearance headLook, bool front, bool flip)
    {
        using var figure = renderer.Render(in headLook, front ? FRONT_IDLE_FRAME : BACK_IDLE_FRAME, AislingRenderer.IDLE_ANIM, flip, front);

        if (figure is null)
            return null;

        var left = Math.Max(0, AislingRenderer.CANVAS_CENTER_X - (CROP_WIDTH / 2));
        var width = Math.Min(CROP_WIDTH, figure.Width - left);

        if ((width <= 0) || ContentRows(figure, left, width) is not var (top, bottom))
            return null;

        var height = bottom - top + 1;
        var pixels = new Color[width * height];
        figure.GetData(0, new Rectangle(left, top, width, height), pixels, 0, pixels.Length);
        var texture = new Texture2D(figure.GraphicsDevice, width, height);
        texture.SetData(pixels);

        return new KartHeadSprite(texture, new Vector2(ChinCenter(pixels, width, height) ?? AislingRenderer.CANVAS_CENTER_X - left, height));
    }

    /// <summary>
    ///     The middle of the opaque columns in the bottom rows of the cut head. The head form is not drawn at the canvas
    ///     centre, so the chin is found from the pixels.
    /// </summary>
    private static float? ChinCenter(Color[] pixels, int width, int height)
    {
        const int CHIN_ROWS = 3;
        var minX = int.MaxValue;
        var maxX = -1;

        for (var y = Math.Max(0, height - CHIN_ROWS); y < height; y++)
            for (var x = 0; x < width; x++)
                if (pixels[y * width + x].A > 16)
                {
                    minX = Math.Min(minX, x);
                    maxX = Math.Max(maxX, x);
                }

        return maxX < 0 ? null : (minX + maxX + 1) / 2f;
    }

    /// <summary>First and last rows with a visible pixel inside the crop columns: the top of the hair and the chin.</summary>
    private static (int Top, int Bottom)? ContentRows(Texture2D texture, int left, int width)
    {
        using var scope = new PixelBufferScope(texture);
        var pixels = scope.AsSpan();
        int? top = null;
        var bottom = 0;

        for (var y = 0; y < scope.Height; y++)
            for (var x = left; x < left + width; x++)
                if (pixels[y * scope.Width + x].A > 16)
                {
                    top ??= y;
                    bottom = y;

                    break;
                }

        return top is { } first ? (first, bottom) : null;
    }

    public void Clear()
    {
        foreach (var sprite in Cache.Values)
            sprite?.Texture.Dispose();

        Cache.Clear();
    }

    public void Dispose() => Clear();
}

/// <summary>A cut head and its origin: the middle of the chin, which is drawn on the driver's neck.</summary>
public sealed record KartHeadSprite(Texture2D Texture, Vector2 Origin);
