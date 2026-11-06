using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;

namespace Chaos.Client.Collections;

public static class EntityLookMath
{
    public const float FLOAT_BASE_PIXELS = 12f;
    public const float FLOAT_BOB_PIXELS = 3f;
    public const double FLOAT_PERIOD_SECONDS = 2.0;

    public static float FloatRaise(double seconds)
        => FLOAT_BASE_PIXELS + FLOAT_BOB_PIXELS * (float)Math.Sin(2 * Math.PI * seconds / FLOAT_PERIOD_SECONDS);

    public static float Scale(EntityLookArgs? look) => look is null ? 1f : look.ScalePercent / 100f;

    /// <summary>
    ///     Scales a click hitbox about its bottom center, so a giant's box stays planted on the same tile point.
    /// </summary>
    public static Rectangle HitBox(Rectangle baseRect, float scale)
    {
        if (scale == 1f)
            return baseRect;

        var w = (int)MathF.Round(baseRect.Width * scale);
        var h = (int)MathF.Round(baseRect.Height * scale);

        return new Rectangle(baseRect.Center.X - w / 2, baseRect.Bottom - h, w, h);
    }

    public static int OverlayOffset(int baseOffset, float scale) => (int)MathF.Round(baseOffset * scale);

    /// <summary>
    ///     World-space Y offset for an entity-anchored overlay: the resolved offset scaled with the entity,
    ///     plus the float raise when the entity floats so overlays move with the sprite.
    /// </summary>
    public static float OverlayAnchorY(int resolvedOffset, float scale, bool floating, double nowSeconds)
        => OverlayOffset(resolvedOffset, scale) + (floating ? FloatRaise(nowSeconds) : 0f);
}
