using Chaos.DarkAges.Definitions;
using Microsoft.Xna.Framework;

namespace Chaos.Client.ViewModel.College;

public enum SketchTool
{
    Pen,
    BigPen,
    Fill,
    Erase
}

/// <summary>
///     The Sketch Guess drawing tools over an 8-colour canvas: index 0 is the white background and doubles as the
///     eraser. Pen paints one pixel, Big pen paints a 3 x 3 block, Fill floods and Erase paints 0.
/// </summary>
public sealed class SketchToolbox
{
    private readonly ArtEditor Editor;

    public SketchToolbox()
    {
        Drawing = PixelDrawing.From(PaletteBytes(), new byte[CollegeProtocol.DRAWING_PIXELS]);
        Editor = new ArtEditor(Drawing);
    }

    /// <summary>The 8 <see cref="CollegeProtocol.SKETCH_PALETTE" /> colours, index 0 white first.</summary>
    public static IReadOnlyList<Color> Colours { get; } = CollegeProtocol.SKETCH_PALETTE.Select(ToColor).ToArray();

    public PixelDrawing Drawing { get; }
    public SketchTool Tool { get; set; } = SketchTool.Pen;

    /// <summary>The selected colour's index, 0 to 7.</summary>
    public byte Colour { get; set; } = 1;

    public bool CanUndo => Drawing.CanUndo;

    public void Press(int x, int y)
    {
        Prepare();
        Editor.Press(x, y, false);
    }

    public void Drag(int x, int y) => Editor.Drag(x, y);

    public void Release(int x, int y) => Editor.Release(x, y);

    public void Undo() => Editor.Undo();

    /// <summary>Empties the whole canvas, as one undo step.</summary>
    public void Clear() => Editor.ClearAll();

    public byte[] Pixels() => Drawing.Pixels.ToArray();

    private void Prepare()
    {
        if (Editor.IsPressing)
            return;

        var colour = Colour < CollegeProtocol.SKETCH_COLOURS ? Colour : (byte)0;

        switch (Tool)
        {
            case SketchTool.Pen:
                Editor.Tool = ArtTool.Pen;
                SetBrush(1);
                Editor.Current = colour;

                break;
            case SketchTool.BigPen:
                Editor.Tool = ArtTool.Pen;
                SetBrush(3);
                Editor.Current = colour;

                break;
            case SketchTool.Fill:
                Editor.Tool = ArtTool.Fill;
                Editor.Current = colour;

                break;
            default:
                Editor.Tool = ArtTool.Eraser;
                SetBrush(1);

                break;
        }
    }

    private void SetBrush(int size)
    {
        while (Editor.BrushSize != size)
            Editor.CycleBrush();
    }

    private static byte[] PaletteBytes()
    {
        var bytes = new byte[CollegeProtocol.SKETCH_COLOURS * 3];

        for (var i = 0; i < CollegeProtocol.SKETCH_COLOURS; i++)
        {
            var colour = CollegeProtocol.SKETCH_PALETTE[i];
            bytes[i * 3] = (byte)(colour >> 16);
            bytes[(i * 3) + 1] = (byte)(colour >> 8);
            bytes[(i * 3) + 2] = (byte)colour;
        }

        return bytes;
    }

    private static Color ToColor(int colour) => new((byte)(colour >> 16), (byte)(colour >> 8), (byte)colour);
}
