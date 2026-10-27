using Microsoft.Xna.Framework.Graphics;

namespace Chaos.Client.Rendering;

/// <summary>
///     The full-resolution target the world is drawn into while Smooth screen scrolling is on. ChaosGame lays the
///     640x480 HUD target over it on the back buffer.
/// </summary>
public sealed class WorldSurface : IDisposable
{
    private readonly GraphicsDevice Device;

    public WorldSurface(GraphicsDevice device) => Device = device;

    public RenderTarget2D? Target { get; private set; }
    public WorldSurfaceLayout Layout { get; private set; }
    public bool DrawnThisFrame { get; private set; }

    public void BeginFrame() => DrawnThisFrame = false;

    /// <summary>
    ///     Makes sure the target matches the layout's size and marks it drawn for this frame. The caller binds it.
    /// </summary>
    public RenderTarget2D Prepare(WorldSurfaceLayout layout)
    {
        var width = Math.Max(1, layout.Destination.Width);
        var height = Math.Max(1, layout.Destination.Height);

        if (Target is null || (Target.Width != width) || (Target.Height != height))
        {
            Target?.Dispose();
            Target = new RenderTarget2D(Device, width, height, false, SurfaceFormat.Color, DepthFormat.Depth24Stencil8);
        }

        Layout = layout;
        DrawnThisFrame = true;

        return Target;
    }

    public void Dispose() => Target?.Dispose();
}
