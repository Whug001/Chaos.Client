#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Definitions;
using Chaos.Client.Rendering;
using Chaos.Client.Systems.College;
using Chaos.Client.ViewModel.College;
using Chaos.DarkAges.Definitions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Popups.College;

/// <summary>
///     The drawing at its zoom (4 by default), with grid lines, the mirror line and a line tool's preview. Mouse input goes to the
///     <see cref="ArtEditor" /> in canvas pixels; the texture is rebuilt only when the drawing's version changes.
/// </summary>
public sealed class ArtCanvas : UIElement
{
    private static readonly Color GridLine = new(0, 0, 0, 50);
    private static readonly Color MirrorLine = new(255, 255, 255, 150);

    private readonly ArtEditor Editor;
    private readonly int Zoom;
    private bool Painting;
    private bool RightButton;
    private Texture2D? Texture;
    private Color[]? TextureColours;
    private PixelDrawing? TextureDrawing;
    private int TextureVersion = -1;

    public ArtCanvas(ArtEditor editor, int zoom = CollegeProtocol.DRAWING_ZOOM)
    {
        Editor = editor;
        Zoom = zoom;
        Resize();
    }

    public bool ShowGrid { get; set; } = true;

    /// <summary>Raised after a press, a drag or a release, so the window can refresh its buttons.</summary>
    public event Action? Changed;

    public override void Dispose()
    {
        Texture?.Dispose();
        base.Dispose();
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        if (!Visible)
            return;

        base.Draw(spriteBatch);
        EnsureTexture();
        DrawTexture(spriteBatch, Texture, new Vector2(ScreenX, ScreenY), Color.White);

        var preview = Editor.Drawing.Palette[Editor.Current];

        foreach (var pixel in Editor.PreviewPixels())
            DrawRectClipped(spriteBatch, new Rectangle(ScreenX + (pixel.X * Zoom), ScreenY + (pixel.Y * Zoom), Zoom, Zoom), preview);

        if (ShowGrid)
        {
            for (var x = 1; x < Editor.Drawing.Width; x++)
                DrawRectClipped(spriteBatch, new Rectangle(ScreenX + (x * Zoom), ScreenY, 1, Height), GridLine);

            for (var y = 1; y < Editor.Drawing.Height; y++)
                DrawRectClipped(spriteBatch, new Rectangle(ScreenX, ScreenY + (y * Zoom), Width, 1), GridLine);
        }

        if (Editor.Mirror)
            DrawRectClipped(spriteBatch, new Rectangle(ScreenX + (Width / 2), ScreenY, 1, Height), MirrorLine);
    }

    public override void OnMouseDown(MouseDownEvent e)
    {
        e.Handled = true;

        if (Painting || ((e.Button != MouseButton.Left) && (e.Button != MouseButton.Right)))
            return;

        RightButton = e.Button == MouseButton.Right;
        var (x, y) = CellAt(e.ScreenX, e.ScreenY);
        Editor.Press(x, y, RightButton);
        Painting = Editor.IsPressing;
        Changed?.Invoke();
    }

    public override void OnMouseMove(MouseMoveEvent e)
    {
        if (!Painting)
            return;

        e.Handled = true;
        var (x, y) = CellAt(e.ScreenX, e.ScreenY);

        //the button came up where no mouse-up reached this canvas (e.g. the window lost focus mid-stroke)
        if (!(RightButton ? InputBuffer.IsRightButtonHeld : InputBuffer.IsLeftButtonHeld))
        {
            Finish(x, y);

            return;
        }

        Editor.Drag(x, y);
    }

    public override void OnMouseUp(MouseUpEvent e)
    {
        if (!Painting)
            return;

        e.Handled = true;
        var (x, y) = CellAt(e.ScreenX, e.ScreenY);
        Finish(x, y);
    }

    public override void ResetInteractionState()
    {
        base.ResetInteractionState();

        if (!Painting)
            return;

        Painting = false;
        Editor.Cancel();
        Changed?.Invoke();
    }

    private void Resize()
    {
        Width = Editor.Drawing.Width * Zoom;
        Height = Editor.Drawing.Height * Zoom;
    }

    private (int X, int Y) CellAt(int screenX, int screenY)
        => ((int)Math.Floor((screenX - ScreenX) / (double)Zoom), (int)Math.Floor((screenY - ScreenY) / (double)Zoom));

    private void Finish(int x, int y)
    {
        Painting = false;
        Editor.Release(x, y);
        Changed?.Invoke();
    }

    private void EnsureTexture()
    {
        var drawing = Editor.Drawing;

        if ((Texture is not null) && ReferenceEquals(TextureDrawing, drawing) && (TextureVersion == drawing.Version))
            return;

        if ((Width != drawing.Width * Zoom) || (Height != drawing.Height * Zoom))
        {
            Resize();
            Texture?.Dispose();
            Texture = null;
            TextureColours = null;
        }

        Texture ??= new Texture2D(TextureConverter.Device, Width, Height);
        TextureColours ??= new Color[Width * Height];
        Texture.SetData(DrawingTextures.ToColors(drawing.Palette, drawing.Pixels, Zoom, drawing.Width, drawing.Height, TextureColours));
        TextureDrawing = drawing;
        TextureVersion = drawing.Version;
    }
}
