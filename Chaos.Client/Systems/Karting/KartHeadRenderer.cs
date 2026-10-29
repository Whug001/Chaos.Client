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
    private const int CROP_WIDTH = KartHeadPixelMath.CROP_WIDTH;
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
        var frame = front ? FRONT_IDLE_FRAME : BACK_IDLE_FRAME;
        using var figure = renderer.Render(in headLook, frame, AislingRenderer.IDLE_ANIM, flip, front);

        if (figure is null)
            return null;

        var baldLook = headLook with { HeadSprite = 0 };
        using var baldFigure = headLook.HeadSprite > 0
            ? renderer.Render(in baldLook, frame, AislingRenderer.IDLE_ANIM, flip, front)
            : null;

        var left = Math.Max(0, AislingRenderer.CANVAS_CENTER_X - (CROP_WIDTH / 2));
        var width = Math.Min(CROP_WIDTH, figure.Width - left);

        if (width <= 0)
            return null;

        using var hairScope = new PixelBufferScope(figure);
        var hairPixels = hairScope.AsSpan().ToArray();
        Color[]? baldPixels = null;

        if (baldFigure is not null && (baldFigure.Width == figure.Width) && (baldFigure.Height == figure.Height))
        {
            using var baldScope = new PixelBufferScope(baldFigure);
            baldPixels = baldScope.AsSpan().ToArray();
        }

        var crop = KartHeadPixelMath.Measure(hairPixels, baldPixels, figure.Width, figure.Height, left, width);

        if (crop is not { } bounds)
            return null;

        var height = bounds.Bottom - bounds.Top + 1;
        var pixels = new Color[width * height];

        for (var y = 0; y < height; y++)
            Array.Copy(hairPixels, ((bounds.Top + y) * figure.Width) + left, pixels, y * width, width);

        var texture = new Texture2D(figure.GraphicsDevice, width, height);
        texture.SetData(pixels);

        return new KartHeadSprite(texture, bounds.Origin);
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
