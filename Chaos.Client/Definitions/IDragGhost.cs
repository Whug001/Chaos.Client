using Microsoft.Xna.Framework.Graphics;

namespace Chaos.Client.Definitions;

/// <summary>
///     A drag payload that renders its own cursor-following ghost icon. The texture is borrowed — the payload never owns
///     or disposes it.
/// </summary>
public interface IDragGhost
{
    Texture2D? GhostTexture { get; }
}
