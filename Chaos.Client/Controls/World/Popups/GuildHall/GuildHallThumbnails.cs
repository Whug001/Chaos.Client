#region
using Chaos.Client.Data;
using Chaos.Client.Systems;
using Chaos.Client.ViewModel;
using Chaos.Networking.Entities.Server;
using Chaos.Geometry.Abstractions.Definitions;
using DALib.Drawing;
using Microsoft.Xna.Framework.Graphics;
using SkiaSharp;
#endregion

namespace Chaos.Client.Controls.World.Popups.GuildHall;

/// <summary>
///     Thumbnails for the hall editor picker. Floors are rendered like MapRenderer's background fallback; a foreground
///     pair draws the left half at x 0 and the right half one half-tile to the right, both bottom-aligned, the way
///     MapRenderer.DrawForegroundTile places them. NPC thumbnails are the creature renderer's own idle frames.
/// </summary>
public sealed class GuildHallThumbnails : IDisposable
{
    private readonly CreatureRenderer Creatures;
    private readonly Dictionary<ushort, Texture2D?> Floors = [];
    private readonly Dictionary<(ushort Left, ushort Right), Texture2D?> Foregrounds = [];
    private readonly Dictionary<string, Texture2D> PrefabCache = [];

    public GuildHallThumbnails(CreatureRenderer creatures) => Creatures = creatures;

    public void Dispose() => Clear();

    public Texture2D? For(GuildHallPick pick, IReadOnlyList<GuildHallNpcOption> npcs, IReadOnlyList<GuildHallPrefabInfo> prefabs)
    {
        if (pick.PrefabKey is { } prefabKey)
            return prefabs.FirstOrDefault(p => p.Key == prefabKey) is { } prefab ? Prefab(prefab) : null;

        if (pick.NpcKey is { } key)
            return npcs.FirstOrDefault(n => n.Key == key) is { } npc ? NpcFrame(npc.Sprite) : null;

        return pick.Floor != 0 ? Floor(pick.Floor) : Foreground(pick.Left, pick.Right);
    }

    public void Clear()
    {
        foreach (var texture in Floors.Values)
            texture?.Dispose();

        foreach (var texture in Foregrounds.Values)
            texture?.Dispose();

        Floors.Clear();
        Foregrounds.Clear();

        foreach (var texture in PrefabCache.Values)
            texture.Dispose();

        PrefabCache.Clear();
    }

    private Texture2D? Floor(ushort id)
    {
        if (Floors.TryGetValue(id, out var cached))
            return cached;

        Texture2D? texture = null;

        if (DataContext.Tiles.GetBackgroundTile(id) is { } palettized)
        {
            using var image = Graphics.RenderTile(palettized.Entity, palettized.Palette);
            texture = TextureConverter.ToTexture2D(image);
        }

        Floors[id] = texture;

        return texture;
    }

    private Texture2D? Foreground(ushort left, ushort right)
    {
        if (Foregrounds.TryGetValue((left, right), out var cached))
            return cached;

        using var leftImage = RenderForeground(left);
        using var rightImage = RenderForeground(right);

        var width = Math.Max(leftImage?.Width ?? 0, rightImage is null ? 0 : DaLibConstants.HALF_TILE_WIDTH + rightImage.Width);
        var height = Math.Max(leftImage?.Height ?? 0, rightImage?.Height ?? 0);
        Texture2D? texture = null;

        if ((width > 0) && (height > 0))
        {
            var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
            using var surface = SKSurface.Create(info);
            surface.Canvas.Clear(SKColors.Transparent);

            if (leftImage is not null)
                surface.Canvas.DrawImage(leftImage, 0, height - leftImage.Height);

            if (rightImage is not null)
                surface.Canvas.DrawImage(rightImage, DaLibConstants.HALF_TILE_WIDTH, height - rightImage.Height);

            using var snapshot = surface.Snapshot();
            texture = TextureConverter.ToTexture2D(snapshot);
        }

        Foregrounds[(left, right)] = texture;

        return texture;
    }

    private Texture2D? Prefab(GuildHallPrefabInfo prefab)
    {
        if (PrefabCache.TryGetValue(prefab.Key, out var cached))
            return cached;

        var parts = new List<(SKImage Image, int Left, int Bottom)>();

        for (var y = 0; y < prefab.Height; y++)
            for (var x = 0; x < prefab.Width; x++)
            {
                var index = y * prefab.Width + x;
                var px = (prefab.Height - 1 + x - y) * DaLibConstants.HALF_TILE_WIDTH;
                var bottom = (x + y) * DaLibConstants.HALF_TILE_HEIGHT + (2 * DaLibConstants.HALF_TILE_HEIGHT);

                if (prefab.Left[index] != 0 && RenderForeground(prefab.Left[index]) is { } left)
                    parts.Add((left, px, bottom));

                if (prefab.Right[index] != 0 && RenderForeground(prefab.Right[index]) is { } right)
                    parts.Add((right, px + DaLibConstants.HALF_TILE_WIDTH, bottom));
            }

        if (parts.Count == 0)
            return null;

        var top = parts.Min(p => p.Bottom - p.Image.Height);
        var width = parts.Max(p => p.Left + p.Image.Width);
        var height = parts.Max(p => p.Bottom) - top;
        Texture2D? texture = null;

        if ((width > 0) && (height > 0))
        {
            var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
            using var surface = SKSurface.Create(info);
            surface.Canvas.Clear(SKColors.Transparent);

            foreach (var part in parts)
                surface.Canvas.DrawImage(part.Image, part.Left, part.Bottom - part.Image.Height - top);

            using var snapshot = surface.Snapshot();
            texture = TextureConverter.ToTexture2D(snapshot);
        }

        foreach (var part in parts)
            part.Image.Dispose();

        if (texture is not null)
            PrefabCache[prefab.Key] = texture;

        return texture;
    }

    private static SKImage? RenderForeground(ushort id)
    {
        if (id == 0)
            return null;

        var palettized = DataContext.Tiles.GetForegroundTile(id);

        return palettized is null ? null : Graphics.RenderImage(palettized.Entity.Decompress(), palettized.Palette);
    }

    //owned by the creature renderer (cleared on map change), so never disposed here
    private Texture2D? NpcFrame(ushort sprite)
    {
        var (frameIndex, _) = Creatures.GetAnimInfo(sprite) is { } info ? AnimationSystem.GetCreatureIdleFrame(in info, Direction.Down) : (0, false);

        return Creatures.GetFrame(sprite, frameIndex)?.Texture;
    }
}
