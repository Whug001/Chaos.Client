#region
using Chaos.Client.Rendering;
using Chaos.DarkAges.Definitions;
#endregion

namespace Chaos.Client.ViewModel;

/// <summary>The guild cloak editor's paint tools.</summary>
public enum GuildCloakTool
{
    Pencil,
    Fill,
    Pick
}

/// <summary>
///     The guild cloak editor's state: the design being painted, the chosen color and tool, mirror painting, and undo and
///     redo. It only paints cells inside the cloak's outline. <see cref="Version" /> changes on every visible change, so the
///     window knows when to redraw.
/// </summary>
public sealed class GuildCloakEditorModel
{
    public const int MAX_UNDO = 50;

    private readonly Action<byte[]>? FillHiddenLining;
    private readonly Func<GuildCloakPart, int, int, bool> IsPaintable;
    private readonly List<GuildCloakDesign> RedoSteps = [];
    private readonly List<GuildCloakDesign> UndoSteps = [];
    private GuildCloakDesign? StrokeStart;
    private bool StrokeRecorded;

    /// <param name="isPaintable">Whether a cell lies inside the cloak's outline (its reference frame has a pixel there).</param>
    /// <param name="fillHiddenLining">
    ///     Recolors the lining cells the collar hides from the lining around them. Run after every change and on load, so
    ///     the design that is saved and submitted never shows an unpainted strip.
    /// </param>
    public GuildCloakEditorModel(Func<GuildCloakPart, int, int, bool> isPaintable, Action<byte[]>? fillHiddenLining = null)
    {
        IsPaintable = isPaintable;
        FillHiddenLining = fillHiddenLining;
    }

    public bool CanRedo => RedoSteps.Count > 0;
    public bool CanUndo => UndoSteps.Count > 0;
    public GuildCloakDesign Design { get; private set; } = GuildCloakDesign.CreateDefault();
    public bool IsDirty { get; private set; }
    public bool Mirror { get; set; }
    public int SelectedColor { get; private set; } = 1;
    public GuildCloakTool Tool { get; set; } = GuildCloakTool.Pencil;
    public int Version { get; private set; }

    /// <summary>Adds a color and selects it. False when the design already has <see cref="GuildCloakProtocol.MAX_COLORS" />.</summary>
    public bool AddColor(GuildCloakColor color)
    {
        if (Design.Colors.Count >= GuildCloakProtocol.MAX_COLORS)
            return false;

        Record();
        Design.Colors.Add(color);
        SelectedColor = Design.Colors.Count;
        Changed();

        return true;
    }

    /// <summary>
    ///     Uses the current tool on a cell. Pencil and fill change the design, and the mirrored cell too when mirror is on;
    ///     pick selects the cell's color.
    /// </summary>
    public void Apply(GuildCloakPart part, int x, int y)
    {
        if (!CanPaint(part, x, y))
            return;

        switch (Tool)
        {
            case GuildCloakTool.Pencil:
                SetCell(part, x, y);

                if (Mirror)
                    SetCell(part, MirrorX(part, x, y), y);

                break;

            case GuildCloakTool.Fill:
                Fill(part, x, y);

                if (Mirror)
                    Fill(part, MirrorX(part, x, y), y);

                break;

            case GuildCloakTool.Pick:
                SelectColor(CellAt(part, x, y));

                break;
        }
    }

    /// <summary>Starts a stroke: every change until <see cref="EndStroke" /> undoes as one step.</summary>
    public void BeginStroke()
    {
        StrokeStart = Design.DeepCopy();
        StrokeRecorded = false;
    }

    public byte CellAt(GuildCloakPart part, int x, int y) => GuildCloakReferences.Cells(Design, part)[(y * WidthOf(part)) + x];

    public void EndStroke() => StrokeStart = null;

    public static int HeightOf(GuildCloakPart part)
        => part switch
        {
            GuildCloakPart.Back   => GuildCloakProtocol.BACK_HEIGHT,
            GuildCloakPart.Lining => GuildCloakProtocol.LINING_HEIGHT,
            _                     => GuildCloakProtocol.COLLAR_HEIGHT
        };

    /// <summary>
    ///     Starts over from a design sent by the server: no history, color 1 selected, nothing unsaved. The hidden lining
    ///     cells are refilled without counting as a change.
    /// </summary>
    public void Load(GuildCloakDesign design)
    {
        Design = design.DeepCopy();
        FillHiddenLining?.Invoke(Design.Lining);
        SelectedColor = 1;
        UndoSteps.Clear();
        RedoSteps.Clear();
        StrokeStart = null;
        IsDirty = false;
        Version++;
    }

    /// <summary>
    ///     Loads the guild's saved design, unless the window is already open with unsaved painting, which is kept. The server
    ///     sends a saved design each time the leader asks Quill for the editor, even while it is open.
    /// </summary>
    /// <returns><c>true</c> if the saved design was loaded.</returns>
    public bool LoadSaved(GuildCloakDesign saved, bool windowOpen)
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

    public void Redo()
    {
        if (RedoSteps.Count == 0)
            return;

        UndoSteps.Add(Design);
        Design = Pop(RedoSteps);
        AfterHistoryStep();
    }

    public void SelectColor(int number)
    {
        if ((number < 1) || (number > Design.Colors.Count) || (number == SelectedColor))
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

    /// <summary>The editor's status line. The reason shows only for a rejection.</summary>
    public static string StatusText(GuildCloakStatus status, string reason, bool dirty)
    {
        var text = status switch
        {
            GuildCloakStatus.Waiting  => "Waiting for review",
            GuildCloakStatus.Approved => "Approved",
            GuildCloakStatus.Rejected => $"Rejected: {reason}",
            _                         => "Draft"
        };

        return dirty ? $"{text} - unsaved changes" : text;
    }

    public void Undo()
    {
        if (UndoSteps.Count == 0)
            return;

        RedoSteps.Add(Design);
        Design = Pop(UndoSteps);
        AfterHistoryStep();
    }

    public static int WidthOf(GuildCloakPart part)
        => part switch
        {
            GuildCloakPart.Back   => GuildCloakProtocol.BACK_WIDTH,
            GuildCloakPart.Lining => GuildCloakProtocol.LINING_WIDTH,
            _                     => GuildCloakProtocol.COLLAR_WIDTH
        };

    private void AfterHistoryStep()
    {
        StrokeStart = null;
        SelectedColor = Math.Clamp(SelectedColor, 1, Design.Colors.Count);
        IsDirty = true;
        Version++;
    }

    private bool CanPaint(GuildCloakPart part, int x, int y)
        => (x >= 0) && (y >= 0) && (x < WidthOf(part)) && (y < HeightOf(part)) && IsPaintable(part, x, y);

    private void Changed()
    {
        FillHiddenLining?.Invoke(Design.Lining);
        IsDirty = true;
        Version++;
    }

    private void Fill(GuildCloakPart part, int x, int y)
    {
        if (!CanPaint(part, x, y))
            return;

        var cells = GuildCloakReferences.Cells(Design, part);
        var width = WidthOf(part);
        var from = cells[(y * width) + x];

        if (from == SelectedColor)
            return;

        Record();
        var pending = new Stack<(int X, int Y)>();
        pending.Push((x, y));

        while (pending.Count > 0)
        {
            (var cx, var cy) = pending.Pop();

            if (!CanPaint(part, cx, cy) || (cells[(cy * width) + cx] != from))
                continue;

            cells[(cy * width) + cx] = (byte)SelectedColor;
            pending.Push((cx + 1, cy));
            pending.Push((cx - 1, cy));
            pending.Push((cx, cy + 1));
            pending.Push((cx, cy - 1));
        }

        Changed();
    }

    //the painter places a cell by where it sits between its row's first and last outline cell, so a mirrored cell is
    //flipped within that span; the angled reference frames' rows are not centered in the grid
    private int MirrorX(GuildCloakPart part, int x, int y)
    {
        var width = WidthOf(part);
        var first = 0;
        var last = width - 1;

        while ((first < width) && !IsPaintable(part, first, y))
            first++;

        while ((last >= 0) && !IsPaintable(part, last, y))
            last--;

        return first + last - x;
    }

    private static GuildCloakDesign Pop(List<GuildCloakDesign> steps)
    {
        var last = steps[^1];
        steps.RemoveAt(steps.Count - 1);

        return last;
    }

    /// <summary>Saves the design as an undo step before it changes: once per stroke, or once per change outside a stroke.</summary>
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

    private void SetCell(GuildCloakPart part, int x, int y)
    {
        if (!CanPaint(part, x, y))
            return;

        var cells = GuildCloakReferences.Cells(Design, part);
        var index = (y * WidthOf(part)) + x;

        if (cells[index] == SelectedColor)
            return;

        Record();
        cells[index] = (byte)SelectedColor;
        Changed();
    }
}
