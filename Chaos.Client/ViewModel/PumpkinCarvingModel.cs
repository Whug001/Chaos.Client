#region
using Chaos.DarkAges.Definitions;
#endregion

namespace Chaos.Client.ViewModel;

public enum PumpkinTool
{
    Knife,
    Eraser
}

/// <summary>
///     The Pumpkin Carving window's state: the grid, the tool, mirror, undo and redo, and whether the grid changed since it
///     was last sent. No graphics, so it is tested directly.
/// </summary>
public sealed class PumpkinCarvingModel
{
    private const int MAX_UNDO = 50;

    /// <summary>How often the window saves while the grid has changed, so the audience sees the carving almost live.</summary>
    public const long SAVE_INTERVAL_MS = 600;

    private readonly List<byte[]> RedoSteps = [];
    private readonly List<byte[]> UndoSteps = [];
    private byte[]? StrokeStart;

    public bool CanRedo => RedoSteps.Count > 0;
    public bool CanUndo => UndoSteps.Count > 0;
    public int CutCount => PumpkinGrid.CountCut(Grid);
    public byte[] Grid { get; private set; } = PumpkinGrid.Empty();
    public bool HasUnsent { get; private set; }
    public bool Mirror { get; set; }
    public PumpkinTool Tool { get; set; } = PumpkinTool.Knife;
    public int Version { get; private set; }

    /// <summary>Cuts with the knife, fills with the eraser; <paramref name="erase" /> (a right-click) always fills.</summary>
    public void Apply(int x, int y, bool erase)
    {
        if (((uint)x >= PumpkinGrid.WIDTH) || ((uint)y >= PumpkinGrid.HEIGHT))
            return;

        var cut = !erase && (Tool == PumpkinTool.Knife);
        var changed = Set(x, y, cut);

        if (Mirror)
            changed |= Set(MirrorX(x), y, cut);

        if (changed)
            Changed();
    }

    public void BeginStroke() => StrokeStart = Grid.ToArray();

    public void Clear()
    {
        if (CutCount == 0)
            return;

        Record(UndoSteps, Grid);
        RedoSteps.Clear();
        Grid = PumpkinGrid.Empty();
        Changed();
    }

    public void EndStroke()
    {
        if (StrokeStart is null)
            return;

        if (!StrokeStart.AsSpan().SequenceEqual(Grid))
        {
            Record(UndoSteps, StrokeStart);
            RedoSteps.Clear();
        }

        StrokeStart = null;
    }

    public void Load(byte[] grid)
    {
        Grid = PumpkinGrid.IsValid(grid) ? grid.ToArray() : PumpkinGrid.Empty();
        UndoSteps.Clear();
        RedoSteps.Clear();
        StrokeStart = null;
        HasUnsent = false;
        Version++;
    }

    public static int MirrorX(int x) => PumpkinGrid.WIDTH - 1 - x;

    public void Redo()
    {
        if (!CanRedo)
            return;

        Record(UndoSteps, Grid);
        Grid = Pop(RedoSteps);
        Changed();
    }

    public byte[] TakeForSend()
    {
        HasUnsent = false;

        return Grid.ToArray();
    }

    public bool SaveDue(long nowMs, long lastSendMs) => HasUnsent && ((nowMs - lastSendMs) >= SAVE_INTERVAL_MS);

    /// <summary>The grid if it changed since the last send, or null when the server already has it.</summary>
    public byte[]? TakeUnsent() => HasUnsent ? TakeForSend() : null;

    public void Undo()
    {
        if (!CanUndo)
            return;

        Record(RedoSteps, Grid);
        Grid = Pop(UndoSteps);
        Changed();
    }

    private void Changed()
    {
        HasUnsent = true;
        Version++;
    }

    private static byte[] Pop(List<byte[]> steps)
    {
        var last = steps[^1];
        steps.RemoveAt(steps.Count - 1);

        return last;
    }

    private static void Record(List<byte[]> steps, byte[] grid)
    {
        steps.Add(grid.ToArray());

        if (steps.Count > MAX_UNDO)
            steps.RemoveAt(0);
    }

    private bool Set(int x, int y, bool cut)
    {
        if (PumpkinGrid.IsCut(Grid, x, y) == cut)
            return false;

        PumpkinGrid.SetCut(Grid, x, y, cut);

        return true;
    }
}
