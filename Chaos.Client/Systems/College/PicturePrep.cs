using System.Security.Cryptography;
using Chaos.DarkAges.Definitions;
using SkiaSharp;

namespace Chaos.Client.Systems.College;

public sealed record PreparedPicture(byte[] Bytes, string Hash, int Width, int Height);

/// <summary>Turns a picture file into what the College accepts: at most 400 x 300 and 200 KB, PNG if it has transparency, JPEG otherwise.</summary>
public static class PicturePrep
{
    public const string NOT_A_PICTURE = "That file isn't a PNG or JPG picture.";
    public const string TOO_LARGE = "That picture is too large.";
    public const string FILE_TOO_BIG = "That file is too big to open.";
    public const string TOO_MANY_PIXELS = "That picture is too big to open. Try a smaller copy of it.";
    public const int MAX_FILE_BYTES = 20 * 1024 * 1024;

    /// <summary>The most pixels decoded at once (160 MB as RGBA). A JPEG decodes scaled down, so only huge PNGs reach it.</summary>
    public const long MAX_DECODE_PIXELS = 40_000_000;

    public static (int Width, int Height) FitSize(int width, int height)
    {
        if ((width <= CollegeProtocol.MAX_PICTURE_WIDTH) && (height <= CollegeProtocol.MAX_PICTURE_HEIGHT))
            return (width, height);

        var scale = Math.Min((double)CollegeProtocol.MAX_PICTURE_WIDTH / width, (double)CollegeProtocol.MAX_PICTURE_HEIGHT / height);

        return (Math.Max(1, (int)Math.Floor(width * scale)), Math.Max(1, (int)Math.Floor(height * scale)));
    }

    public static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    public static PreparedPicture? Prepare(byte[] file, out string error) => Prepare(file, MAX_DECODE_PIXELS, out error);

    public static PreparedPicture? Prepare(byte[] file, long maxDecodePixels, out string error)
    {
        error = string.Empty;

        if (file.Length > MAX_FILE_BYTES)
        {
            error = FILE_TOO_BIG;

            return null;
        }

        using var codec = SKCodec.Create(new MemoryStream(file));

        if (codec is null || (codec.Info.Width <= 0) || (codec.Info.Height <= 0))
        {
            error = NOT_A_PICTURE;

            return null;
        }

        //a phone stores a photo as the sensor saw it and says in EXIF how to turn it, so the box is fitted to the turned size
        var origin = codec.EncodedOrigin;
        var turned = IsQuarterTurn(origin);
        var (storedWidth, storedHeight) = (codec.Info.Width, codec.Info.Height);
        var (width, height) = turned ? FitSize(storedHeight, storedWidth) : FitSize(storedWidth, storedHeight);
        var (fitWidth, fitHeight) = turned ? (height, width) : (width, height);
        var decodeSize = DecodeSize(codec, fitWidth, fitHeight);

        if ((long)decodeSize.Width * decodeSize.Height > maxDecodePixels)
        {
            error = TOO_MANY_PIXELS;

            return null;
        }

        var decodeInfo = new SKImageInfo(decodeSize.Width, decodeSize.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var source = new SKBitmap(decodeInfo);

        if (codec.GetPixels(decodeInfo, source.GetPixels()) is not (SKCodecResult.Success or SKCodecResult.IncompleteInput))
        {
            error = NOT_A_PICTURE;

            return null;
        }

        using var stored = (fitWidth == source.Width) && (fitHeight == source.Height)
            ? source.Copy()
            : source.Resize(new SKImageInfo(fitWidth, fitHeight, SKColorType.Rgba8888, SKAlphaType.Premul), new SKSamplingOptions(SKCubicResampler.Mitchell));

        if (stored is null)
        {
            error = NOT_A_PICTURE;

            return null;
        }

        using var sized = Upright(stored, origin);
        using var image = SKImage.FromBitmap(sized);

        byte[]? bytes;

        if (HasTransparency(sized))
        {
            using var png = image.Encode(SKEncodedImageFormat.Png, 100);
            bytes = png.ToArray();
        } else
        {
            bytes = null;

            for (var quality = 85; quality >= 60; quality -= 5)
            {
                using var jpeg = image.Encode(SKEncodedImageFormat.Jpeg, quality);
                bytes = jpeg.ToArray();

                if (bytes.Length <= CollegeProtocol.MAX_PICTURE_BYTES)
                    break;
            }
        }

        if (bytes is null || (bytes.Length > CollegeProtocol.MAX_PICTURE_BYTES))
        {
            error = TOO_LARGE;

            return null;
        }

        return new PreparedPicture(bytes, Hash(bytes), width, height);
    }

    /// <summary>
    ///     The smallest size the codec can decode to that still covers the fitted size: a JPEG scales by eighths as it
    ///     decodes, so a large photo never has to be held at full size; other formats decode at full size.
    /// </summary>
    private static SKSizeI DecodeSize(SKCodec codec, int fitWidth, int fitHeight)
    {
        for (var eighths = 1; eighths < 8; eighths++)
        {
            var size = codec.GetScaledDimensions(eighths / 8f);

            if ((size.Width >= fitWidth) && (size.Height >= fitHeight))
                return size;
        }

        return new SKSizeI(codec.Info.Width, codec.Info.Height);
    }

    private static bool IsQuarterTurn(SKEncodedOrigin origin)
        => origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;

    /// <summary>A copy of <paramref name="stored" /> turned and flipped the way its EXIF origin says to show it.</summary>
    private static SKBitmap Upright(SKBitmap stored, SKEncodedOrigin origin)
    {
        if (origin is SKEncodedOrigin.TopLeft)
            return stored.Copy();

        int w = stored.Width, h = stored.Height;
        var turned = IsQuarterTurn(origin);
        var upright = new SKBitmap(new SKImageInfo(turned ? h : w, turned ? w : h, SKColorType.Rgba8888, SKAlphaType.Premul));

        for (var y = 0; y < upright.Height; y++)
            for (var x = 0; x < upright.Width; x++)
            {
                var (sx, sy) = origin switch
                {
                    SKEncodedOrigin.TopRight    => (w - 1 - x, y),
                    SKEncodedOrigin.BottomRight => (w - 1 - x, h - 1 - y),
                    SKEncodedOrigin.BottomLeft  => (x, h - 1 - y),
                    SKEncodedOrigin.LeftTop     => (y, x),
                    SKEncodedOrigin.RightTop    => (y, h - 1 - x),
                    SKEncodedOrigin.RightBottom => (w - 1 - y, h - 1 - x),
                    SKEncodedOrigin.LeftBottom  => (w - 1 - y, x),
                    _                           => (x, y)
                };

                upright.SetPixel(x, y, stored.GetPixel(sx, sy));
            }

        return upright;
    }

    private static bool HasTransparency(SKBitmap bitmap)
    {
        foreach (var pixel in bitmap.Pixels)
            if (pixel.Alpha < 255)
                return true;

        return false;
    }
}
