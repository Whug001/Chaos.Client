using Chaos.Client.Systems.College;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework.Graphics;

namespace Chaos.Client.Systems.Portraits;

/// <summary>
///     Painted Portrait pictures by hash: single-message fetches over the Plan 1 PortraitFetch, a disk cache in its own
///     PortraitPictures folder, and the textures made from them. Used on the game thread only.
/// </summary>
public sealed class PortraitPictureTransfers
{
    public const int KEPT_TEXTURES = 40;

    private readonly CollegePictureCache Cache;
    private readonly Func<byte[], Texture2D> Decode;
    private readonly HashSet<string> FailedHashes = new(StringComparer.Ordinal);
    private readonly HashSet<string> PendingHashes = new(StringComparer.Ordinal);
    private readonly Action<string> Send;
    private readonly HeldCache<Texture2D> Textures = new(KEPT_TEXTURES, texture => texture.Dispose());

    public PortraitPictureTransfers(Action<string> send, Func<byte[], Texture2D>? decode = null)
        : this(send, new CollegePictureCache(DefaultDirectory(), CollegePictureCache.DEFAULT_CAP), decode) { }

    public PortraitPictureTransfers(
        Action<string> send,
        CollegePictureCache cache,
        Func<byte[], Texture2D>? decode = null)
    {
        Send = send;
        Cache = cache;
        Decode = decode ?? DecodeOnDevice;
    }

    /// <summary>A picture's texture is ready.</summary>
    public event Action<string>? PictureReady;

    /// <summary>A picture won't come this session: the server has none, or it doesn't decode.</summary>
    public event Action<string>? PictureFailed;

    /// <summary>The disk folder for fetched portrait pictures.</summary>
    public static string DefaultDirectory()
        => Path.Combine(GlobalSettings.DataPath, "PortraitPictures");

    public bool TryGetTexture(string hash, out Texture2D texture)
    {
        if (Textures.TryGet(hash, out texture))
            return true;

        if (FailedHashes.Contains(hash) || PendingHashes.Contains(hash))
        {
            texture = null!;

            return false;
        }

        if (Cache.TryRead(hash) is { } bytes)
        {
            if (MakeTexture(hash, bytes))
                return Textures.TryGet(hash, out texture);

            //the bytes matched their hash when they were cached, so fetching them again would only fail the same way
            Fail(hash);
            texture = null!;

            return false;
        }

        PendingHashes.Add(hash);
        Send(hash);
        texture = null!;

        return false;
    }

    /// <summary>True when the picture won't come this session.</summary>
    public bool HasFailed(string hash) => FailedHashes.Contains(hash);

    /// <summary>Keeps a picture's texture while a tooltip shows it; each Hold needs a <see cref="Release" />.</summary>
    public void Hold(string hash) => Textures.Hold(hash);

    public void Release(string hash) => Textures.Release(hash);

    public void OnPicture(PortraitPictureArgs args)
    {
        if (!PendingHashes.Contains(args.Hash))
            return;

        PendingHashes.Remove(args.Hash);

        //the server echoes the hash with empty bytes when it has nothing to send; that is the blank frame
        if (args.Picture is not { Length: > 0 })
        {
            Fail(args.Hash);

            return;
        }

        if (PicturePrep.Hash(args.Picture) != args.Hash)
        {
            Fail(args.Hash);

            return;
        }

        Cache.Write(args.Hash, args.Picture);

        if (MakeTexture(args.Hash, args.Picture))
            PictureReady?.Invoke(args.Hash);
        else
            Fail(args.Hash);
    }

    public void Clear()
    {
        Textures.Clear();
        PendingHashes.Clear();
        FailedHashes.Clear();
    }

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
            return false;
        }
    }

    private static Texture2D DecodeOnDevice(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);

        return Texture2D.FromStream(ChaosGame.Device, stream);
    }
}
