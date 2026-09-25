#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Definitions;
using Chaos.Client.Rendering;
using Chaos.DarkAges.Definitions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Popups.GuildCloak;

/// <summary>Which view a guild cloak canvas shows.</summary>
public enum GuildCloakView
{
    Front,
    Back
}

/// <summary>
///     One zoomed view of a guild cloak design in flat colors: the back, or the front (the lining, a faint body for placement,
///     and the collar on top). Unless <see cref="ReadOnly" />, left-button strokes raise cell events in grid coordinates.
/// </summary>
public sealed class GuildCloakCanvas : UIElement
{
    private const float GHOST_ALPHA = 0.3f;

    private static readonly Color Background = new(28, 28, 34);
    private static readonly Color GhostTint = new(200, 190, 170);

    //premultiplied: black at alpha 60
    private static readonly Color GridLine = new(0, 0, 0, 60);

    private readonly Point CollarAt;
    private readonly Point GhostAt;
    private readonly Point LiningAt;

    //one CPU buffer for the life of the canvas, refilled on every rebuild
    private readonly Color[] Pixels;
    private readonly GuildCloakReferences References;
    private readonly GuildCloakView View;
    private readonly int ViewHeight;
    private readonly int ViewWidth;
    private readonly int Zoom;
    private GuildCloakDesign? Design;
    private bool Dirty = true;
    private Point LastCell;
    private bool Painting;
    private Texture2D? Texture;

    public GuildCloakCanvas(GuildCloakReferences references, GuildCloakView view, int zoom)
    {
        References = references;
        View = view;
        Zoom = zoom;

        if (view == GuildCloakView.Back)
        {
            ViewWidth = references.Back.Width;
            ViewHeight = references.Back.Height;
        } else
        {
            var lining = references.LiningFrame;
            var collar = references.CollarFrame;
            var left = Math.Min(lining.Left, collar.Left);
            var top = Math.Min(lining.Top, collar.Top);
            ViewWidth = Math.Max(lining.Right, collar.Right) - left;
            ViewHeight = Math.Max(lining.Bottom, collar.Bottom) - top;
            LiningAt = new Point(lining.Left - left, lining.Top - top);
            CollarAt = new Point(collar.Left - left, collar.Top - top);

            //accessory frames draw 27 px left of body frames on the character's canvas
            if (references.BodyFront is { } body)
                GhostAt = new Point(body.Left - AislingRenderer.GetLayerOffsetX('c') - left, body.Top - top);
        }

        Pixels = new Color[ViewWidth * ViewHeight];
        Width = ViewWidth * zoom;
        Height = ViewHeight * zoom;
    }

    /// <summary>No strokes: the review window shows designs without editing them.</summary>
    public bool ReadOnly { get; set; }

    /// <summary>Raised for each cell a stroke touches, in the grid of the part it belongs to.</summary>
    public event Action<GuildCloakPart, int, int>? CellPainted;

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

        if (Dirty)
            RebuildTexture();

        var bounds = new Rectangle(ScreenX, ScreenY, Width, Height);
        DrawRectClipped(spriteBatch, bounds, Background);
        DrawTextureFitted(spriteBatch, Texture, bounds, Color.White);

        if (Zoom < 4)
            return;

        for (var x = 1; x < ViewWidth; x++)
            DrawRectClipped(spriteBatch, new Rectangle(ScreenX + (x * Zoom), ScreenY, 1, Height), GridLine);

        for (var y = 1; y < ViewHeight; y++)
            DrawRectClipped(spriteBatch, new Rectangle(ScreenX, ScreenY + (y * Zoom), Width, 1), GridLine);
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

    public void SetDesign(GuildCloakDesign design)
    {
        Design = design;
        Dirty = true;
    }

    private void EndStroke()
    {
        if (!Painting)
            return;

        Painting = false;
        StrokeEnded?.Invoke();
    }

    /// <summary>
    ///     Paints every cell on the straight line after <paramref name="from" /> up to <paramref name="to" />, so a fast drag
    ///     leaves no gaps. <paramref name="from" /> was painted by the previous event. Cells may lie outside the view (the
    ///     cursor left the canvas mid-stroke); those are skipped.
    /// </summary>
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

    /// <summary>Raises <see cref="CellPainted" /> for one cell of this view, in the grid of the part under it.</summary>
    private void PaintCell(int x, int y)
    {
        if ((x < 0) || (y < 0) || (x >= ViewWidth) || (y >= ViewHeight))
            return;

        if (View == GuildCloakView.Back)
        {
            CellPainted?.Invoke(GuildCloakPart.Back, x, y);

            return;
        }

        //the collar is on top: paint it where it has a pixel, else the lining underneath
        if (References.Collar.IsFilled(x - CollarAt.X, y - CollarAt.Y))
            CellPainted?.Invoke(GuildCloakPart.Collar, x - CollarAt.X, y - CollarAt.Y);
        else
            CellPainted?.Invoke(GuildCloakPart.Lining, x - LiningAt.X, y - LiningAt.Y);
    }

    private void RebuildTexture()
    {
        Dirty = false;
        Array.Clear(Pixels);

        if (Design is { } design)
        {
            if (View == GuildCloakView.Back)
                Stamp(References.Back, design.Back, design.Colors, Point.Zero);
            else
            {
                Stamp(References.Lining, design.Lining, design.Colors, LiningAt);
                StampGhost();
                Stamp(References.Collar, design.Collar, design.Colors, CollarAt);
            }
        }

        //created once; later rebuilds only upload new pixels
        Texture ??= new Texture2D(TextureConverter.Device, ViewWidth, ViewHeight);
        Texture.SetData(Pixels);
    }

    private void Stamp(GuildCloakGrid grid, byte[] cells, List<GuildCloakColor> colors, Point at)
    {
        if (colors.Count == 0)
            return;

        for (var y = 0; y < grid.Height; y++)
            for (var x = 0; x < grid.Width; x++)
            {
                if (!grid.IsFilled(x, y))
                    continue;

                var number = cells[(y * grid.Width) + x];
                var color = (number >= 1) && (number <= colors.Count) ? colors[number - 1] : colors[0];
                Pixels[((y + at.Y) * ViewWidth) + x + at.X] = new Color(color.R, color.G, color.B);
            }
    }

    private void StampGhost()
    {
        if (References.BodyFront is not { } body)
            return;

        var width = body.PixelWidth;

        for (var y = 0; y < body.PixelHeight; y++)
            for (var x = 0; x < width; x++)
            {
                if (body.Data[(y * width) + x] == 0)
                    continue;

                var px = x + GhostAt.X;
                var py = y + GhostAt.Y;

                if ((px < 0) || (py < 0) || (px >= ViewWidth) || (py >= ViewHeight))
                    continue;

                var index = (py * ViewWidth) + px;

                //premultiplied: an empty cell gets the tint at 30%, a painted one is blended 30% towards it
                Pixels[index] = Pixels[index].A == 0 ? GhostTint * GHOST_ALPHA : Color.Lerp(Pixels[index], GhostTint, GHOST_ALPHA);
            }
    }

    /// <summary>The view cell under a screen point. It lies outside the view when the point is outside the canvas.</summary>
    private Point CellAt(int screenX, int screenY)
        => new(FloorDiv(screenX - ScreenX, Zoom), FloorDiv(screenY - ScreenY, Zoom));

    //rounds towards negative infinity, so the pixels just left of or above the canvas are cell -1, not cell 0
    private static int FloorDiv(int value, int divisor) => value >= 0 ? value / divisor : ((value + 1) / divisor) - 1;
}
