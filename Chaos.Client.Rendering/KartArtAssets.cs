#region
using System.Buffers.Binary;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Rendering;

public sealed record KartArtSheet(string File, int FrameWidth, int FrameHeight, int Frames, int Columns, int AnchorX, int AnchorY)
{
    public Rectangle Frame(int index)
    {
        if (index < 0 || index >= Frames)
            throw new ArgumentOutOfRangeException(nameof(index));

        return new Rectangle(index % Columns * FrameWidth, index / Columns * FrameHeight, FrameWidth, FrameHeight);
    }
}

public static class KartArtAssets
{
    private static readonly Lazy<IReadOnlyDictionary<string, KartArtSheet>> Manifest = new(LoadManifest);
    private static readonly Dictionary<(GraphicsDevice Device, string Key), Texture2D> Textures = [];

    public static IReadOnlyDictionary<string, KartArtSheet> ReadManifest() => Manifest.Value;

    public static bool HasSheet(string key) => ReadManifest().ContainsKey(key);

    public static void Validate(KartArtSheet sheet, int width, int height)
    {
        if (sheet.FrameWidth <= 0 || sheet.FrameHeight <= 0 || sheet.Frames <= 0 || sheet.Columns <= 0
            || sheet.Columns > sheet.Frames || sheet.AnchorX < 0 || sheet.AnchorX >= sheet.FrameWidth
            || sheet.AnchorY < 0 || sheet.AnchorY >= sheet.FrameHeight
            || width != (long)sheet.FrameWidth * sheet.Columns
            || height != (long)sheet.FrameHeight * ((sheet.Frames + (long)sheet.Columns - 1) / sheet.Columns))
            throw new InvalidDataException($"Invalid kart art geometry for {sheet.File}.");
    }

    public static Stream OpenAsset(string key)
    {
        var sheet = ReadManifest()[key];
        var stream = OpenResource(sheet.File);

        try
        {
            Span<byte> header = stackalloc byte[24];
            stream.ReadExactly(header);

            if (!header[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
                throw new InvalidDataException($"Kart art {sheet.File} is not a PNG.");

            Validate(sheet, BinaryPrimitives.ReadInt32BigEndian(header[16..20]), BinaryPrimitives.ReadInt32BigEndian(header[20..24]));
            stream.Position = 0;

            return stream;
        }
        catch
        {
            stream.Dispose();

            throw;
        }
    }

    public static Texture2D Texture(GraphicsDevice device, string key)
    {
        if (Textures.TryGetValue((device, key), out var texture))
            return texture;

        using var stream = OpenAsset(key);
        texture = Texture2D.FromStream(device, stream, DefaultColorProcessors.PremultiplyAlpha);
        Textures[(device, key)] = texture;

        return texture;
    }

    private static Stream OpenResource(string file)
        => typeof(KartArtAssets).Assembly.GetManifestResourceStream($"kartracing.{file}")
           ?? throw new InvalidDataException($"Required kart art resource {file} is missing.");

    private static IReadOnlyDictionary<string, KartArtSheet> LoadManifest()
    {
        using var stream = OpenResource("manifest.json");

        return JsonSerializer.Deserialize<Dictionary<string, KartArtSheet>>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
               ?? throw new InvalidDataException("Kart art manifest is empty.");
    }
}
