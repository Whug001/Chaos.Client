using Chaos.DarkAges.Definitions;

namespace Chaos.Client.ViewModel.College;

/// <summary>
///     The composer's editable tune. Every edit keeps the notes inside the grid with no overlaps in a row, and undoes
///     as one step; a placed note and its drag are one step. Up to <see cref="UNDO_LIMIT" /> steps are kept.
/// </summary>
public sealed class TuneDocument
{
    public const int UNDO_LIMIT = 50;
    private const int HALF = CollegeProtocol.TUNE_STEPS / 2;

    private readonly List<TuneNote> NoteList = [];
    private readonly List<TuneData> RedoSteps = [];
    private readonly List<TuneData> UndoSteps = [];
    private int PlacingIndex = -1;
    private TuneData? PlaceStart;

    public TuneScale Scale { get; private set; } = TuneData.Empty.Scale;
    public TuneSpeed Speed { get; private set; } = TuneData.Empty.Speed;
    public TuneInstrument Instrument { get; private set; } = TuneData.Empty.Instrument;
    public IReadOnlyList<TuneNote> Notes => NoteList;

    /// <summary>Goes up on every change, including each drag step, so a grid knows when to redraw.</summary>
    public int Version { get; private set; }

    /// <summary>Raised each time <see cref="Version" /> goes up.</summary>
    public event Action? Changed;

    /// <summary>Changed since it was loaded or last saved.</summary>
    public bool IsDirty { get; private set; }

    public bool CanUndo => UndoSteps.Count > 0;
    public bool CanRedo => RedoSteps.Count > 0;
    public bool IsPlacing => PlacingIndex >= 0;

    public int NoteCount => NoteList.Count;
    public int MelodyNotes => NoteList.Count(n => n.Layer == TuneLayer.Melody);
    public int MelodyRows => NoteList.Where(n => n.Layer == TuneLayer.Melody).Select(n => n.Row).Distinct().Count();

    public TuneNote? NoteAt(TuneLayer layer, int row, int column)
    {
        var index = IndexAt(layer, row, column);

        return index < 0 ? null : NoteList[index];
    }

    /// <summary>Starts a one-column note on an empty cell. False if the cell is filled, outside the grid, or the tune is full.</summary>
    public bool BeginPlace(TuneLayer layer, int row, int column)
    {
        var note = new TuneNote(layer, row, column, 1);

        if (IsPlacing || !note.IsInGrid || (IndexAt(layer, row, column) >= 0) || (NoteList.Count >= CollegeProtocol.MAX_TUNE_NOTES))
            return false;

        PlaceStart = Snapshot();
        NoteList.Add(note);
        PlacingIndex = NoteList.Count - 1;
        Bump();

        return true;
    }

    /// <summary>Lengthens the note being placed to reach a column, up to the next note in its row or the grid's end.</summary>
    public void DragTo(int column)
    {
        if (!IsPlacing)
            return;

        var note = NoteList[PlacingIndex];

        if (note.Layer == TuneLayer.Drums)
            return;

        var limit = NoteList.Where(n => (n.Layer == note.Layer) && (n.Row == note.Row) && (n.Start > note.Start))
                            .Select(n => n.Start)
                            .DefaultIfEmpty(CollegeProtocol.TUNE_STEPS)
                            .Min();

        var length = Math.Clamp(column - note.Start + 1, 1, limit - note.Start);

        if (length == note.Length)
            return;

        NoteList[PlacingIndex] = note with { Length = length };
        Bump();
    }

    public void EndPlace()
    {
        if (!IsPlacing)
            return;

        PlacingIndex = -1;
        Commit(PlaceStart!);
        PlaceStart = null;
    }

    public bool Remove(TuneLayer layer, int row, int column)
    {
        var index = IndexAt(layer, row, column);

        if (IsPlacing || (index < 0))
            return false;

        var before = Snapshot();
        NoteList.RemoveAt(index);
        Commit(before);

        return true;
    }

    /// <summary>Replaces bars 5-8 of one layer with a copy of bars 1-4. A note crossing the middle is cut there first.</summary>
    public void CopyFirstHalf(TuneLayer layer)
    {
        if (IsPlacing)
            return;

        var before = Snapshot();
        NoteList.RemoveAll(n => (n.Layer == layer) && (n.Start >= HALF));

        for (var i = 0; i < NoteList.Count; i++)
            if ((NoteList[i].Layer == layer) && (NoteList[i].End > HALF))
                NoteList[i] = NoteList[i] with { Length = HALF - NoteList[i].Start };

        var copies = NoteList.Where(n => n.Layer == layer).Select(n => n with { Start = n.Start + HALF }).ToList();

        foreach (var copy in copies)
        {
            if (NoteList.Count >= CollegeProtocol.MAX_TUNE_NOTES)
                break;

            NoteList.Add(copy);
        }

        Commit(before);
    }

    public void Clear(TuneLayer layer)
    {
        if (IsPlacing)
            return;

        var before = Snapshot();
        NoteList.RemoveAll(n => n.Layer == layer);
        Commit(before);
    }

    public void SetScale(TuneScale scale) => Change(() => Scale = scale);

    public void SetSpeed(TuneSpeed speed) => Change(() => Speed = speed);

    public void SetInstrument(TuneInstrument instrument) => Change(() => Instrument = instrument);

    public bool Undo()
    {
        if (IsPlacing || !CanUndo)
            return false;

        RedoSteps.Add(Snapshot());
        Restore(UndoSteps[^1]);
        UndoSteps.RemoveAt(UndoSteps.Count - 1);
        IsDirty = true;
        Bump();

        return true;
    }

    public bool Redo()
    {
        if (IsPlacing || !CanRedo)
            return false;

        UndoSteps.Add(Snapshot());
        Restore(RedoSteps[^1]);
        RedoSteps.RemoveAt(RedoSteps.Count - 1);
        IsDirty = true;
        Bump();

        return true;
    }

    public TuneData Snapshot() => new(Scale, Speed, Instrument, NoteList.ToArray());

    /// <summary>Replaces the whole tune, clearing undo and the dirty flag.</summary>
    public void Load(TuneData data)
    {
        PlacingIndex = -1;
        PlaceStart = null;
        Restore(data);
        UndoSteps.Clear();
        RedoSteps.Clear();
        IsDirty = false;
        Bump();
    }

    public void MarkClean() => IsDirty = false;

    private int IndexAt(TuneLayer layer, int row, int column) => NoteList.FindIndex(n => n.Covers(layer, row, column));

    private void Bump()
    {
        Version++;
        Changed?.Invoke();
    }

    private void Change(Action apply)
    {
        if (IsPlacing)
            return;

        var before = Snapshot();
        apply();
        Commit(before);
    }

    private void Commit(TuneData before)
    {
        if ((before.Scale == Scale) && (before.Speed == Speed) && (before.Instrument == Instrument) && before.Notes.SequenceEqual(NoteList))
            return;

        UndoSteps.Add(before);

        if (UndoSteps.Count > UNDO_LIMIT)
            UndoSteps.RemoveAt(0);

        RedoSteps.Clear();
        IsDirty = true;
        Bump();
    }

    private void Restore(TuneData data)
    {
        Scale = data.Scale;
        Speed = data.Speed;
        Instrument = data.Instrument;
        NoteList.Clear();
        NoteList.AddRange(data.Notes);
    }
}
