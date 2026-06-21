using WhereWindsMeetMidiPlayer.Infrastructure;
using WhereWindsMeetMidiPlayer.Models;

namespace WhereWindsMeetMidiPlayer.Services;

public static class PracticeFingersSidecarService
{
    public static string ResolveSidecarPath(string midiPath) =>
        Path.ChangeExtension(midiPath, ".fingers.json");

    public static void MergeIntoNotes(
        string midiPath,
        IList<NormalizedNote> notes,
        IReadOnlyList<MidiTrackInfo> tracks)
    {
        var sidecar = TryLoad(midiPath);
        if (sidecar is null)
            return;

        var (rightTrackIndex, leftTrackIndex) = ResolveHandTrackIndices(tracks);

        foreach (var entry in sidecar.Notes)
        {
            if (entry.Finger <= 0)
                continue;

            var trackIndex = ResolveTrackIndex(entry.Hand, rightTrackIndex, leftTrackIndex);
            if (TryFindNote(notes, entry.StartMs, entry.Midi, trackIndex, requireUnsetFinger: true, out var note))
            {
                note.FingerNumber = entry.Finger;
                continue;
            }

            var candidates = notes
                .Where(n => n.FingerNumber == 0 && n.StartMs == entry.StartMs && n.NoteNumber == entry.Midi)
                .ToList();
            if (candidates.Count == 1)
                candidates[0].FingerNumber = entry.Finger;
        }
    }

    public static List<PracticeVisualNote> MergeIntoVisualNotes(
        string midiPath,
        IReadOnlyList<PracticeVisualNote> visualNotes,
        IReadOnlyList<MidiTrackInfo> tracks)
    {
        var sidecar = TryLoad(midiPath);
        if (sidecar is null)
            return visualNotes.ToList();

        var (rightTrackIndex, leftTrackIndex) = ResolveHandTrackIndices(tracks);
        var merged = visualNotes.ToList();

        for (var i = 0; i < merged.Count; i++)
        {
            var note = merged[i];
            if (note.FingerNumber > 0)
                continue;

            foreach (var entry in sidecar.Notes)
            {
                if (entry.Finger <= 0)
                    continue;

                var trackIndex = ResolveTrackIndex(entry.Hand, rightTrackIndex, leftTrackIndex);
                if (entry.StartMs != note.StartMs || entry.Midi != note.NoteNumber || trackIndex != note.TrackIndex)
                    continue;

                merged[i] = CopyVisualNote(note, entry.Finger);
                break;
            }

            if (merged[i].FingerNumber > 0)
                continue;

            var candidates = sidecar.Notes
                .Where(e => e.Finger > 0 && e.StartMs == note.StartMs && e.Midi == note.NoteNumber)
                .ToList();
            if (candidates.Count != 1)
                continue;

            merged[i] = CopyVisualNote(note, candidates[0].Finger);
        }

        return merged;
    }

    private static PracticeFingersSidecar? TryLoad(string midiPath)
    {
        var sidecarPath = ResolveSidecarPath(midiPath);
        if (!File.Exists(sidecarPath))
            return null;

        return JsonFileStore.Read<PracticeFingersSidecar>(sidecarPath);
    }

    private static bool TryFindNote(
        IList<NormalizedNote> notes,
        long startMs,
        int midiNote,
        int trackIndex,
        bool requireUnsetFinger,
        out NormalizedNote? note)
    {
        note = notes.FirstOrDefault(n =>
            n.StartMs == startMs
            && n.NoteNumber == midiNote
            && n.TrackIndex == trackIndex
            && (!requireUnsetFinger || n.FingerNumber == 0));

        return note is not null;
    }

    private static (int RightTrackIndex, int LeftTrackIndex) ResolveHandTrackIndices(
        IReadOnlyList<MidiTrackInfo> tracks)
    {
        if (tracks.Count == 0)
            return (0, 1);

        var right = tracks.FirstOrDefault(t =>
            t.Name.Contains("right", StringComparison.OrdinalIgnoreCase))?.Index ?? tracks[0].Index;
        var left = tracks.FirstOrDefault(t =>
            t.Name.Contains("left", StringComparison.OrdinalIgnoreCase))?.Index
            ?? (tracks.Count > 1 ? tracks[1].Index : tracks[0].Index);

        return (right, left);
    }

    private static int ResolveTrackIndex(string hand, int rightTrackIndex, int leftTrackIndex) =>
        hand.Equals("LH", StringComparison.OrdinalIgnoreCase) ? leftTrackIndex : rightTrackIndex;

    private static PracticeVisualNote CopyVisualNote(PracticeVisualNote note, int fingerNumber) => new()
    {
        StartMs = note.StartMs,
        DurationMs = note.DurationMs,
        NoteNumber = note.NoteNumber,
        GameNoteNumber = note.GameNoteNumber,
        TrackIndex = note.TrackIndex,
        ColorHex = note.ColorHex,
        FingerNumber = fingerNumber
    };
}
