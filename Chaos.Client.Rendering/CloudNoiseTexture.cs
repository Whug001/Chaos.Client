#region
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Rendering;

/// <summary>
///     Builds seamless (toroidal) cloud-like value-noise textures for the ambient overlay
///     renderers (<see cref="MistRenderer" />). The alpha channel
///     carries the cloud shape; RGB is left white so the draw call tints it. Deterministic per seed,
///     so every client renders identical banks and tiling has no seams.
/// </summary>
public static class CloudNoiseTexture
{
    /// <summary>
    ///     Builds a <paramref name="width" /> x <paramref name="height" /> texture whose alpha is
    ///     3-octave fractal value noise wrapping cleanly in both axes.
    ///     <paramref name="contrastKnee" /> / <paramref name="contrastRange" /> shape the wispiness:
    ///     alpha = smoothstep(clamp((noise - knee) / range, 0, 1)) — a higher knee gives sparser,
    ///     wispier banks. Dimensions must be multiples of 16x8 so the octave lattices divide evenly.
    /// </summary>
    public static Texture2D Build(
        GraphicsDevice device,
        int width,
        int height,
        int seed,
        float contrastKnee,
        float contrastRange)
    {
        var rng = new Random(seed);

        //three octaves of value noise, each on a lattice whose dimensions divide the texture size so
        //the noise wraps cleanly. weight halves per octave (fractal brownian motion).
        var octaves = new (int LatW, int LatH, float Weight, float[] Lattice)[]
        {
            (4, 2, 0.55f, null!),
            (8, 4, 0.30f, null!),
            (16, 8, 0.15f, null!)
        };

        for (var o = 0; o < octaves.Length; o++)
        {
            var (latW, latH, weight, _) = octaves[o];
            var lattice = new float[latW * latH];

            for (var i = 0; i < lattice.Length; i++)
                lattice[i] = (float)rng.NextDouble();

            octaves[o] = (latW, latH, weight, lattice);
        }

        var pixels = new Color[width * height];

        for (var py = 0; py < height; py++)
            for (var px = 0; px < width; px++)
            {
                var n = 0f;

                foreach (var (latW, latH, weight, lattice) in octaves)
                    n += weight * SampleWrapped(lattice, latW, latH, px / (float)width, py / (float)height);

                //contrast curve: push toward wispy banks (low values sparse, mid values soft)
                n = Math.Clamp((n - contrastKnee) / contrastRange, 0f, 1f);
                n *= n * (3f - 2f * n); // smoothstep for softer edges

                var a = (byte)(n * 255f);
                pixels[py * width + px] = new Color((byte)255, (byte)255, (byte)255, a);
            }

        var tex = new Texture2D(device, width, height);
        tex.SetData(pixels);

        return tex;
    }

    //bilinear sample of a wrapping lattice at normalized (u,v) in [0,1); wraps in both axes so the
    //result tiles seamlessly.
    private static float SampleWrapped(float[] lattice, int latW, int latH, float u, float v)
    {
        var gx = u * latW;
        var gy = v * latH;

        var x0 = (int)MathF.Floor(gx);
        var y0 = (int)MathF.Floor(gy);
        var fx = gx - x0;
        var fy = gy - y0;

        var x0w = ((x0 % latW) + latW) % latW;
        var y0w = ((y0 % latH) + latH) % latH;
        var x1w = (x0w + 1) % latW;
        var y1w = (y0w + 1) % latH;

        var v00 = lattice[y0w * latW + x0w];
        var v10 = lattice[y0w * latW + x1w];
        var v01 = lattice[y1w * latW + x0w];
        var v11 = lattice[y1w * latW + x1w];

        //smoothstep the interpolation weights for smoother clouds
        fx = fx * fx * (3f - 2f * fx);
        fy = fy * fy * (3f - 2f * fy);

        var top = v00 + (v10 - v00) * fx;
        var bottom = v01 + (v11 - v01) * fx;

        return top + (bottom - top) * fy;
    }
}
