using Chaos.Client.Systems.College;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
using FluentAssertions;
using Microsoft.Xna.Framework.Graphics;

namespace Chaos.Client.Tests.College;

public class CollegePictureTransfersTests
{
    private static readonly DateTime Start = new(2026, 10, 6, 12, 0, 0, DateTimeKind.Utc);
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

    [Test]
    public void An_unanswered_fetch_is_resent_every_thirty_seconds_without_being_asked_again()
    {
        var sent = new List<CollegeActionArgs>();
        var now = Start;
        var transfers = new CollegePictureTransfers(sent.Add, Cache(), Undecodable, () => now);
        var hash = PicturePrep.Hash(Garbage);

        transfers.TryGetTexture(hash, out _);

        now = Start.AddSeconds(29);
        transfers.Update();
        sent.Should().HaveCount(1);

        now = Start.AddSeconds(30);
        transfers.Update();
        sent.Should().HaveCount(2);

        now = Start.AddSeconds(59);
        transfers.Update();
        transfers.TryGetTexture(hash, out _);
        sent.Should().HaveCount(2);

        now = Start.AddSeconds(60);
        transfers.Update();
        sent.Should().HaveCount(3).And.OnlyContain(a => (a.Type == CollegeActionType.PictureFetch) && (a.Hash == hash));
    }

    [Test]
    public void A_fetch_that_never_gets_a_reply_gives_up_and_says_so()
    {
        var sent = new List<CollegeActionArgs>();
        var failed = new List<string>();
        var now = Start;
        var transfers = new CollegePictureTransfers(sent.Add, Cache(), Undecodable, () => now);
        transfers.PictureFailed += failed.Add;
        var hash = PicturePrep.Hash(Garbage);

        transfers.TryGetTexture(hash, out _);

        for (var second = 0; second <= 900; second++)
        {
            now = Start.AddSeconds(second);
            transfers.Update();
            transfers.TryGetTexture(hash, out _);
        }

        sent.Should().HaveCount(FetchRetry.MAX_ATTEMPTS);
        failed.Should().Equal(hash);
        transfers.HasFailed(hash).Should().BeTrue();
    }

    [Test]
    public void A_picture_that_arrives_after_a_retry_is_announced()
    {
        var sent = new List<CollegeActionArgs>();
        var ready = new List<string>();
        var now = Start;
        var transfers = new CollegePictureTransfers(sent.Add, Cache(), _ => null!, () => now);
        transfers.PictureReady += ready.Add;
        var hash = PicturePrep.Hash(Garbage);

        transfers.TryGetTexture(hash, out _);
        now = Start.AddSeconds(30);
        transfers.Update();

        transfers.OnPart(
            new CollegeDisplayArgs
            {
                Type = CollegeDisplayType.PicturePart,
                Hash = hash,
                PartIndex = 0,
                PartCount = 1,
                Data = Garbage
            });

        ready.Should().Equal(hash);
        transfers.TryGetTexture(hash, out _).Should().BeTrue();

        now = Start.AddMinutes(5);
        transfers.Update();
        sent.Should().HaveCount(2);
    }
}
