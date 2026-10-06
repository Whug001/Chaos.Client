using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;

namespace Chaos.Client.ViewModel.College;

/// <summary>A finished tune that never changes: what is saved, sent, drawn read-only and played.</summary>
public sealed record TuneData(TuneScale Scale, TuneSpeed Speed, TuneInstrument Instrument, IReadOnlyList<TuneNote> Notes)
{
    public static readonly TuneData Empty = new(TuneScale.Major, TuneSpeed.Steady, TuneInstrument.Lute, []);

    /// <summary>Notes outside the grid are dropped, and bytes after the last whole note are ignored.</summary>
    public static TuneData From(CollegeBlockInfo block)
    {
        var notes = new List<TuneNote>();
        var bytes = block.Notes;

        for (var at = 0; at + CollegeProtocol.TUNE_NOTE_BYTES <= bytes.Length; at += CollegeProtocol.TUNE_NOTE_BYTES)
        {
            var note = new TuneNote((TuneLayer)bytes[at], bytes[at + 1], bytes[at + 2], bytes[at + 3]);

            if (note.IsInGrid)
                notes.Add(note);
        }

        return new TuneData(block.Scale, block.Speed, block.Instrument, notes);
    }

    public CollegeBlockInfo ToBlock()
    {
        var bytes = new byte[Notes.Count * CollegeProtocol.TUNE_NOTE_BYTES];

        for (var i = 0; i < Notes.Count; i++)
        {
            var at = i * CollegeProtocol.TUNE_NOTE_BYTES;
            bytes[at] = (byte)Notes[i].Layer;
            bytes[at + 1] = (byte)Notes[i].Row;
            bytes[at + 2] = (byte)Notes[i].Start;
            bytes[at + 3] = (byte)Notes[i].Length;
        }

        return new CollegeBlockInfo
        {
            Kind = CollegeBlockKind.Tune,
            Scale = Scale,
            Speed = Speed,
            Instrument = Instrument,
            Notes = bytes
        };
    }
}
