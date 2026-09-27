#region
using Chaos.Client.Controls.Components;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Emblems;

/// <summary>
///     Draws one emblem centered in its bounds at a whole-number <see cref="Scale" />, pixel-sharp. Locked emblems are
///     drawn <see cref="Dimmed" />. Raises <see cref="Clicked" /> on a left click when something listens. It never disposes
///     its texture: emblem textures belong to UiRenderer's cache.
/// </summary>
public sealed class EmblemIcon : UIElement
{
    private static readonly Color DimTint = new(80, 80, 80);

    public ushort Art { get; set; }
    public bool Dimmed { get; set; }
    public int Scale { get; set; } = 1;

    public event Action? Clicked;

    public override void Draw(SpriteBatch spriteBatch)
    {
        if (!Visible)
            return;

        base.Draw(spriteBatch);

        if (EmblemTextures.Get(Art, Environment.TickCount64) is not { } texture)
            return;

        var width = texture.Width * Scale;
        var height = texture.Height * Scale;

        DrawTextureFitted(
            spriteBatch,
            texture,
            new Rectangle(ScreenX + (Width - width) / 2, ScreenY + (Height - height) / 2, width, height),
            Dimmed ? DimTint : Color.White);
    }

    public override void OnClick(ClickEvent e)
    {
        if ((Clicked is null) || (e.Button != MouseButton.Left))
            return;

        Clicked.Invoke();
        e.Handled = true;
    }
}
