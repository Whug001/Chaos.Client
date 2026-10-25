using Microsoft.Xna.Framework.Graphics;

namespace Chaos.Client.Rendering;

/// <summary>
///     The Loures Cinema seated view art embedded in this assembly: Assets/Cinema, made by Unora
///     Tools/Cinema/art/draw_seated.py. Each file is read back as "cinema.&lt;file&gt;".
/// </summary>
public static class CinemaAssets
{
    public static Texture2D Load(GraphicsDevice device, string file)
    {
        using var stream = typeof(CinemaAssets).Assembly.GetManifestResourceStream($"cinema.{file}")
                           ?? throw new InvalidOperationException($"missing embedded cinema {file}");

        return Texture2D.FromStream(device, stream);
    }
}
