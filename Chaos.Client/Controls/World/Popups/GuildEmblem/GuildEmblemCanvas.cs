#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.World.Emblems;
using Chaos.Client.Definitions;
using Chaos.Client.Rendering;
using Chaos.DarkAges.Definitions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Popups.GuildEmblem;

/// <summary>
///     An 11 × 11 guild emblem drawn at <see cref="Zoom" /> on a checker board, so see-through pixels read as empty. Unless
///     <see cref="ReadOnly" />, left-button strokes raise pixel events in grid coordinates.
/// </summary>
public sealed class GuildEmblemCanvas : UIElement
{
    private const int SIZE = GuildEmblemProtocol.SIZE;
    private static readonly Color CheckDark = new(52, 52, 52);
    private static readonly Color CheckLight = new(72, 72, 72);
    private static readonly Color GridLine = new(0, 0, 0, 60);

    private GuildEmblemDesign? Design;
    private bool Dirty;
    private Point LastCell;
    private bool Painting;
    private Texture2D? Texture;

    public GuildEmblemCanvas(int zoom)
    {
        Zoom = zoom;
        Width = SIZE * zoom;
        Height = SIZE * zoom;
    }

    public bool ReadOnly { get; set; }
    public int Zoom { get; }

    public event Action<int, int>? CellPainted;
    public event Action? StrokeEnded;
    public event Action? StrokeStarted;

    public override void Dispose()
    {
        Texture?.Dispose();
        Texture = null;
        base.Dispose();
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        if (!Visible)
            return;

        base.Draw(spriteBatch);

        for (var y = 0; y < SIZE; y++)
            for (var x = 0; x < SIZE; x++)
                DrawRectClipped(
                    spriteBatch,
                    new Rectangle(ScreenX + (x * Zoom), ScreenY + (y * Zoom), Zoom, Zoom),
                    ((x + y) % 2) == 0 ? CheckDark : CheckLight);

        if (Dirty)
            RebuildTexture();

        if (Texture is not null)
            DrawTextureFitted(spriteBatch, Texture, new Rectangle(ScreenX, ScreenY, Width, Height), Color.White);

        for (var i = 1; i < SIZE; i++)
        {
            DrawRectClipped(spriteBatch, new Rectangle(ScreenX + (i * Zoom), ScreenY, 1, Height), GridLine);
            DrawRectClipped(spriteBatch, new Rectangle(ScreenX, ScreenY + (i * Zoom), Width, 1), GridLine);
        }
    }

    public override void OnMouseDown(MouseDownEvent e)
    {
        e.Handled = true;

        if (ReadOnly || Painting || (e.Button != MouseButton.Left))
            return;

        Painting = true;
        LastCell = CellAt(e.ScreenX, e.ScreenY);
        StrokeStarted?.Invoke();
        PaintCell(LastCell.X, LastCell.Y);
    }

    public override void OnMouseMove(MouseMoveEvent e)
    {
        if (!Painting)
            return;

        e.Handled = true;

        //the button came up where no mouse-up reached this canvas (e.g. the window lost focus mid-stroke)
        if (!InputBuffer.IsLeftButtonHeld)
        {
            EndStroke();

            return;
        }

        var cell = CellAt(e.ScreenX, e.ScreenY);

        if (cell == LastCell)
            return;

        PaintLine(LastCell, cell);
        LastCell = cell;
    }

    public override void OnMouseUp(MouseUpEvent e)
    {
        if (!Painting)
            return;

        EndStroke();
        e.Handled = true;
    }

    public override void ResetInteractionState()
    {
        base.ResetInteractionState();
        EndStroke();
    }

    public void SetDesign(GuildEmblemDesign design)
    {
        Design = design;
        Dirty = true;
    }

    private Point CellAt(int screenX, int screenY)
        => new((int)Math.Floor((screenX - ScreenX) / (double)Zoom), (int)Math.Floor((screenY - ScreenY) / (double)Zoom));

    private void EndStroke()
    {
        if (!Painting)
            return;

        Painting = false;
        StrokeEnded?.Invoke();
    }

    private void PaintCell(int x, int y)
    {
        if ((x >= 0) && (y >= 0) && (x < SIZE) && (y < SIZE))
            CellPainted?.Invoke(x, y);
    }

    /// <summary>Paints every pixel on the straight line after <paramref name="from" /> up to <paramref name="to" />, so a fast drag leaves no gaps.</summary>
    private void PaintLine(Point from, Point to)
    {
        var dx = Math.Abs(to.X - from.X);
        var dy = -Math.Abs(to.Y - from.Y);
        var sx = from.X < to.X ? 1 : -1;
        var sy = from.Y < to.Y ? 1 : -1;
        var error = dx + dy;
        var x = from.X;
        var y = from.Y;

        while ((x != to.X) || (y != to.Y))
        {
            var doubled = 2 * error;

            if (doubled >= dy)
            {
                error += dy;
                x += sx;
            }

            if (doubled <= dx)
            {
                error += dx;
                y += sy;
            }

            PaintCell(x, y);
        }
    }

    private void RebuildTexture()
    {
        Dirty = false;

        if (Design is null)
            return;

        //created once; later rebuilds only upload new pixels
        Texture ??= new Texture2D(TextureConverter.Device, SIZE, SIZE);
        Texture.SetData(GuildEmblemTextures.ToPixels(Design));
    }
}
