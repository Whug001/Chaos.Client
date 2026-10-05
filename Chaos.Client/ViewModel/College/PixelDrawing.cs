using Chaos.DarkAges.Definitions;
using Microsoft.Xna.Framework;

namespace Chaos.Client.ViewModel.College;

/// <summary>
///     A 96 x 72 College drawing: 32 swatch colours and one swatch number per pixel. Edits made between
///     <see cref="BeginStep" /> and <see cref="EndStep" /> undo as one step; up to <see cref="MAX_UNDO" /> steps are kept.
/// </summary>
public sealed class PixelDrawing
{
    public const int WIDTH = CollegeProtocol.DRAWING_WIDTH;
    public const int HEIGHT = CollegeProtocol.DRAWING_HEIGHT;
    public const int COLOURS = CollegeProtocol.DRAWING_COLOURS;
    public const int MAX_UNDO = 50;

    private readonly List<byte[]> RedoSteps = [];
    private readonly List<byte[]> UndoSteps = [];
    private byte[]? StepStart;

    private PixelDrawing(Color[] palette, byte[] pixels)
    {
        Palette = palette;
        Pixels = pixels;
    }

    public Color[] Palette { get; }
    public byte[] Pixels { get; }

    /// <summary>Goes up on every change, so a texture knows when to redraw.</summary>
    public int Version { get; private set; }

    /// <summary>Changed since it was loaded or last saved.</summary>
    public bool IsDirty { get; private set; }

    public bool CanUndo => UndoSteps.Count > 0;
    public bool CanRedo => RedoSteps.Count > 0;
    public int DrawnPixels => CollegeProtocol.DrawnPixels(Pixels);

    public static PixelDrawing Blank() => new(ArtPalette.Default.ToArray(), new byte[WIDTH * HEIGHT]);

    /// <summary>A palette shorter than 32 colours keeps the default for the rest; a swatch number of 32 or more becomes 0.</summary>
    public static PixelDrawing From(byte[] palette, byte[] pixels)
    {
        var colours = ArtPalette.Default.ToArray();

        for (var i = 0; (i < COLOURS) && ((i * 3) + 2 < palette.Length); i++)
            colours[i] = new Color(palette[i * 3], palette[(i * 3) + 1], palette[(i * 3) + 2]);

        var cells = new byte[WIDTH * HEIGHT];

        for (var i = 0; (i < cells.Length) && (i < pixels.Length); i++)
            cells[i] = pixels[i] < COLOURS ? pixels[i] : (byte)0;

        return new PixelDrawing(colours, cells);
    }

    public static bool InBounds(int x, int y) => (x >= 0) && (y >= 0) && (x < WIDTH) && (y < HEIGHT);

    public byte[] PaletteBytes()
    {
        var bytes = new byte[COLOURS * 3];

        for (var i = 0; i < COLOURS; i++)
        {
            bytes[i * 3] = Palette[i].R;
            bytes[(i * 3) + 1] = Palette[i].G;
            bytes[(i * 3) + 2] = Palette[i].B;
        }

        return bytes;
    }

    public byte SwatchAt(int x, int y) => Pixels[(y * WIDTH) + x];

    public void BeginStep() => StepStart ??= Snapshot();

    public void EndStep()
    {
        if (StepStart is null)
            return;

        if (!StepStart.AsSpan().SequenceEqual(Snapshot()))
        {
            UndoSteps.Add(StepStart);

            if (UndoSteps.Count > MAX_UNDO)
                UndoSteps.RemoveAt(0);

            RedoSteps.Clear();
            IsDirty = true;
        }

        StepStart = null;
    }

    public void Set(int x, int y, byte swatch)
    {
        if (!InBounds(x, y))
            return;

        var index = (y * WIDTH) + x;

        if (Pixels[index] == swatch)
            return;

        Pixels[index] = swatch;
        Version++;
    }

    /// <summary>Fills the area of one swatch around a pixel. Only up, down, left and right connect.</summary>
    public void Fill(int x, int y, byte swatch)
    {
        if (!InBounds(x, y))
            return;

        var target = SwatchAt(x, y);

        if (target == swatch)
            return;

        var stack = new Stack<(int X, int Y)>();
        stack.Push((x, y));

        while (stack.Count > 0)
        {
            var (px, py) = stack.Pop();

            if (!InBounds(px, py) || (SwatchAt(px, py) != target))
                continue;

            Pixels[(py * WIDTH) + px] = swatch;
            stack.Push((px + 1, py));
            stack.Push((px - 1, py));
            stack.Push((px, py + 1));
            stack.Push((px, py - 1));
        }

        Version++;
    }

    /// <summary>Changes a swatch's colour as one undo step; every pixel on that swatch changes with it.</summary>
    public void SetColour(int swatch, Color colour)
    {
        BeginStep();
        Palette[swatch] = new Color(colour.R, colour.G, colour.B);
        Version++;
        EndStep();
    }

    public void Undo()
    {
        if ((StepStart is not null) || !CanUndo)
            return;

        RedoSteps.Add(Snapshot());
        Restore(UndoSteps[^1]);
        UndoSteps.RemoveAt(UndoSteps.Count - 1);
    }

    public void Redo()
    {
        if ((StepStart is not null) || !CanRedo)
            return;

        UndoSteps.Add(Snapshot());
        Restore(RedoSteps[^1]);
        RedoSteps.RemoveAt(RedoSteps.Count - 1);
    }

    public void MarkClean() => IsDirty = false;

    public void MarkDirty() => IsDirty = true;

    private byte[] Snapshot()
    {
        var snapshot = new byte[Pixels.Length + (COLOURS * 3)];
        Pixels.CopyTo(snapshot, 0);
        PaletteBytes().CopyTo(snapshot, Pixels.Length);

        return snapshot;
    }

    private void Restore(byte[] snapshot)
    {
        snapshot.AsSpan(0, Pixels.Length).CopyTo(Pixels);

        for (var i = 0; i < COLOURS; i++)
        {
            var at = Pixels.Length + (i * 3);
            Palette[i] = new Color(snapshot[at], snapshot[at + 1], snapshot[at + 2]);
        }

        IsDirty = true;
        Version++;
    }
}
