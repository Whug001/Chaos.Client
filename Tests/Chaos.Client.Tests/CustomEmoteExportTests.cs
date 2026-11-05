using Chaos.Client.Rendering.CustomEmotes;
using FluentAssertions;
using SkiaSharp;

namespace Chaos.Client.Tests;

public class CustomEmoteExportTests
{
    private const float SAMPLE_MS = 100f;

    [Test]
    public async Task ExportsEveryPixelArtEmoteFrame()
    {
        var dir = Environment.GetEnvironmentVariable("EMOTE_EXPORT_DIR");

        if (string.IsNullOrWhiteSpace(dir))
            Skip.Test("EMOTE_EXPORT_DIR is not set");

        var notPixelArt = new List<ICustomEmote>();

        foreach (var emote in CustomEmoteRegistry.All)
        {
            if (emote is not PixelArtEmote art)
            {
                notPixelArt.Add(emote);
                continue;
            }

            var name = emote.GetType().Name.Replace("Emote", string.Empty);
            var folder = Path.Combine(dir!, name);
            Directory.CreateDirectory(folder);
            var written = 0;

            for (var t = 0f; t < art.DurationMs; t += SAMPLE_MS)
            {
                var layers = art.Compose(t, 0);

                if (layers.Count == 0)
                    continue;

                using var bitmap = Flatten(layers);
                using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
                File.WriteAllBytes(Path.Combine(folder, $"{written:D2}.png"), data.ToArray());
                written++;
            }

            written.Should().BeGreaterThan(0, $"{name} drew nothing between 0 and {art.DurationMs} ms");
        }

        notPixelArt.Should().ContainSingle().Which.Should().BeOfType<MiddleFingerEmote>();
        await Task.CompletedTask;
    }

    private static SKBitmap Flatten(IReadOnlyList<PixelLayer> layers)
    {
        var left = layers.Min(l => l.X);
        var top = layers.Min(l => l.Y);
        var right = layers.Max(l => l.X + l.Art.Width);
        var bottom = layers.Max(l => l.Y + l.Art.Height);
        var bitmap = new SKBitmap(right - left, bottom - top, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        bitmap.Erase(SKColors.Transparent);
        using var canvas = new SKCanvas(bitmap);

        foreach (var layer in layers)
        {
            using var art = PixelSprite.ToBitmap(layer.Art);
            using var paint = new SKPaint { Color = SKColors.White.WithAlpha((byte)Math.Round(255 * Math.Clamp(layer.Opacity, 0f, 1f))) };
            canvas.DrawBitmap(art, layer.X - left, layer.Y - top, paint);
        }

        return bitmap;
    }
}
