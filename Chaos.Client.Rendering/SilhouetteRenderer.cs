#region
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Rendering;

/// <summary>
///     Renders translucent silhouettes of blocked entities by compositing into a single viewport-sized offscreen render
///     target — all silhouette entities are drawn at their world positions with correct inter-entity occlusion, then the
///     entire RT is overlaid with uniform alpha. Transparent (invisible/near-phantom) aislings are handled directly in
///     the stripe pass via <see cref="AislingRenderer.Draw" /> at reduced alpha — the pre-composited CompositeCache
///     texture already bakes all layers into a single opaque image, so uniform-alpha drawing produces the correct result
///     with no layer bleed-through.
/// </summary>
public sealed class SilhouetteRenderer : IDisposable
{
    public const float SILHOUETTE_ALPHA = 0.50f;

    private readonly GraphicsDevice Device;
    public readonly List<uint> SilhouetteEntityIds = [];
    private SpriteBatch? Batch;
    private bool SilhouettesReady;
    private RenderTarget2D? SilhouetteTarget;

    public SilhouetteRenderer(GraphicsDevice device) => Device = device;

    /// <inheritdoc />
    public void Dispose()
    {
        SilhouetteTarget?.Dispose();
        Batch?.Dispose();
    }

    /// <summary>
    ///     Adds any entity to be rendered as a silhouette this frame (drawn on top of world as a single alpha overlay).
    /// </summary>
    public void AddSilhouette(uint entityId) => SilhouetteEntityIds.Add(entityId);

    /// <summary>
    ///     Clears all entries. Call at the start of each frame before adding entities.
    /// </summary>
    public void Clear()
    {
        SilhouetteEntityIds.Clear();
        SilhouettesReady = false;
    }

    /// <summary>
    ///     Draws the pre-rendered silhouette overlay onto the active SpriteBatch. Call after the world pass. The scale
    ///     defaults to today’s back-buffer values; pass the surface scales while Smooth screen scrolling is on.
    /// </summary>
    public void DrawSilhouettes(SpriteBatch spriteBatch, float scaleX = 1f, float scaleY = 1f)
    {
        if (!SilhouettesReady || SilhouetteTarget is null)
            return;

        spriteBatch.Draw(
            SilhouetteTarget,
            Vector2.Zero,
            null,
            Color.White * SILHOUETTE_ALPHA,
            0f,
            Vector2.Zero,
            new Vector2(1f / scaleX, 1f / scaleY),
            SpriteEffects.None,
            0f);
    }

    /// <summary>
    ///     Composites all silhouette entities into a single viewport-sized RT at their world positions. Call from WorldScreen
    ///     after the normal entity draw pass, passing the same camera transform. The target size defaults to today’s
    ///     back-buffer values; pass the world target’s size while Smooth screen scrolling is on.
    /// </summary>
    public void PreRenderSilhouettes(Action<SpriteBatch> drawEntities, Point? targetSize = null)
    {
        if (SilhouetteEntityIds.Count == 0)
        {
            SilhouettesReady = false;

            return;
        }

        Batch ??= new SpriteBatch(Device);

        var screenWidth = targetSize?.X ?? Device.PresentationParameters.BackBufferWidth;
        var screenHeight = targetSize?.Y ?? Device.PresentationParameters.BackBufferHeight;

        if (SilhouetteTarget is null || (SilhouetteTarget.Width != screenWidth) || (SilhouetteTarget.Height != screenHeight))
        {
            SilhouetteTarget?.Dispose();
            SilhouetteTarget = new RenderTarget2D(Device, screenWidth, screenHeight);
        }

        var bindings = Device.GetRenderTargets();

        var previousTarget = bindings.Length > 0 ? bindings[0].RenderTarget as RenderTarget2D : null;

        Device.SetRenderTarget(SilhouetteTarget);

        Device.Clear(
            new Color(
                0,
                0,
                0,
                0));

        //the caller draws all silhouetted entities using the same spritebatch/transform as the normal world pass
        drawEntities(Batch);

        Device.SetRenderTarget(previousTarget);
        SilhouettesReady = true;
    }
}
