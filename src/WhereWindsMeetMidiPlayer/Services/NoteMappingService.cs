using WhereWindsMeetMidiPlayer.Infrastructure;
using WhereWindsMeetMidiPlayer.Models;

namespace WhereWindsMeetMidiPlayer.Services;

public static class NoteMappingService
{
    private static readonly int[] NaturalPitchClasses = [0, 2, 4, 5, 7, 9, 11];

    public static List<NormalizedNote> ApplyMappingMode(IReadOnlyList<NormalizedNote> notes, NoteMappingMode mode)
    {
        if (mode != NoteMappingMode.ClosestNatural)
            return notes.Select(Clone).ToList();

        return notes.Select(n =>
        {
            var c = Clone(n);
            c.NoteNumber = SnapToNearestNatural(c.NoteNumber);
            c.NoteName = NoteNames.FromMidiNumber(c.NoteNumber);
            return c;
        }).ToList();
    }

    public static int SnapToNearestNatural(int noteNumber)
    {
        var best = noteNumber;
        var bestDistance = int.MaxValue;

        for (var midi = NoteNames.MinGameNote; midi <= NoteNames.MaxGameNote; midi++)
        {
            if (!IsNaturalPitchClass(midi))
                continue;

            var distance = Math.Abs(midi - noteNumber);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = midi;
            }
        }

        return best;
    }

    private static bool IsNaturalPitchClass(int midi) =>
        NaturalPitchClasses.Contains(midi % 12);

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
