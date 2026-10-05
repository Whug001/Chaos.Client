using Chaos.Client.Systems.College;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
using FluentAssertions;
using Microsoft.Xna.Framework.Graphics;

namespace Chaos.Client.Tests.College;

public class CollegePictureTransfersTests
{
    private static readonly byte[] Garbage = [0x89, 0x50, 0x4E, 0x47, 1, 2, 3, 4];

    private static Texture2D Undecodable(byte[] bytes) => throw new ArgumentException("damaged picture data");

    private static CollegePictureCache Cache()
        => new(Path.Combine(Path.GetTempPath(), "college-transfers-" + Guid.NewGuid().ToString("N")), CollegePictureCache.DEFAULT_CAP);

    [Test]
    public void A_downloaded_picture_that_does_not_decode_is_not_fetched_again()
    {
        var sent = new List<CollegeActionArgs>();
        var cache = Cache();
        var transfers = new CollegePictureTransfers(sent.Add, cache, Undecodable);
        var hash = PicturePrep.Hash(Garbage);

        transfers.TryGetTexture(hash, out _).Should().BeFalse();
        sent.Should().ContainSingle().Which.Type.Should().Be(CollegeActionType.PictureFetch);

        transfers.OnPart(
            new CollegeDisplayArgs
            {
                Type = CollegeDisplayType.PicturePart,
                Hash = hash,
                PartIndex = 0,
                PartCount = 1,
                Data = Garbage
            });

        transfers.TryGetTexture(hash, out _).Should().BeFalse();
        transfers.TryGetTexture(hash, out _).Should().BeFalse();
        sent.Should().ContainSingle();
        cache.TryRead(hash).Should().BeNull();
    }

    [Test]
    public void A_cached_picture_that_does_not_decode_is_dropped_and_not_fetched()
    {
        var sent = new List<CollegeActionArgs>();
        var cache = Cache();
        var hash = PicturePrep.Hash(Garbage);
        cache.Write(hash, Garbage);
        var transfers = new CollegePictureTransfers(sent.Add, cache, Undecodable);

        transfers.TryGetTexture(hash, out _).Should().BeFalse();
        transfers.TryGetTexture(hash, out _).Should().BeFalse();

        sent.Should().BeEmpty();
        cache.TryRead(hash).Should().BeNull();
    }
}
