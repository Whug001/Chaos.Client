using Chaos.Client.Rendering;
using FluentAssertions;

namespace Chaos.Client.Tests.BrassRing;

public sealed class BrassRingArtAssetsTests
{
    [Test]
    public void Manifest_lists_every_sheet()
        => BrassRingArtAssets.ReadManifest().Keys.Should().BeEquivalentTo("arena", "ropes", "glove", "spark", "stars", "white", "keys", "hud", "keybar", "plaque", "pip");

    [Test]
    public void Every_sheet_opens_and_passes_its_size_check()
    {
        foreach (var key in BrassRingArtAssets.ReadManifest().Keys)
        {
            using var stream = BrassRingArtAssets.OpenAsset(key);
            stream.Length.Should().BeGreaterThan(0);
        }
    }
}
