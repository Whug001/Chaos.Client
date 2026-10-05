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
    public const int MAX_FILE_BYTES = 20 * 1024 * 1024;

    public static (int Width, int Height) FitSize(int width, int height)
    {
        if ((width <= CollegeProtocol.MAX_PICTURE_WIDTH) && (height <= CollegeProtocol.MAX_PICTURE_HEIGHT))
            return (width, height);

        var scale = Math.Min((double)CollegeProtocol.MAX_PICTURE_WIDTH / width, (double)CollegeProtocol.MAX_PICTURE_HEIGHT / height);

        return (Math.Max(1, (int)Math.Floor(width * scale)), Math.Max(1, (int)Math.Floor(height * scale)));
    }

    public static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    public static PreparedPicture? Prepare(byte[] file, out string error)
    {
        error = string.Empty;

        if (file.Length > MAX_FILE_BYTES)
        {
            error = FILE_TOO_BIG;

            return null;
        }

        using var codec = SKCodec.Create(new MemoryStream(file));
        using var source = codec is null ? null : SKBitmap.Decode(codec);

        if (source is null)
        {
            error = NOT_A_PICTURE;

            return null;
        }

        var (width, height) = FitSize(source.Width, source.Height);
        using var sized = (width == source.Width) && (height == source.Height)
            ? source.Copy()
            : source.Resize(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul), new SKSamplingOptions(SKCubicResampler.Mitchell));

        if (sized is null)
        {
            error = NOT_A_PICTURE;

            return null;
        }

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

    private static bool HasTransparency(SKBitmap bitmap)
    {
        foreach (var pixel in bitmap.Pixels)
            if (pixel.Alpha < 255)
                return true;

        return false;
    }
}
