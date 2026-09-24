#region
using Chaos.Client.Data.Models;
using Chaos.Geometry.Abstractions.Definitions;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Rendering;

/// <summary>
///     One light for the darkness layer and the Tab map. <see cref="Strength" /> scales the mask, 0-32: lanterns use 32,
///     Theatre spotlights use their current brightness.
/// </summary>
public readonly record struct LightSource(
    Vector2 ScreenPosition,
    int TileX,
    int TileY,
    Direction Direction,
    LightMask PixelMask,
    (int Dx, int Dy)[] TileOffsets,
    byte Strength = 32);
