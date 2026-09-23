#region
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Rendering;

/// <summary>
///     A full-viewport ambient effect switched on and off by a map flag (fog, lightning, fireflies, ...). Managed as a
///     group by <see cref="AmbientEffects" />. Touched only on the game-loop thread.
/// </summary>
public interface IAmbientOverlay : IDisposable
{
    /// <summary>The blend state <see cref="Draw" /> expects the caller's <c>SpriteBatch.Begin</c> to use.</summary>
    BlendState BlendState { get; }

    /// <summary>True while the effect is on or still fading out — i.e. the screen should draw it.</summary>
    bool IsActive { get; }

    /// <summary>
    ///     Turns the effect on or off. Normally it fades; <paramref name="immediate" /> skips the fade, used on map
    ///     change so the previous map's effect doesn't linger over the new one.
    /// </summary>
    void SetActive(bool on, bool immediate = false);

    /// <summary>
    ///     Advances the effect one frame. <paramref name="worldOrigin" /> is the SCREEN position of world pixel (0,0)
    ///     (<c>Camera.WorldToScreen(Vector2.Zero)</c>), so effects can stay anchored to the map while the camera pans.
    /// </summary>
    void Update(GameTime gameTime, Rectangle viewport, Vector2 worldOrigin);

    /// <summary>
    ///     Draws the effect inside <paramref name="viewport" />. The caller owns the surrounding
    ///     <c>SpriteBatch.Begin</c>/<c>End</c>, using <see cref="BlendState" />.
    /// </summary>
    void Draw(SpriteBatch spriteBatch, Rectangle viewport, Vector2 worldOrigin);
}
