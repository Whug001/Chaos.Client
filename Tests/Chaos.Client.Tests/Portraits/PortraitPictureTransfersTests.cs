using Chaos.Client.Systems.College;
using Chaos.Client.Systems.Portraits;
using Chaos.Networking.Entities.Server;
using FluentAssertions;
using Microsoft.Xna.Framework.Graphics;
using SkiaSharp;

namespace Chaos.Client.Tests.Portraits;

public sealed class PortraitPictureTransfersTests
{
    private static readonly byte[] Garbage = [0x89, 0x50, 0x4E, 0x47, 1, 2, 3, 4];

    private static Texture2D FakeTexture(byte[] bytes) => null!;

    private static Texture2D Undecodable(byte[] bytes) => throw new ArgumentException("damaged picture data");

    private static byte[] Png48()
    {
        using var bitmap = new SKBitmap(48, 56);
        bitmap.Erase(SKColors.White);
        using var image = SKImage.FromBitmap(bitmap);
        using var png = image.Encode(SKEncodedImageFormat.Png, 100);

        return png.ToArray();
    }

    private static CollegePictureCache Cache()
        => new(Path.Combine(Path.GetTempPath(), "portrait-transfers-" + Guid.NewGuid().ToString("N")), CollegePictureCache.DEFAULT_CAP);

    private static string TempDir()
        => Path.Combine(Path.GetTempPath(), "portrait-transfers-" + Guid.NewGuid().ToString("N"));

    [Test]
    public void A_cached_picture_needs_no_fetch()
    {
        var bytes = Png48();
        var hash = PicturePrep.Hash(bytes);
        var cache = new CollegePictureCache(TempDir(), 1_000_000);
        cache.Write(hash, bytes);
        var sent = new List<string>();
        var transfers = new PortraitPictureTransfers(h => sent.Add(h), cache, FakeTexture);
        transfers.TryGetTexture(hash, out _).Should().BeTrue();
        sent.Should().BeEmpty();
    }

    [Test]
    public void A_missing_picture_sends_one_fetch()
    {
        var sent = new List<string>();
        var transfers = new PortraitPictureTransfers(sent.Add, Cache(), FakeTexture);
        var hash = PicturePrep.Hash(Garbage);

        transfers.TryGetTexture(hash, out _).Should().BeFalse();
        transfers.TryGetTexture(hash, out _).Should().BeFalse();

        sent.Should().Equal(hash);
    }

    [Test]
    public void An_arriving_picture_is_cached_and_announced()
    {
        var ready = new List<string>();
        var cache = Cache();
        var transfers = new PortraitPictureTransfers(_ => { }, cache, FakeTexture);
        transfers.PictureReady += ready.Add;
        var bytes = Png48();
        var hash = PicturePrep.Hash(bytes);

        transfers.TryGetTexture(hash, out _).Should().BeFalse();

        transfers.OnPicture(new PortraitPictureArgs { IsOwn = false, Hash = hash, Picture = bytes });

        ready.Should().Equal(hash);
        transfers.TryGetTexture(hash, out _).Should().BeTrue();
        cache.TryRead(hash).Should().Equal(bytes);
    }

    [Test]
    public void An_empty_picture_marks_failed()
    {
        var failed = new List<string>();
        var sent = new List<string>();
        var transfers = new PortraitPictureTransfers(sent.Add, Cache(), FakeTexture);
        transfers.PictureFailed += failed.Add;
        var hash = PicturePrep.Hash(Garbage);

        transfers.TryGetTexture(hash, out _).Should().BeFalse();
        transfers.OnPicture(new PortraitPictureArgs { IsOwn = false, Hash = hash, Picture = [] });

        failed.Should().Equal(hash);
        transfers.HasFailed(hash).Should().BeTrue();
        transfers.TryGetTexture(hash, out _).Should().BeFalse();
        sent.Should().HaveCount(1);
    }

    [Test]
    public void A_picture_with_the_wrong_hash_marks_failed()
    {
        var failed = new List<string>();
        var cache = Cache();
        var transfers = new PortraitPictureTransfers(_ => { }, cache, FakeTexture);
        transfers.PictureFailed += failed.Add;
        var bytes = Png48();
        var hash = PicturePrep.Hash(bytes);
        var other = PicturePrep.Hash(Garbage);

        transfers.TryGetTexture(hash, out _).Should().BeFalse();
        transfers.OnPicture(new PortraitPictureArgs { IsOwn = false, Hash = hash, Picture = bytes });

        transfers.TryGetTexture(other, out _).Should().BeFalse();
        transfers.OnPicture(new PortraitPictureArgs { IsOwn = false, Hash = other, Picture = bytes });

        failed.Should().Equal(other);
        transfers.HasFailed(other).Should().BeTrue();
        cache.TryRead(other).Should().BeNull();
    }

    [Test]
    public void A_cached_picture_that_does_not_decode_is_dropped_and_not_fetched()
    {
        var sent = new List<string>();
        var cache = Cache();
        var hash = PicturePrep.Hash(Garbage);
        cache.Write(hash, Garbage);
        var transfers = new PortraitPictureTransfers(sent.Add, cache, Undecodable);

        transfers.TryGetTexture(hash, out _).Should().BeFalse();
        transfers.TryGetTexture(hash, out _).Should().BeFalse();

        sent.Should().BeEmpty();
        cache.TryRead(hash).Should().BeNull();
    }

    [Test]
    public void A_downloaded_picture_that_does_not_decode_is_not_fetched_again()
    {
        var sent = new List<string>();
        var cache = Cache();
        var transfers = new PortraitPictureTransfers(sent.Add, cache, Undecodable);
        var bytes = Png48();
        var hash = PicturePrep.Hash(bytes);

        transfers.TryGetTexture(hash, out _).Should().BeFalse();
        sent.Should().HaveCount(1);

        transfers.OnPicture(new PortraitPictureArgs { IsOwn = false, Hash = hash, Picture = bytes });

        transfers.TryGetTexture(hash, out _).Should().BeFalse();
        sent.Should().HaveCount(1);
        cache.TryRead(hash).Should().BeNull();
    }
}
