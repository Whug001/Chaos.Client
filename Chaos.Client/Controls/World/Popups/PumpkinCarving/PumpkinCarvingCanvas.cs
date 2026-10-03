#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Definitions;
using Chaos.Client.Rendering;
using Chaos.DarkAges.Definitions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Popups.PumpkinCarving;

/// <summary>
///     The carving grid at a fixed zoom: skin cells orange, cut cells candle yellow. A left drag uses the window's tool; a
///     right drag always erases. Raises one event per cell, with every cell on the line between mouse moves.
/// </summary>
public sealed class PumpkinCarvingCanvas : UIElement
{
    private static readonly Color Cut = new(255, 226, 122);
    private static readonly Color GridLine = new(0, 0, 0, 50);
    private static readonly Color Skin = new(217, 116, 28);

    private bool Erasing;
    private byte[] Grid = PumpkinGrid.Empty();
    private Point LastCell;
    private bool Painting;

    public PumpkinCarvingCanvas(int zoom)
    {
        Zoom = zoom;
        Width = PumpkinGrid.WIDTH * zoom;
        Height = PumpkinGrid.HEIGHT * zoom;
    }

    public int Zoom { get; }

    /// <summary>x, y, and true when the stroke erases (a right drag).</summary>
    public event Action<int, int, bool>? CellPainted;

    public event Action? StrokeEnded;
    public event Action? StrokeStarted;

    public override void Draw(SpriteBatch spriteBatch)
    {
        if (!Visible)
            return;

        base.Draw(spriteBatch);

        for (var y = 0; y < PumpkinGrid.HEIGHT; y++)
            for (var x = 0; x < PumpkinGrid.WIDTH; x++)
                DrawRectClipped(
                    spriteBatch,
                    new Rectangle(ScreenX + (x * Zoom), ScreenY + (y * Zoom), Zoom, Zoom),
                    PumpkinGrid.IsCut(Grid, x, y) ? Cut : Skin);

        for (var x = 1; x < PumpkinGrid.WIDTH; x++)
            DrawRectClipped(spriteBatch, new Rectangle(ScreenX + (x * Zoom), ScreenY, 1, Height), GridLine);

        for (var y = 1; y < PumpkinGrid.HEIGHT; y++)
            DrawRectClipped(spriteBatch, new Rectangle(ScreenX, ScreenY + (y * Zoom), Width, 1), GridLine);
    }

    public override void OnMouseDown(MouseDownEvent e)
    {
        e.Handled = true;

        if (Painting || ((e.Button != MouseButton.Left) && (e.Button != MouseButton.Right)))
            return;

        Painting = true;
        Erasing = e.Button == MouseButton.Right;
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
        if (!(Erasing ? InputBuffer.IsRightButtonHeld : InputBuffer.IsLeftButtonHeld))
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

    public void SetGrid(byte[] grid) => Grid = grid;

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
        if ((x >= 0) && (y >= 0) && (x < PumpkinGrid.WIDTH) && (y < PumpkinGrid.HEIGHT))
            CellPainted?.Invoke(x, y, Erasing);
    }

    /// <summary>Paints every cell on the straight line after <paramref name="from" /> up to <paramref name="to" />, so a fast drag leaves no gaps.</summary>
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
}
