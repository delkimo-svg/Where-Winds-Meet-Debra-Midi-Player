using WhereWindsMeetMidiPlayer.Models;
using WhereWindsMeetMidiPlayer.Services;
using WhereWindsMeetMidiPlayer.ViewModels;

namespace WhereWindsMeetMidiPlayer.Helpers;

public static class PracticeNoteColorHelper
{
    public static List<PracticeVisualNote> ApplyTrackColors(
        IReadOnlyList<PracticeVisualNote> notes,
        IReadOnlyList<PracticeTrackOption> trackOptions)
    {
        var colorByTrack = trackOptions.ToDictionary(t => t.TrackIndex, t => t.ColorHex);

        return notes
            .Select(n => CopyWithColor(
                n,
                colorByTrack.TryGetValue(n.TrackIndex, out var hex)
                    ? hex
                    : PracticePrepareService.DefaultTrackColors[
                        n.TrackIndex % PracticePrepareService.DefaultTrackColors.Length]))
            .ToList();
    }

    public static List<PracticeVisualNote> ApplyPitchHandColors(
        IReadOnlyList<PracticeVisualNote> notes,
        string leftHex,
        string rightHex,
        int splitMidiNote = PracticeHandColorResolver.SplitMidiNote)
    {
        return notes
            .Select(n => CopyWithColor(n, n.NoteNumber < splitMidiNote ? leftHex : rightHex))
            .ToList();
    }

    public static PracticeVisualNote CopyWithColor(PracticeVisualNote note, string colorHex) => new()
    {
        StartMs = note.StartMs,
        DurationMs = note.DurationMs,
        NoteNumber = note.NoteNumber,
        GameNoteNumber = note.GameNoteNumber,
        TrackIndex = note.TrackIndex,
        ColorHex = colorHex,
        FingerNumber = note.FingerNumber
    };
}
