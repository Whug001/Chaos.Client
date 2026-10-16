using System.Security.Cryptography;
using Chaos.Client.ViewModel.College;
using Microsoft.Xna.Framework;
using SkiaSharp;

namespace Chaos.Client.ViewModel.Portraits;

/// <summary>
///     A 48 x 56 full-colour portrait canvas: one <see cref="Color" /> per pixel, unlike College
///     <see cref="PixelDrawing" /> which stores 32 swatch indices. Edits made between <see cref="BeginStep" /> and
///     <see cref="EndStep" /> undo as one step; up to <see cref="MAX_UNDO" /> steps are kept.
/// </summary>
public sealed class PortraitCanvas
{
    //must match the server's PortraitProtocol.WIDTH/HEIGHT; the game never resizes
    public const int WIDTH = 48;
    public const int HEIGHT = 56;

    //Set sends the canvas re-encoded, never the uploaded file, so a real 48 x 56 file carrying editor metadata
    //may be far over the server's 16 KiB limit; this only stops a huge file being read whole
    public const int MAX_UPLOAD_FILE_BYTES = 1024 * 1024;
    public const int SWATCH_COUNT = 32;
    public const int MAX_UNDO = 50;

    private const string NOT_A_PICTURE = "That file is not a PNG or JPEG picture.";

    private readonly List<(Color[] Pixels, Color[] Swatches)> RedoSteps = [];
    private readonly List<(Color[] Pixels, Color[] Swatches)> UndoSteps = [];
    private (Color[] Pixels, Color[] Swatches)? StepStart;

    private PortraitCanvas(Color[] pixels, Color[] swatches)
    {
        Pixels = pixels;
        Swatches = swatches;
    }

    public static readonly Color Transparent = new(0, 0, 0, 0);

    public Color[] Pixels { get; }
    public Color[] Swatches { get; }

    /// <summary>Goes up on every change, so a texture knows when to redraw.</summary>
    public int Version { get; private set; }

    /// <summary>Changed since it was loaded or last saved.</summary>
    public bool IsDirty { get; private set; }

    public bool CanUndo => UndoSteps.Count > 0;
    public bool CanRedo => RedoSteps.Count > 0;

    /// <summary>When true, Set and Fill also paint the horizontally mirrored pixel.</summary>
    public bool Mirror { get; set; }

    /// <summary>A blank canvas is transparent, with the default swatches and no undo step recorded.</summary>
    public static PortraitCanvas Blank()
        => new(new Color[WIDTH * HEIGHT], ArtPalette.Default.ToArray());

    /// <summary>SHA-256 over the exact bytes, lowercase hex, the same hash the server answers with.</summary>
    public static string HashOf(byte[] bytes)
        => Convert.ToHexStringLower(SHA256.HashData(bytes));

    /// <summary>Decodes picture bytes onto a canvas. Empty string when exact; otherwise the refusal text.</summary>
    public static string TryFromBytes(byte[] bytes, out PortraitCanvas? canvas)
    {
        canvas = null;

        if (bytes.Length == 0)
            return NOT_A_PICTURE;

        try
        {
            using var image = SKImage.FromEncodedData(bytes);

            if (image is null)
                return NOT_A_PICTURE;

            if ((image.Width != WIDTH) || (image.Height != HEIGHT))
                return $"Portraits must be exactly 48 x 56 pixels. That file is {image.Width} x {image.Height}.";

            using var bitmap = SKBitmap.FromImage(image);
            var decoded = Blank();

            for (var y = 0; y < HEIGHT; y++)
                for (var x = 0; x < WIDTH; x++)
                {
                    var pixel = bitmap.GetPixel(x, y);
                    decoded.Pixels[(y * WIDTH) + x] = new Color(pixel.Red, pixel.Green, pixel.Blue, pixel.Alpha);
                }

            canvas = decoded;

            return string.Empty;
        } catch
        {
            return NOT_A_PICTURE;
        }
    }

    public bool Contains(int x, int y)
        => (x >= 0) && (y >= 0) && (x < WIDTH) && (y < HEIGHT);

    public Color ColorAt(int x, int y)
        => Contains(x, y) ? Pixels[(y * WIDTH) + x] : Transparent;

    /// <summary>Reads the exact pixel colour, including colours no swatch holds.</summary>
    public Color Pick(int x, int y)
        => ColorAt(x, y);

    public void BeginStep()
        => StepStart ??= Snapshot();

    public void EndStep()
    {
        if (StepStart is null)
            return;

        if (!StepStart.Value.Pixels.AsSpan().SequenceEqual(Pixels)
            || !StepStart.Value.Swatches.AsSpan().SequenceEqual(Swatches))
        {
            UndoSteps.Add(StepStart.Value);

            if (UndoSteps.Count > MAX_UNDO)
                UndoSteps.RemoveAt(0);

            RedoSteps.Clear();
            IsDirty = true;
        }

        StepStart = null;
    }

    public void Set(int x, int y, Color color)
    {
        if (!Contains(x, y))
            return;

        var index = (y * WIDTH) + x;
        var twin = Mirror ? (y * WIDTH) + Mirrored(x) : index;

        if ((Pixels[index] == color) && (Pixels[twin] == color))
            return;

        Pixels[index] = color;
        Pixels[twin] = color;
        Version++;
    }

    /// <summary>Fills the area of one colour around a pixel. Only up, down, left and right connect.</summary>
    public void Fill(int x, int y, Color color)
    {
        if (!Contains(x, y))
            return;

        var target = ColorAt(x, y);

        if (target != color)
        {
            var stack = new Stack<(int X, int Y)>();
            stack.Push((x, y));

            while (stack.Count > 0)
            {
                var (px, py) = stack.Pop();

                if (!Contains(px, py) || (ColorAt(px, py) != target))
                    continue;

                Pixels[(py * WIDTH) + px] = color;
                stack.Push((px + 1, py));
                stack.Push((px - 1, py));
                stack.Push((px, py + 1));
                stack.Push((px, py - 1));
            }

            Version++;
        }

        //the mirrored side is skipped when the first fill already reached it, which also ends the recursion
        if (Mirror)
        {
            var mirrorX = Mirrored(x);

            if ((mirrorX != x) && (ColorAt(mirrorX, y) != color))
                Fill(mirrorX, y, color);
        }
    }

    /// <summary>Changes a swatch's colour as one undo step. Painted pixels keep their own colour.</summary>
    public void SetSwatch(int swatch, Color color)
    {
        if ((swatch < 0) || (swatch >= SWATCH_COUNT))
            return;

        BeginStep();
        Swatches[swatch] = new Color(color.R, color.G, color.B);
        Version++;
        EndStep();
    }

    /// <summary>Empties the whole canvas to transparent, as one undo step.</summary>
    public void Clear()
    {
        BeginStep();

        for (var i = 0; i < Pixels.Length; i++)
            Pixels[i] = Transparent;

        Version++;
        EndStep();
    }

    /// <summary>Takes the canvas over: decoded pixels plus swatches, with the undo history dropped.</summary>
    public void CopyFrom(PortraitCanvas source)
    {
        source.Pixels.CopyTo(Pixels, 0);
        source.Swatches.CopyTo(Swatches, 0);
        UndoSteps.Clear();
        RedoSteps.Clear();
        StepStart = null;
        IsDirty = false;
        Version++;
    }

    /// <summary>Sets the 32 swatches to the most-used pixel colours in frequency order.</summary>
    public void FillSwatchesFrom()
    {
        var firstSeen = new Dictionary<Color, int>();
        var counts = new Dictionary<Color, int>();

        foreach (var pixel in Pixels)
        {
            if (!firstSeen.ContainsKey(pixel))
                firstSeen[pixel] = firstSeen.Count;

            counts[pixel] = counts.TryGetValue(pixel, out var count) ? count + 1 : 1;
        }

        var top = counts.Keys.OrderByDescending(c => counts[c]).ThenBy(c => firstSeen[c]).Take(SWATCH_COUNT).ToList();
        var defaults = ArtPalette.Default;

        for (var i = 0; i < SWATCH_COUNT; i++)
            Swatches[i] = i < top.Count ? top[i] : defaults[i];

        Version++;
    }

    /// <summary>Loads an upload onto the canvas and refills the swatches. Empty string when exact; otherwise the refusal text, with the old pixels kept.</summary>
    public string LoadUpload(byte[] file)
    {
        var error = TryFromBytes(file, out var decoded);

        if ((error.Length > 0) || (decoded is null))
            return error;

        BeginStep();
        decoded.Pixels.CopyTo(Pixels, 0);
        FillSwatchesFrom();
        EndStep();

        return string.Empty;
    }

    public byte[] ToPng()
    {
        using var bitmap = new SKBitmap(WIDTH, HEIGHT, SKColorType.Rgba8888, SKAlphaType.Unpremul);

        for (var y = 0; y < HEIGHT; y++)
            for (var x = 0; x < WIDTH; x++)
            {
                var pixel = Pixels[(y * WIDTH) + x];
                bitmap.SetPixel(x, y, new SKColor(pixel.R, pixel.G, pixel.B, pixel.A));
            }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);

        return data.ToArray();
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

    public void MarkClean()
        => IsDirty = false;

    public void MarkDirty()
        => IsDirty = true;

    private int Mirrored(int x)
        => WIDTH - 1 - x;

    private (Color[] Pixels, Color[] Swatches) Snapshot()
        => (Pixels.ToArray(), Swatches.ToArray());

    private void Restore((Color[] Pixels, Color[] Swatches) snapshot)
    {
        snapshot.Pixels.CopyTo(Pixels, 0);
        snapshot.Swatches.CopyTo(Swatches, 0);
        IsDirty = true;
        Version++;
    }
}
