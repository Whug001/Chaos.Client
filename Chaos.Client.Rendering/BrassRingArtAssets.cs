using System.Buffers.Binary;
using System.Text.Json;
using Microsoft.Xna.Framework.Graphics;

namespace Chaos.Client.Rendering;

public static class BrassRingArtAssets
{
    private static readonly Lazy<IReadOnlyDictionary<string, KartArtSheet>> Manifest = new(LoadManifest);
    private static readonly Dictionary<(GraphicsDevice Device, string Key), Texture2D> Textures = [];

    public static IReadOnlyDictionary<string, KartArtSheet> ReadManifest() => Manifest.Value;

    public static Stream OpenAsset(string key)
    {
        var sheet = ReadManifest()[key];
        var stream = OpenResource(sheet.File);

        try
        {
            Span<byte> header = stackalloc byte[24];
            stream.ReadExactly(header);
            KartArtAssets.Validate(sheet, BinaryPrimitives.ReadInt32BigEndian(header[16..20]), BinaryPrimitives.ReadInt32BigEndian(header[20..24]));
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
        => typeof(BrassRingArtAssets).Assembly.GetManifestResourceStream($"brassring.{file}")
           ?? throw new InvalidDataException($"Required Brass Ring art resource {file} is missing.");

    private static IReadOnlyDictionary<string, KartArtSheet> LoadManifest()
    {
        using var stream = OpenResource("manifest.json");

        return JsonSerializer.Deserialize<Dictionary<string, KartArtSheet>>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
               ?? throw new InvalidDataException("Brass Ring art manifest is empty.");
    }
}
