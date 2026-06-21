using WhereWindsMeetMidiPlayer.Infrastructure;
using WhereWindsMeetMidiPlayer.Models;

namespace WhereWindsMeetMidiPlayer.Services;

/// <summary>
/// Global MIDI transpose (SnowiyQ detect_best_transpose), tuned for C3–B5 (48–83).
/// </summary>
public static class MidiTransposeService
{
    public static int DetectBestTranspose(IReadOnlyList<NormalizedNote> notes, bool preferNearestNatural = false, int maxSemitones = 12)
    {
        if (notes.Count == 0)
            return 0;

        maxSemitones = Math.Clamp(maxSemitones, 12, 60);
        var bestTranspose = 0;
        var bestScore = int.MaxValue;

        for (var transpose = -maxSemitones; transpose <= maxSemitones; transpose++)
        {
            var score = 0;
            foreach (var note in notes)
            {
                var target = note.NoteNumber + transpose;
                var folded = FoldIntoGameRange(target);
                score += preferNearestNatural
                    ? Math.Abs(folded - NoteMappingService.SnapToNearestNatural(folded))
                    : Math.Abs(target - folded);
            }

            if (score < bestScore
                || (score == bestScore && Math.Abs(transpose) < Math.Abs(bestTranspose)))
            {
                bestScore = score;
                bestTranspose = transpose;
            }
        }

        return bestTranspose;
    }

    public static List<NormalizedNote> ApplyTranspose(IReadOnlyList<NormalizedNote> notes, int semitones)
    {
        if (semitones == 0)
            return notes.Select(Clone).ToList();

        return notes.Select(n =>
        {
            var c = Clone(n);
            c.NoteNumber += semitones;
            c.OriginalNoteNumber = n.NoteNumber;
            c.NoteName = NoteNames.FromMidiNumber(c.NoteNumber);
            return c;
        }).ToList();
    }

    private static int FoldIntoGameRange(int noteNumber)
    {
        var result = noteNumber;
        while (result < NoteNames.MinGameNote)
            result += 12;
        while (result > NoteNames.MaxGameNote)
            result -= 12;
        return result;
    }

    private static NormalizedNote Clone(NormalizedNote n) => new()
    {
        OriginalNoteNumber = n.OriginalNoteNumber,
        NoteName = n.NoteName,
        StartMs = n.StartMs,
        DurationMs = n.DurationMs,
        Velocity = n.Velocity,
        TrackIndex = n.TrackIndex,
        Channel = n.Channel,
        NoteNumber = n.NoteNumber,
        Skipped = n.Skipped,
        FingerNumber = n.FingerNumber
    };
}
