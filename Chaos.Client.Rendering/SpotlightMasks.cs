#region
using Chaos.Client.Data.Models;
using Chaos.DarkAges.Definitions;
#endregion

namespace Chaos.Client.Rendering;

/// <summary>
///     Darkness masks for Theatre spotlights: a soft floor oval with the game's 2:1 proportions, plus a softer column
///     above it so a person standing in the light is lit too. Values run 0-32 like lantern masks. The mask is centred on
///     the floor point. One per size, built once.
/// </summary>
public static class SpotlightMasks
{
    /// <summary>A floor radius of one tile spans about this many pixels across (and half as many down).</summary>
    public const float TILE_TO_PIXELS = 39.6f;

    /// <summary>How far above the floor point the lit column reaches, in pixels.</summary>
    public const int BODY_HEIGHT = 64;

    private static readonly Dictionary<StageLightSize, LightMask> Cache = [];

    /// <summary>Pool radius across the floor, in tiles: Small 1, Medium 1.5, Large 2.5.</summary>
    public static float RadiusTiles(StageLightSize size)
        => size switch
        {
            StageLightSize.Small => 1f,
            StageLightSize.Large => 2.5f,
            _                    => 1.5f
        };

    public static LightMask Get(StageLightSize size)
    {
        if (!Cache.TryGetValue(size, out var mask))
            Cache[size] = mask = Build(RadiusTiles(size));

        return mask;
    }

    public static LightMask Build(float radiusTiles)
    {
        var rx = radiusTiles * TILE_TO_PIXELS;
        var ry = rx / 2f;
        var columnHalfWidth = rx * 0.45f;
        var halfWidth = (int)MathF.Ceiling(rx);
        var halfHeight = (int)MathF.Ceiling(MathF.Max(ry, BODY_HEIGHT));
        var width = (halfWidth * 2) + 1;
        var height = (halfHeight * 2) + 1;
        var pixels = new byte[width * height];

        for (var py = 0; py < height; py++)
            for (var px = 0; px < width; px++)
            {
                var dx = px - halfWidth;
                var dy = py - halfHeight;
                var floor = Falloff(MathF.Sqrt((dx / rx * (dx / rx)) + (dy / ry * (dy / ry))));
                var body = 0f;

                if (dy < 0)
                {
                    var up = -dy / (float)BODY_HEIGHT;
                    var across = MathF.Abs(dx) / columnHalfWidth;
                    body = Falloff(MathF.Max(up, across)) * 0.9f;
                }

                pixels[(py * width) + px] = (byte)MathF.Round(MathF.Max(floor, body) * 32f);
            }

        return new LightMask
        {
            Width = width,
            Height = height,
            Pixels = pixels
        };
    }

    //1 inside 65% of the radius, easing to 0 at the edge
    private static float Falloff(float distance)
    {
        if (distance >= 1f)
            return 0f;

        if (distance <= 0.65f)
            return 1f;

        var t = (1f - distance) / 0.35f;

        return t * t * (3f - (2f * t));
    }
}
