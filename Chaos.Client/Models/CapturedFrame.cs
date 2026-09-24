using SkiaSharp;

namespace Chaos.Client.Models;

/// <summary>
///     One rendered frame kept in memory for a bug report: the full 640x480 picture as a 256-color PNG, and a small
///     preview for the report window. Never written to disk on the player's computer.
/// </summary>
public sealed class CapturedFrame(byte[] png, SKImage thumbnail) : IDisposable
{
    public const int THUMBNAIL_WIDTH = 110;
    public const int THUMBNAIL_HEIGHT = 83;

    public byte[] Png { get; } = png;
    public SKImage Thumbnail { get; } = thumbnail;

    public void Dispose() => Thumbnail.Dispose();
}
