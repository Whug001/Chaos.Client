using Chaos.Client.ViewModel.College;
using Chaos.DarkAges.Definitions;
using FluentAssertions;

namespace Chaos.Client.Tests.College;

public class TuneDocumentTests
{
    private static TuneDocument Document(params TuneNote[] notes)
    {
        var document = new TuneDocument();
        document.Load(TuneData.Empty with { Notes = notes });

        return document;
    }

    private static void Place(TuneDocument document, TuneLayer layer, int row, int column, int dragTo = -1)
    {
        document.BeginPlace(layer, row, column).Should().BeTrue();

        if (dragTo >= 0)
            document.DragTo(dragTo);

        document.EndPlace();
    }

    [Test]
    public void A_placed_note_is_one_column_and_undoes_as_one_step()
    {
        var document = new TuneDocument();

        document.BeginPlace(TuneLayer.Melody, 3, 5).Should().BeTrue();
        document.CanUndo.Should().BeFalse();
        document.DragTo(8);
        document.EndPlace();

        document.Notes.Should().Equal(new TuneNote(TuneLayer.Melody, 3, 5, 4));
        document.IsDirty.Should().BeTrue();
        document.Undo().Should().BeTrue();
        document.Notes.Should().BeEmpty();
        document.CanUndo.Should().BeFalse();
    }

    [Test]
    public void Changed_is_raised_each_time_the_version_goes_up()
    {
        var document = new TuneDocument();
        var raised = 0;
        document.Changed += () => raised++;

        Place(document, TuneLayer.Melody, 3, 5, dragTo: 7);
        document.Undo();
        document.Load(TuneData.Empty);

        raised.Should().Be(document.Version);
        raised.Should().BeGreaterThan(3);
    }

    [Test]
    public void A_drag_stops_before_the_next_note_in_the_row()
    {
        var document = Document(new TuneNote(TuneLayer.Melody, 3, 10, 2));

        Place(document, TuneLayer.Melody, 3, 4, 20);

        document.NoteAt(TuneLayer.Melody, 3, 4).Should().Be(new TuneNote(TuneLayer.Melody, 3, 4, 6));
    }

    [Test]
    public void A_drag_stops_at_the_end_of_the_grid()
    {
        var document = new TuneDocument();

        Place(document, TuneLayer.Bass, 0, 60, 80);

        document.Notes.Should().Equal(new TuneNote(TuneLayer.Bass, 0, 60, 4));
    }

    [Test]
    public void A_drag_to_the_left_keeps_one_column()
    {
        var document = new TuneDocument();

        Place(document, TuneLayer.Melody, 0, 20, 5);

        document.Notes.Should().Equal(new TuneNote(TuneLayer.Melody, 0, 20, 1));
    }

    [Test]
    public void Drum_notes_stay_one_column()
    {
        var document = new TuneDocument();

        Place(document, TuneLayer.Drums, 1, 0, 10);

        document.Notes.Should().Equal(new TuneNote(TuneLayer.Drums, 1, 0, 1));
    }

    [Test]
    public void A_filled_cell_or_a_cell_outside_the_grid_is_refused()
    {
        var document = Document(new TuneNote(TuneLayer.Melody, 2, 8, 4));

        document.BeginPlace(TuneLayer.Melody, 2, 10).Should().BeFalse();
        document.BeginPlace(TuneLayer.Melody, 15, 0).Should().BeFalse();
        document.BeginPlace(TuneLayer.Drums, 3, 0).Should().BeFalse();
        document.BeginPlace(TuneLayer.Melody, 0, 64).Should().BeFalse();
        document.Notes.Should().HaveCount(1);
    }

    [Test]
    public void The_513th_note_is_refused()
    {
        var notes = new List<TuneNote>();

        for (var row = 0; row < 15; row++)
            for (var start = 0; start < 64; start++)
                if (notes.Count < CollegeProtocol.MAX_TUNE_NOTES)
                    notes.Add(new TuneNote(row < 8 ? TuneLayer.Melody : TuneLayer.Bass, row, start, 1));

        var document = Document(notes.ToArray());

        document.NoteCount.Should().Be(512);
        document.BeginPlace(TuneLayer.Drums, 0, 0).Should().BeFalse();
    }

    [Test]
    public void Remove_takes_the_note_under_a_cell()
    {
        var document = Document(new TuneNote(TuneLayer.Bass, 4, 10, 6));

        document.Remove(TuneLayer.Bass, 4, 3).Should().BeFalse();
        document.Remove(TuneLayer.Bass, 4, 15).Should().BeTrue();

        document.Notes.Should().BeEmpty();
        document.Undo();
        document.Notes.Should().HaveCount(1);
    }

    [Test]
    public void Copy_first_half_cuts_at_the_middle_and_replaces_bars_5_to_8()
    {
        var document = Document(
            new TuneNote(TuneLayer.Melody, 1, 0, 4),
            new TuneNote(TuneLayer.Melody, 2, 30, 6),
            new TuneNote(TuneLayer.Melody, 5, 40, 2),
            new TuneNote(TuneLayer.Bass, 0, 40, 2));

        document.CopyFirstHalf(TuneLayer.Melody);

        document.Notes.Should().BeEquivalentTo(
        [
            new TuneNote(TuneLayer.Melody, 1, 0, 4),
            new TuneNote(TuneLayer.Melody, 2, 30, 2),
            new TuneNote(TuneLayer.Melody, 1, 32, 4),
            new TuneNote(TuneLayer.Melody, 2, 62, 2),
            new TuneNote(TuneLayer.Bass, 0, 40, 2)
        ]);
        document.Undo().Should().BeTrue();
        document.Notes.Should().HaveCount(4);
    }

    [Test]
    public void Clear_empties_one_layer()
    {
        var document = Document(new TuneNote(TuneLayer.Melody, 1, 0, 4), new TuneNote(TuneLayer.Drums, 0, 0, 1));

        document.Clear(TuneLayer.Melody);

        document.Notes.Should().Equal(new TuneNote(TuneLayer.Drums, 0, 0, 1));
    }

    [Test]
    public void Undo_keeps_fifty_steps()
    {
        var document = new TuneDocument();

        for (var column = 0; column < 51; column++)
            Place(document, TuneLayer.Melody, 0, column);

        var undone = 0;

        while (document.Undo())
            undone++;

        undone.Should().Be(TuneDocument.UNDO_LIMIT);
        document.Notes.Should().HaveCount(1);
    }

    [Test]
    public void A_new_edit_clears_redo()
    {
        var document = new TuneDocument();
        Place(document, TuneLayer.Melody, 0, 0);
        document.Undo();
        document.CanRedo.Should().BeTrue();

        Place(document, TuneLayer.Melody, 1, 0);

        document.CanRedo.Should().BeFalse();
    }

    [Test]
    public void A_scale_change_undoes_and_redoes()
    {
        var document = new TuneDocument();

        document.SetScale(TuneScale.Desert);
        document.SetSpeed(TuneSpeed.Quick);
        document.SetInstrument(TuneInstrument.Bells);

        document.Undo();
        document.Undo();
        document.Scale.Should().Be(TuneScale.Desert);
        document.Speed.Should().Be(TuneSpeed.Steady);
        document.Undo();
        document.Scale.Should().Be(TuneScale.Major);
        document.Redo().Should().BeTrue();
        document.Scale.Should().Be(TuneScale.Desert);
    }

    [Test]
    public void Setting_the_same_value_records_nothing()
    {
        var document = new TuneDocument();

        document.SetScale(TuneScale.Major);

        document.CanUndo.Should().BeFalse();
        document.IsDirty.Should().BeFalse();
    }

    [Test]
    public void Counts_cover_melody_notes_rows_and_all_notes()
    {
        var document = Document(
            new TuneNote(TuneLayer.Melody, 1, 0, 1),
            new TuneNote(TuneLayer.Melody, 1, 4, 1),
            new TuneNote(TuneLayer.Melody, 6, 8, 1),
            new TuneNote(TuneLayer.Bass, 6, 8, 1),
            new TuneNote(TuneLayer.Drums, 0, 8, 1));

        document.MelodyNotes.Should().Be(3);
        document.MelodyRows.Should().Be(2);
        document.NoteCount.Should().Be(5);
    }

    [Test]
    public void Load_clears_undo_and_the_dirty_flag()
    {
        var document = new TuneDocument();
        Place(document, TuneLayer.Melody, 0, 0);

        document.Load(TuneData.Empty);

        document.CanUndo.Should().BeFalse();
        document.IsDirty.Should().BeFalse();
        document.Notes.Should().BeEmpty();
    }

    [Test]
    public void Edits_wait_while_a_note_is_being_placed()
    {
        var document = new TuneDocument();
        document.BeginPlace(TuneLayer.Melody, 0, 0);

        document.Undo().Should().BeFalse();
        document.Remove(TuneLayer.Melody, 0, 0).Should().BeFalse();
        document.BeginPlace(TuneLayer.Melody, 1, 0).Should().BeFalse();
    }

    [Test]
    public void Changed_handlers_see_the_finished_state()
    {
        var document = new TuneDocument();
        Place(document, TuneLayer.Melody, 0, 0);

        var undoState = (CanUndo: true, CanRedo: true, IsDirty: true);
        var redoState = (CanUndo: true, CanRedo: true, IsDirty: true);
        var loadState = (CanUndo: true, CanRedo: true, IsDirty: true);
        var changeFired = 0;

        document.Changed += () =>
        {
            changeFired++;
            if (changeFired == 1)
                undoState = (document.CanUndo, document.CanRedo, document.IsDirty);
            else if (changeFired == 2)
                redoState = (document.CanUndo, document.CanRedo, document.IsDirty);
            else if (changeFired == 3)
                loadState = (document.CanUndo, document.CanRedo, document.IsDirty);
        };

        changeFired = 0;
        document.Undo();
        undoState.Should().Be((false, true, true));

        document.Redo();
        redoState.Should().Be((true, false, true));

        document.Load(TuneData.Empty);
        loadState.Should().Be((false, false, false));
    }

    [Test]
    public void Undoing_past_a_save_stays_dirty()
    {
        var document = new TuneDocument();
        Place(document, TuneLayer.Melody, 0, 0);
        document.IsDirty.Should().BeTrue();

        document.MarkClean();
        document.IsDirty.Should().BeFalse();

        document.Undo();
        document.IsDirty.Should().BeTrue();
    }
}
