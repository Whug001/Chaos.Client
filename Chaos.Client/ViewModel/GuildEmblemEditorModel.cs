#region
using Chaos.DarkAges.Definitions;
#endregion

namespace Chaos.Client.ViewModel;

/// <summary>
///     The guild emblem editor's state: the emblem being painted, the chosen color (0 = see-through, which erases), the
///     tool, mirror painting, and undo and redo. <see cref="Version" /> changes on every visible change, so the window knows
///     when to redraw. It uses the cloak editor's <see cref="GuildCloakTool" />s.
/// </summary>
public sealed class GuildEmblemEditorModel
{
    public const int MAX_UNDO = 50;
    private const int SIZE = GuildEmblemProtocol.SIZE;

    private readonly List<GuildEmblemDesign> RedoSteps = [];
    private readonly List<GuildEmblemDesign> UndoSteps = [];
    private GuildEmblemDesign? StrokeStart;
    private bool StrokeRecorded;

    public bool CanRedo => RedoSteps.Count > 0;
    public bool CanUndo => UndoSteps.Count > 0;
    public GuildEmblemDesign Design { get; private set; } = GuildEmblemDesign.CreateDefault();
    public bool IsDirty { get; private set; }
    public bool Mirror { get; set; }

    /// <summary>The color painted with: 1 up to the color count, or 0 for see-through.</summary>
    public int SelectedColor { get; private set; } = 1;

    public GuildCloakTool Tool { get; set; } = GuildCloakTool.Pencil;
    public int Version { get; private set; }

    /// <summary>Adds a color and selects it. False when the emblem already has <see cref="GuildEmblemProtocol.MAX_COLORS" />.</summary>
    public bool AddColor(GuildCloakColor color)
    {
        if (Design.Colors.Count >= GuildEmblemProtocol.MAX_COLORS)
            return false;

        Record();
        Design.Colors.Add(color);
        SelectedColor = Design.Colors.Count;
        Changed();

        return true;
    }

    /// <summary>Uses the current tool on a pixel. Pencil and fill change the emblem (and the mirrored pixel with mirror on); pick selects the pixel's color.</summary>
    public void Apply(int x, int y)
    {
        if (!InGrid(x, y))
            return;

        switch (Tool)
        {
            case GuildCloakTool.Pencil:
                SetCell(x, y);

                if (Mirror)
                    SetCell(MirrorX(x), y);

                break;

            case GuildCloakTool.Fill:
                Fill(x, y);

                if (Mirror)
                    Fill(MirrorX(x), y);

                break;

            case GuildCloakTool.Pick:
                SelectColor(CellAt(x, y));

                break;
        }
    }

    /// <summary>Starts a stroke: every change until <see cref="EndStroke" /> undoes as one step.</summary>
    public void BeginStroke()
    {
        StrokeStart = Design.DeepCopy();
        StrokeRecorded = false;
    }

    public byte CellAt(int x, int y) => Design.Pixels[(y * SIZE) + x];

    public void EndStroke() => StrokeStart = null;

    /// <summary>Starts over from an emblem sent by the server: no history, color 1 selected, nothing unsaved.</summary>
    public void Load(GuildEmblemDesign design)
    {
        Design = design.DeepCopy();
        SelectedColor = 1;
        UndoSteps.Clear();
        RedoSteps.Clear();
        StrokeStart = null;
        IsDirty = false;
        Version++;
    }

    /// <summary>Loads the guild's saved emblem, unless the window is already open with unsaved painting, which is kept.</summary>
    /// <returns><c>true</c> if the saved emblem was loaded.</returns>
    public bool LoadSaved(GuildEmblemDesign saved, bool windowOpen)
    {
        if (windowOpen && IsDirty)
            return false;

        Load(saved);

        return true;
    }

    public void MarkSaved()
    {
        IsDirty = false;
        Version++;
    }

    /// <summary>The column that mirrors <paramref name="x" /> across the emblem's middle column.</summary>
    public static int MirrorX(int x) => SIZE - 1 - x;

    public void Redo()
    {
        if (RedoSteps.Count == 0)
            return;

        UndoSteps.Add(Design);
        Design = Pop(RedoSteps);
        AfterHistoryStep();
    }

    /// <summary>Selects a color by number: 0 for see-through, or 1 up to the color count.</summary>
    public void SelectColor(int number)
    {
        if ((number < 0) || (number > Design.Colors.Count) || (number == SelectedColor))
            return;

        SelectedColor = number;
        Version++;
    }

    /// <summary>Changes one color. The color picker calls this while dragging, inside a stroke, so the whole drag undoes as one step.</summary>
    public void SetColor(int number, GuildCloakColor color)
    {
        if ((number < 1) || (number > Design.Colors.Count) || (Design.Colors[number - 1] == color))
            return;

        Record();
        Design.Colors[number - 1] = color;
        Changed();
    }

    public void Undo()
    {
        if (UndoSteps.Count == 0)
            return;

        RedoSteps.Add(Design);
        Design = Pop(UndoSteps);
        AfterHistoryStep();
    }

    private void AfterHistoryStep()
    {
        StrokeStart = null;
        SelectedColor = Math.Clamp(SelectedColor, 0, Design.Colors.Count);
        IsDirty = true;
        Version++;
    }

    private void Changed()
    {
        IsDirty = true;
        Version++;
    }

    private void Fill(int x, int y)
    {
        var from = CellAt(x, y);

        if (from == SelectedColor)
            return;

        Record();
        var pending = new Stack<(int X, int Y)>();
        pending.Push((x, y));

        while (pending.Count > 0)
        {
            (var cx, var cy) = pending.Pop();

            if (!InGrid(cx, cy) || (Design.Pixels[(cy * SIZE) + cx] != from))
                continue;

            Design.Pixels[(cy * SIZE) + cx] = (byte)SelectedColor;
            pending.Push((cx + 1, cy));
            pending.Push((cx - 1, cy));
            pending.Push((cx, cy + 1));
            pending.Push((cx, cy - 1));
        }

        Changed();
    }

    private static bool InGrid(int x, int y) => (x >= 0) && (y >= 0) && (x < SIZE) && (y < SIZE);

    private static GuildEmblemDesign Pop(List<GuildEmblemDesign> steps)
    {
        var last = steps[^1];
        steps.RemoveAt(steps.Count - 1);

        return last;
    }

    /// <summary>Saves the emblem as an undo step before it changes: once per stroke, or once per change outside a stroke.</summary>
    private void Record()
    {
        if (StrokeStart is not null)
        {
            if (StrokeRecorded)
                return;

            UndoSteps.Add(StrokeStart);
            StrokeRecorded = true;
        } else
            UndoSteps.Add(Design.DeepCopy());

        if (UndoSteps.Count > MAX_UNDO)
            UndoSteps.RemoveAt(0);

        RedoSteps.Clear();
    }

    private void SetCell(int x, int y)
    {
        var index = (y * SIZE) + x;

        if (Design.Pixels[index] == SelectedColor)
            return;

        Record();
        Design.Pixels[index] = (byte)SelectedColor;
        Changed();
    }
}
