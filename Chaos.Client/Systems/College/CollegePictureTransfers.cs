using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework.Graphics;

namespace Chaos.Client.Systems.College;

/// <summary>
///     College pictures by hash: uploads (check, then parts), downloads (fetch, then parts, then the disk cache) and the
///     textures made from them. Used on the game thread only.
/// </summary>
public sealed class CollegePictureTransfers(
    Action<CollegeActionArgs> send,
    CollegePictureCache cache,
    Func<byte[], Texture2D>? decode = null)
{
    private readonly Func<byte[], Texture2D> Decode = decode ?? DecodeOnDevice;

    private readonly Dictionary<string, PendingDownload> Downloads = new(StringComparer.Ordinal);
    private readonly HashSet<string> FailedHashes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Texture2D> Textures = new(StringComparer.Ordinal);
    private readonly Dictionary<string, byte[]> Uploads = new(StringComparer.Ordinal);

    /// <summary>A picture's texture is ready.</summary>
    public event Action<string>? PictureReady;

    /// <summary>An upload finished: the hash, whether it worked, and the server's message.</summary>
    public event Action<string, bool, string>? UploadFinished;

    public bool TryGetTexture(string hash, out Texture2D texture)
    {
        if (Textures.TryGetValue(hash, out texture!))
            return true;

        texture = null!;

        if (FailedHashes.Contains(hash))
            return false;

        if (Downloads.TryGetValue(hash, out var pending))
        {
            if (FetchRetry.ShouldResend(pending.SentAt, pending.Resent, DateTime.UtcNow))
            {
                pending.SentAt = DateTime.UtcNow;
                pending.Resent = true;
                send(new CollegeActionArgs { Type = CollegeActionType.PictureFetch, Hash = hash });
            }

            return false;
        }

        if (cache.TryRead(hash) is { } bytes)
        {
            if (MakeTexture(hash, bytes))
                return Textures.TryGetValue(hash, out texture!);

            //the bytes matched their hash when they were cached, so fetching them again would only fail the same way
            Fail(hash);

            return false;
        }

        Downloads[hash] = new PendingDownload(new PictureAssembly(1), DateTime.UtcNow);
        send(new CollegeActionArgs { Type = CollegeActionType.PictureFetch, Hash = hash });

        return false;
    }

    /// <summary>A picture's file bytes, if this client has them (its own upload or the disk cache).</summary>
    public byte[]? BytesOf(string hash) => Uploads.GetValueOrDefault(hash) ?? cache.TryRead(hash);

    public void Upload(PreparedPicture picture)
    {
        cache.Write(picture.Hash, picture.Bytes);
        MakeTexture(picture.Hash, picture.Bytes);
        Uploads[picture.Hash] = picture.Bytes;
        send(new CollegeActionArgs { Type = CollegeActionType.PictureCheck, Hash = picture.Hash, Length = (uint)picture.Bytes.Length });
    }

    public void OnReply(CollegeDisplayArgs args)
    {
        switch (args.Reply)
        {
            case CollegePictureReply.Send when Uploads.TryGetValue(args.Hash, out var bytes):
                var parts = bytes.Chunk(CollegeProtocol.PART_SIZE).ToList();

                for (var i = 0; i < parts.Count; i++)
                    send(
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

        if (assembly.Add(args.PartIndex, args.PartCount, args.Data) is not { } bytes)
            return;

        Downloads.Remove(args.Hash);

        if (PicturePrep.Hash(bytes) != args.Hash)
        {
            FailedHashes.Add(args.Hash);

            return;
        }

        cache.Write(args.Hash, bytes);

        if (MakeTexture(args.Hash, bytes))
            PictureReady?.Invoke(args.Hash);
        else
            Fail(args.Hash);
    }

    public void Clear()
    {
        foreach (var texture in Textures.Values)
            texture.Dispose();

        Textures.Clear();
        Downloads.Clear();
        FailedHashes.Clear();
        Uploads.Clear();
    }

    //a picture whose bytes match its hash but don't decode (a valid header over garbage) is never fetched again this
    //session, and its cached copy goes, so it can't put the viewer into a fetch loop
    private void Fail(string hash)
    {
        FailedHashes.Add(hash);
        cache.Delete(hash);
    }

    private bool MakeTexture(string hash, byte[] bytes)
    {
        if (Textures.ContainsKey(hash))
            return true;

        try
        {
            Textures[hash] = Decode(bytes);

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
    public bool Resent { get; set; }
    public DateTime SentAt { get; set; } = sentAt;
}

/// <summary>When a fetch that got no reply may be sent again: once, after <see cref="RETRY_AFTER" />.</summary>
public static class FetchRetry
{
    public static readonly TimeSpan RETRY_AFTER = TimeSpan.FromSeconds(30);

    public static bool ShouldResend(DateTime sentAt, bool alreadyResent, DateTime now) => !alreadyResent && ((now - sentAt) >= RETRY_AFTER);
}
