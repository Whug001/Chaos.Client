#region
using Chaos.Client.Collections;
using Chaos.Client.Data;
using Chaos.DarkAges.Definitions;
using Chaos.Geometry.Abstractions.Definitions;
using DALib.Data;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Systems;

/// <summary>
///     Owns the per-frame light source buffer and the gather logic that builds it from world entities.
///     Both <see cref="DarknessRenderer" /> and <see cref="TabMapRenderer" /> consume from
///     <see cref="Sources" /> as pure read-only consumers — neither stores its own copy.
/// </summary>
/// <remarks>
///     Tile-space offset shapes (Euclidean circles for lanterns, baseline visibility) live here as
///     static cached arrays so every light source of a given lantern size shares the same array
///     reference. Future direction-aware shapes (cones, lines) drop in by emitting a different
///     cached array per direction inside <see cref="GetTileOffsets" />.
/// </remarks>
public sealed class LightingSystem
{
    private static readonly (int Dx, int Dy)[] Euclidean3 = ComputeEuclidean(3);
    private static readonly (int Dx, int Dy)[] Euclidean5 = ComputeEuclidean(5);
    private static readonly (int Dx, int Dy)[] Euclidean1 = ComputeEuclidean(1);
    private static readonly (int Dx, int Dy)[] Euclidean2 = ComputeEuclidean(2);

    /// <summary>
    ///     The unconditional baseline visibility around the player on a full-black-darkness map:
    ///     the player's own tile only. Adjacent tiles require an actual light source to reveal.
    /// </summary>
    public static readonly (int Dx, int Dy)[] BaselineVisibilityOffsets = ComputeEuclidean(0);

    private LightSource[] Buffer = new LightSource[16];
    private int Count;

    /// <summary>
    ///     The light sources gathered on the most recent <see cref="Gather" /> call. Span lifetime
    ///     is bounded by the next <see cref="Gather" /> call.
    /// </summary>
    public ReadOnlySpan<LightSource> Sources => Buffer.AsSpan(0, Count);

    /// <summary>
    ///     Walks the world entity list and builds the light source array for the current frame, then adds the Theatre
    ///     spotlights in <paramref name="spotlights" />. Short-circuits to an empty span when the map isn't dark, so stale
    ///     sources from a prior map can't leak across a transition.
    /// </summary>
    public void Gather(MapFile? mapFile, MapFlags flags, Camera camera, IReadOnlyList<StageLightFrame> spotlights)
    {
        Count = 0;

        if (mapFile is null || !flags.HasFlag(MapFlags.Darkness))
            return;

        foreach (var entity in WorldState.GetEntities())
        {
            if (entity.LanternSize == LanternSize.None)
                continue;

            var pixelMask = DataContext.LightMasks.Get(entity.LanternSize);

            if (pixelMask is null)
                continue;

            var tileOffsets = GetTileOffsets(entity.LanternSize, entity.Direction);

            var tileWorld = Camera.TileToWorld(entity.TileX, entity.TileY, mapFile.Height);
            var tileCenterX = tileWorld.X + DaLibConstants.HALF_TILE_WIDTH;
            var tileCenterY = tileWorld.Y + DaLibConstants.HALF_TILE_HEIGHT;
            var screenPos = camera.WorldToScreen(new Vector2(tileCenterX + entity.VisualOffset.X, tileCenterY + entity.VisualOffset.Y));

            if (Count >= Buffer.Length)
                Array.Resize(ref Buffer, Buffer.Length * 2);

            Buffer[Count++] = new LightSource(
                screenPos,
                entity.TileX,
                entity.TileY,
                entity.Direction,
                pixelMask,
                tileOffsets);
        }

        foreach (var spot in spotlights)
        {
            var strength = (byte)Math.Clamp((int)MathF.Round(spot.Strength * 32f), 0, 32);

            if (strength == 0)
                continue;

            if (Count >= Buffer.Length)
                Array.Resize(ref Buffer, Buffer.Length * 2);

            Buffer[Count++] = new LightSource(
                FloorToScreen(spot.Tile, mapFile.Height, camera),
                (int)MathF.Round(spot.Tile.X),
                (int)MathF.Round(spot.Tile.Y),
                default,
                SpotlightMasks.Get(spot.Size),
                SpotlightTileOffsets(spot.Size),
                strength);
        }
    }

    /// <summary>A fractional tile's floor centre in viewport pixels (same formula as <c>Camera.TileToWorld</c> plus half a tile).</summary>
    public static Vector2 FloorToScreen(Vector2 tile, int mapHeight, Camera camera)
    {
        var worldX = ((mapHeight - 1 + tile.X - tile.Y) * DaLibConstants.HALF_TILE_WIDTH) + DaLibConstants.HALF_TILE_WIDTH;
        var worldY = ((tile.X + tile.Y) * DaLibConstants.HALF_TILE_HEIGHT) + DaLibConstants.HALF_TILE_HEIGHT;

        return camera.WorldToScreen(new Vector2(worldX, worldY));
    }

    /// <summary>Tiles a spotlight reveals on the Tab map: radius 1, 2 or 3 for Small, Medium, Large.</summary>
    public static (int Dx, int Dy)[] SpotlightTileOffsets(StageLightSize size)
        => size switch
        {
            StageLightSize.Small => Euclidean1,
            StageLightSize.Large => Euclidean3,
            _                    => Euclidean2
        };

    /// <summary>
    ///     Returns the tile-space offset array for a given lantern size and direction. Lanterns are
    ///     circular so direction is currently ignored, but the parameter is wired through for future
    ///     direction-aware shapes (e.g., cones).
    /// </summary>
    public (int Dx, int Dy)[] GetTileOffsets(LanternSize size, Direction direction)
        => size switch
        {
            LanternSize.Small => Euclidean3,
            LanternSize.Large => Euclidean5,
            _                 => []
        };

    //half-step bulge: a tile counts if its center is within radius + 0.5.
    //all-integer rearrangement of √(dx² + dy²) < radius + 0.5 → 4*(dx² + dy²) < (2*radius + 1)².
    //the threshold happens to equal the bounding-box area, so one value serves both the
    //stackalloc size and the inclusion test.
    private static (int Dx, int Dy)[] ComputeEuclidean(int radius)
    {
        var diameterSquared = (2 * radius + 1) * (2 * radius + 1);
        Span<(int Dx, int Dy)> buffer = stackalloc (int, int)[diameterSquared];
        var count = 0;

        for (var dy = -radius; dy <= radius; dy++)
            for (var dx = -radius; dx <= radius; dx++)
                if ((4 * (dx * dx + dy * dy)) < diameterSquared)
                    buffer[count++] = (dx, dy);

        return buffer[..count].ToArray();
    }
}