using Chaos.Client.Models;
using Chaos.Geometry.Abstractions.Definitions;
using FluentAssertions;
using Microsoft.Xna.Framework;

namespace Chaos.Client.Tests;

public class WorldEntityMirrorCopyTests
{
    [Test]
    public async Task A_copy_has_its_own_cache_id_and_leaves_the_original_alone()
    {
        var original = new WorldEntity
        {
            Id = 42,
            TileX = 3,
            TileY = 4,
            Direction = Direction.Up,
            SpriteId = 179,
            VisualOffset = new Vector2(5, 6)
        };

        original.RenderCacheId.Should().Be(42u);

        var copy = original.CopyForMirror(0x8000_0151u);

        copy.Should().NotBeSameAs(original);
        copy.Id.Should().Be(42u);
        copy.RenderCacheId.Should().Be(0x8000_0151u);
        copy.TileX.Should().Be(3);
        copy.SpriteId.Should().Be((ushort)179);

        copy.Direction = Direction.Down;
        copy.VisualOffset = Vector2.Zero;

        original.Direction.Should().Be(Direction.Up);
        original.VisualOffset.Should().Be(new Vector2(5, 6));
        original.RenderCacheId.Should().Be(42u);
        await Task.CompletedTask;
    }
}
