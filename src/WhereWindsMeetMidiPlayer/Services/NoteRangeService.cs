using WhereWindsMeetMidiPlayer.Infrastructure;
using WhereWindsMeetMidiPlayer.Models;

namespace WhereWindsMeetMidiPlayer.Services;

public sealed class NoteRangeService
{
    public NoteRangeResult ApplyRange(IReadOnlyList<NormalizedNote> notes, bool smartTranspose, bool strictMode)
    {
        var output = new List<NormalizedNote>(notes.Count);
        var outOfRange = 0;
        var skipped = 0;

        foreach (var note in notes)
        {
            var adjusted = new NormalizedNote
            {
                OriginalNoteNumber = note.OriginalNoteNumber,
                NoteName = note.NoteName,
                StartMs = note.StartMs,
                DurationMs = note.DurationMs,
                Velocity = note.Velocity,
                TrackIndex = note.TrackIndex,
                Channel = note.Channel,
                NoteNumber = note.NoteNumber
            };

            if (IsInRange(adjusted.NoteNumber))
            {
                adjusted.NoteName = NoteNames.FromMidiNumber(adjusted.NoteNumber);
                output.Add(adjusted);
                continue;
            }

            outOfRange++;

            if (strictMode)
            {
                adjusted.Skipped = true;
                skipped++;
                output.Add(adjusted);
                continue;
            }

            if (!smartTranspose)
            {
                adjusted.Skipped = true;
                skipped++;
                output.Add(adjusted);
                continue;
            }

            var transposed = TryTransposeIntoRange(adjusted.NoteNumber);
            if (transposed is null)
            {
                adjusted.Skipped = true;
                skipped++;
                output.Add(adjusted);
                continue;
            }

            adjusted.NoteNumber = transposed.Value;
            adjusted.NoteName = NoteNames.FromMidiNumber(adjusted.NoteNumber);
            output.Add(adjusted);
        }

        return new NoteRangeResult
        {
            Notes = output.Where(n => !n.Skipped).ToList(),
            OutOfRangeNoteCount = outOfRange,
            SkippedNoteCount = skipped
        };
    }

    private static bool IsInRange(int noteNumber) =>
        noteNumber >= NoteNames.MinGameNote && noteNumber <= NoteNames.MaxGameNote;

    private static int? TryTransposeIntoRange(int noteNumber)
    {
        var current = noteNumber;
        while (current > NoteNames.MaxGameNote)
        {
            current -= 12;
            if (IsInRange(current))
                return current;
        }

        current = noteNumber;
        while (current < NoteNames.MinGameNote)
        {
            current += 12;
            if (IsInRange(current))
                return current;
        }

        return null;
    }
}

public sealed class NoteRangeResult
{
    public IReadOnlyList<NormalizedNote> Notes { get; init; } = [];
    public int OutOfRangeNoteCount { get; init; }
    public int SkippedNoteCount { get; init; }
}
