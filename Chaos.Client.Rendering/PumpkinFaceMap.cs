#region
using System.Text.Json;
#endregion

namespace Chaos.Client.Rendering;

/// <summary>One sprite pixel on a pumpkin's face: where it is in the frame, how far from the face's middle (0-100), and the cells it shows.</summary>
public sealed record PumpkinFacePixel(int X, int Y, int Glow, int[] Cells);

/// <summary>
///     The Pumpkin Carving face map (spec: docs/superpowers/specs/2026-10-03-pumpkin-carving-design.md, 5.2), embedded as
///     "pumpkin.facemap.json". Written by Unora's Tools/PumpkinCarving/make_pumpkin.py from the geometry that drew sprite
///     <see cref="SPRITE_ID" />, so the two always match. Coordinates are in the full frame (0,0 at its top left).
/// </summary>
public sealed class PumpkinFaceMap
{
    public const int FRONT_FRAME = 1;
    public const int SPRITE_ID = 1455;

    private const string RESOURCE = "pumpkin.facemap.json";
    private static PumpkinFaceMap? SharedMap;

    private readonly PumpkinFacePixel[][] Frames;

    private PumpkinFaceMap(PumpkinFacePixel[][] frames) => Frames = frames;

    public int FrameCount => Frames.Length;

    public static PumpkinFaceMap Shared => SharedMap ??= Load();

    public IReadOnlyList<PumpkinFacePixel> For(int frameIndex)
        => (frameIndex >= 0) && (frameIndex < Frames.Length) ? Frames[frameIndex] : [];

    public static PumpkinFaceMap Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);

        var frames = doc.RootElement
                        .GetProperty("frames")
                        .EnumerateArray()
                        .Select(frame => frame.EnumerateArray()
                                              .Select(ToPixel)
                                              .ToArray())
                        .ToArray();

        return new PumpkinFaceMap(frames);
    }

    private static PumpkinFaceMap Load()
    {
        using var stream = typeof(PumpkinFaceMap).Assembly.GetManifestResourceStream(RESOURCE)
                           ?? throw new InvalidOperationException($"missing embedded {RESOURCE}");
        using var reader = new StreamReader(stream);

        return Parse(reader.ReadToEnd());
    }

    private static PumpkinFacePixel ToPixel(JsonElement entry)
    {
        var values = entry.EnumerateArray()
                          .Select(value => value.GetInt32())
                          .ToArray();

        return new PumpkinFacePixel(values[0], values[1], values[2], values[3..]);
    }
}
