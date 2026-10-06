using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework.Graphics;

namespace Chaos.Client.Systems.College;

/// <summary>
///     College pictures by hash: uploads (check, then parts), downloads (fetch, then parts, then the disk cache) and the
///     textures made from them. Used on the game thread only.
/// </summary>
/// <remarks>
///     The server drops fetches over its per-minute limit without a reply, so <see cref="Update" /> sends an unanswered
///     fetch again every <see cref="FetchRetry.RETRY_AFTER" />, up to <see cref="FetchRetry.MAX_ATTEMPTS" /> sends. At most
///     <see cref="KEPT_TEXTURES" /> textures are kept besides those a window holds (<see cref="Hold" />); their bytes stay
///     in the disk cache, so a dropped texture is only decoded again.
/// </remarks>
public sealed class CollegePictureTransfers
{
    public const int KEPT_TEXTURES = 40;

    private readonly CollegePictureCache Cache;
    private readonly Func<DateTime> Clock;
    private readonly Func<byte[], Texture2D> Decode;
    private readonly Dictionary<string, PendingDownload> Downloads = new(StringComparer.Ordinal);
    private readonly HashSet<string> FailedHashes = new(StringComparer.Ordinal);
    private readonly Action<CollegeActionArgs> Send;
    private readonly HeldCache<Texture2D> Textures = new(KEPT_TEXTURES, texture => texture.Dispose());
    private readonly Dictionary<string, byte[]> Uploads = new(StringComparer.Ordinal);

    public CollegePictureTransfers(
        Action<CollegeActionArgs> send,
        CollegePictureCache cache,
        Func<byte[], Texture2D>? decode = null,
        Func<DateTime>? clock = null)
    {
        Send = send;
        Cache = cache;
        Decode = decode ?? DecodeOnDevice;
        Clock = clock ?? (() => DateTime.UtcNow);
    }

    /// <summary>A picture's texture is ready.</summary>
    public event Action<string>? PictureReady;

    /// <summary>A picture won't come this session: it doesn't decode, or the server never sent it.</summary>
    public event Action<string>? PictureFailed;

    /// <summary>An upload finished: the hash, whether it worked, and the server's message.</summary>
    public event Action<string, bool, string>? UploadFinished;

    public bool TryGetTexture(string hash, out Texture2D texture)
    {
        if (Textures.TryGet(hash, out texture))
            return true;

        if (FailedHashes.Contains(hash) || Downloads.ContainsKey(hash))
            return false;

        if (Cache.TryRead(hash) is { } bytes)
        {
            if (MakeTexture(hash, bytes))
                return Textures.TryGet(hash, out texture);

            //the bytes matched their hash when they were cached, so fetching them again would only fail the same way
            Fail(hash);

            return false;
        }

        Downloads[hash] = new PendingDownload(new PictureAssembly(1), Clock());
        SendFetch(hash);

        return false;
    }

    /// <summary>True when the picture won't come this session.</summary>
    public bool HasFailed(string hash) => FailedHashes.Contains(hash);

    /// <summary>Keeps a picture's texture while a window shows it; each Hold needs a <see cref="Release" />.</summary>
    public void Hold(string hash) => Textures.Hold(hash);

    public void Release(string hash) => Textures.Release(hash);

    /// <summary>Sends unanswered fetches again, and gives up on those whose last send went unanswered. Called every frame.</summary>
    public void Update()
    {
        if (Downloads.Count == 0)
            return;

        var now = Clock();
        List<string>? gaveUp = null;

        foreach (var (hash, pending) in Downloads)
            if (FetchRetry.ShouldResend(pending.SentAt, pending.Attempts, now))
            {
                pending.SentAt = now;
                pending.Attempts++;
                SendFetch(hash);
            } else if (FetchRetry.HasGivenUp(pending.SentAt, pending.Attempts, now))
                (gaveUp ??= []).Add(hash);

        if (gaveUp is null)
            return;

        foreach (var hash in gaveUp)
        {
            Downloads.Remove(hash);
            Fail(hash);
        }
    }

    /// <summary>A picture's file bytes, if this client has them (its own upload or the disk cache).</summary>
    public byte[]? BytesOf(string hash) => Uploads.GetValueOrDefault(hash) ?? Cache.TryRead(hash);

    public void Upload(PreparedPicture picture)
    {
        Cache.Write(picture.Hash, picture.Bytes);
        MakeTexture(picture.Hash, picture.Bytes);
        Uploads[picture.Hash] = picture.Bytes;
        Send(new CollegeActionArgs { Type = CollegeActionType.PictureCheck, Hash = picture.Hash, Length = (uint)picture.Bytes.Length });
    }

    public void OnReply(CollegeDisplayArgs args)
    {
        switch (args.Reply)
        {
            case CollegePictureReply.Send when Uploads.TryGetValue(args.Hash, out var bytes):
                var parts = bytes.Chunk(CollegeProtocol.PART_SIZE).ToList();

                for (var i = 0; i < parts.Count; i++)
                    Send(
                        new CollegeActionArgs
                        {
                            Type = CollegeActionType.PicturePart,
                            Hash = args.Hash,
                            PartIndex = (byte)i,
                            PartCount = (byte)parts.Count,
                            Data = parts[i]
                        });

                break;
            case CollegePictureReply.Have:
            case CollegePictureReply.Accepted:
                Uploads.Remove(args.Hash);
                UploadFinished?.Invoke(args.Hash, true, args.Message);

                break;
            case CollegePictureReply.Refused:
                Uploads.Remove(args.Hash);
                UploadFinished?.Invoke(args.Hash, false, args.Message);

                break;
        }
    }

    public void OnPart(CollegeDisplayArgs args)
    {
        if (!Downloads.TryGetValue(args.Hash, out var pending))
            return;

        var assembly = pending.Assembly;

        //the first part tells how many parts there are
        if (args.PartIndex == 0 || assembly.Failed)
            pending.Assembly = assembly = new PictureAssembly(args.PartCount);

        //parts still arriving are an answer, so the retry clock waits for the rest
        pending.SentAt = Clock();

        if (assembly.Add(args.PartIndex, args.PartCount, args.Data) is not { } bytes)
            return;

        Downloads.Remove(args.Hash);

        if (PicturePrep.Hash(bytes) != args.Hash)
        {
            FailedHashes.Add(args.Hash);
            PictureFailed?.Invoke(args.Hash);

            return;
        }

        Cache.Write(args.Hash, bytes);

        if (MakeTexture(args.Hash, bytes))
            PictureReady?.Invoke(args.Hash);
        else
            Fail(args.Hash);
    }

    public void Clear()
    {
        Textures.Clear();
        Downloads.Clear();
        FailedHashes.Clear();
        Uploads.Clear();
    }

    private void SendFetch(string hash) => Send(new CollegeActionArgs { Type = CollegeActionType.PictureFetch, Hash = hash });

    //a picture whose bytes match its hash but don't decode (a valid header over garbage) is never fetched again this
    //session, and its cached copy goes, so it can't put the viewer into a fetch loop
    private void Fail(string hash)
    {
        FailedHashes.Add(hash);
        Cache.Delete(hash);
        PictureFailed?.Invoke(hash);
    }

    private bool MakeTexture(string hash, byte[] bytes)
    {
        if (Textures.Contains(hash))
            return true;

        try
        {
            Textures.Add(hash, Decode(bytes));

            return true;
        } catch (Exception e) when (e is not OutOfMemoryException)
        {
            //StbImageSharp, under Texture2D.FromStream, throws more than InvalidOperationException on damaged data
            //(ArgumentException, IndexOutOfRangeException, ...), and none of them may reach the game loop
            return false;
        }
    }

    private static Texture2D DecodeOnDevice(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);

        return Texture2D.FromStream(ChaosGame.Device, stream);
    }
}

internal sealed class PendingDownload(PictureAssembly assembly, DateTime sentAt)
{
    public PictureAssembly Assembly { get; set; } = assembly;
    public int Attempts { get; set; } = 1;
    public DateTime SentAt { get; set; } = sentAt;
}

/// <summary>
///     When a fetch that got no reply is sent again: every <see cref="RETRY_AFTER" /> (the server's fetch limits count per
///     minute), up to <see cref="MAX_ATTEMPTS" /> sends in all, so a picture the server no longer has isn't asked for forever.
/// </summary>
public static class FetchRetry
{
    public const int MAX_ATTEMPTS = 10;
    public static readonly TimeSpan RETRY_AFTER = TimeSpan.FromSeconds(30);

    public static bool ShouldResend(DateTime sentAt, int attempts, DateTime now) => (attempts < MAX_ATTEMPTS) && ((now - sentAt) >= RETRY_AFTER);

    /// <summary>The last send went unanswered too.</summary>
    public static bool HasGivenUp(DateTime sentAt, int attempts, DateTime now) => (attempts >= MAX_ATTEMPTS) && ((now - sentAt) >= RETRY_AFTER);
}
