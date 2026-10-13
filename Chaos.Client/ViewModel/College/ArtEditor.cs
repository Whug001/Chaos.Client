using Microsoft.Xna.Framework;

namespace Chaos.Client.ViewModel.College;

public enum ArtTool
{
    Pen,
    Eraser,
    Fill,
    Line,
    Pick,
    Text
}

/// <summary>
///     The canvas tools over a <see cref="PixelDrawing" />, in canvas pixels. A press, drags and a release make one stroke
///     (one undo step). The right button always erases with the current brush. A line changes nothing until release.
/// </summary>
public sealed class ArtEditor(PixelDrawing drawing)
{
    private bool Erasing;
    private Point Last;
    private Point LineStart;

    public PixelDrawing Drawing { get; private set; } = drawing;
    public ArtTool Tool { get; set; } = ArtTool.Pen;
    public int BrushSize { get; private set; } = 1;
    public bool Mirror { get; set; }

    /// <summary>The swatch the Pen, Fill and Line paint with.</summary>
    public byte Current { get; set; } = 1;

    public bool IsPressing { get; private set; }

    private bool DrawingLine => IsPressing && !Erasing && (Tool == ArtTool.Line);

    public Point? TextAnchor { get; private set; }
    public int TextScale { get; set; } = 1;

    public string PendingText
    {
        get;
        set => field = BoardFont.FilterText(value);
    } = "";

    private int Mirrored(int x) => Drawing.Width - 1 - x;

    public void Load(PixelDrawing drawing)
    {
        Drawing = drawing;
        IsPressing = false;
        TextAnchor = null;
        PendingText = "";
    }

    /// <summary>Takes someone else's board between strokes; unlike <see cref="Load" /> the text tool keeps its anchor and text.</summary>
    public void Adopt(PixelDrawing drawing)
    {
        if (!IsPressing)
            Drawing = drawing;
    }

    /// <summary>Stamps <see cref="PendingText" /> at <see cref="TextAnchor" /> in the current swatch, as one undo step.</summary>
    public bool StampText()
    {
        if (IsPressing || TextAnchor is not { } at || (PendingText.Length == 0))
            return false;

        Drawing.BeginStep();

        foreach (var point in BoardFont.Pixels(PendingText, at.X, at.Y, TextScale))
            Drawing.Set(point.X, point.Y, Current);

        Drawing.EndStep();
        PendingText = "";

        return true;
    }

    /// <summary>Empties the whole drawing, as one undo step.</summary>
    public void ClearAll()
    {
        if (IsPressing)
            return;

        Drawing.BeginStep();

        for (var y = 0; y < Drawing.Height; y++)
            for (var x = 0; x < Drawing.Width; x++)
                Drawing.Set(x, y, 0);

        Drawing.EndStep();
    }

    public void CycleBrush() => BrushSize = (BrushSize % 3) + 1;

    public void Undo()
    {
        if (!IsPressing)
            Drawing.Undo();
    }

    public void Redo()
    {
        if (!IsPressing)
            Drawing.Redo();
    }

    public void SetColour(int swatch, Color colour)
    {
        if (!IsPressing)
            Drawing.SetColour(swatch, colour);
    }

    public void Press(int x, int y, bool erase)
    {
        if (IsPressing)
            return;

        if (Tool == ArtTool.Text)
        {
            TextAnchor = new Point(x, y);

            return;
        }

        Erasing = erase;

        if (!erase)
            switch (Tool)
            {
                case ArtTool.Fill:
                    Drawing.BeginStep();
                    Drawing.Fill(x, y, Current);

                    if (Mirror)
                        Drawing.Fill(Mirrored(x), y, Current);

                    Drawing.EndStep();

                    return;
                case ArtTool.Pick:
                    if (Drawing.Contains(x, y))
                    {
                        Current = Drawing.SwatchAt(x, y);
                        Tool = ArtTool.Pen;
                    }

                    return;
                case ArtTool.Line:
                    IsPressing = true;
                    LineStart = Last = new Point(x, y);

                    return;
            }

        IsPressing = true;
        Drawing.BeginStep();
        Last = new Point(x, y);
        Stamp(x, y);
    }

    public void Drag(int x, int y)
    {
        if (!IsPressing)
            return;

        var to = new Point(x, y);

        if (!DrawingLine)
            foreach (var point in LinePoints(Last, to).Skip(1))
                Stamp(point.X, point.Y);

        Last = to;
    }

    public void Release(int x, int y)
    {
        if (!IsPressing)
            return;

        Drag(x, y);

        if (DrawingLine)
        {
            Drawing.BeginStep();

            foreach (var point in LinePoints(LineStart, Last))
                Stamp(point.X, point.Y);
        }

        Drawing.EndStep();
        IsPressing = false;
    }

    /// <summary>Ends a stroke the window lost (focus or capture): what was painted stays, and a line in progress is dropped.</summary>
    public void Cancel()
    {
        if (!IsPressing)
            return;

        Drawing.EndStep();
        IsPressing = false;
    }

    /// <summary>The pixels a line being dragged would paint, mirrored if Mirror is on. Empty when no line is being dragged.</summary>
    public IReadOnlyCollection<Point> PreviewPixels()
    {
        if ((Tool == ArtTool.Text) && TextAnchor is { } at && (PendingText.Length > 0))
            return BoardFont.Pixels(PendingText, at.X, at.Y, TextScale).Where(p => Drawing.Contains(p.X, p.Y)).ToList();

        if (!DrawingLine)
            return [];

        var pixels = new HashSet<Point>();

        foreach (var point in LinePoints(LineStart, Last))
            foreach (var pixel in Brush(point.X, point.Y))
            {
                if (Drawing.Contains(pixel.X, pixel.Y))
                    pixels.Add(pixel);

                if (Mirror && Drawing.Contains(Mirrored(pixel.X), pixel.Y))
                    pixels.Add(new Point(Mirrored(pixel.X), pixel.Y));
            }

        return pixels;
    }

    public static List<Point> LinePoints(Point from, Point to)
    {
        var points = new List<Point> { from };
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

            points.Add(new Point(x, y));
        }

        return points;
    }

    //size 2 covers the pixel, the one right, below and below right; size 3 is centred on the pixel
    private IEnumerable<Point> Brush(int x, int y)
    {
        var (from, to) = BrushSize switch
        {
            1 => (0, 0),
            2 => (0, 1),
            _ => (-1, 1)
        };

        for (var dy = from; dy <= to; dy++)
            for (var dx = from; dx <= to; dx++)
                yield return new Point(x + dx, y + dy);
    }

    private void Stamp(int x, int y)
    {
        var swatch = Erasing || (Tool == ArtTool.Eraser) ? (byte)0 : Current;

        foreach (var pixel in Brush(x, y))
        {
            Drawing.Set(pixel.X, pixel.Y, swatch);

            if (Mirror)
                Drawing.Set(Mirrored(pixel.X), pixel.Y, swatch);
        }
    }
}
