using WhereWindsMeetMidiPlayer.Infrastructure;
using WhereWindsMeetMidiPlayer.Models;

namespace WhereWindsMeetMidiPlayer.Services;

public static class LiveMidiMapper
{
    public static string? MapNoteToKeyCombo(
        int rawMidiNote,
        int velocity,
        KeyMappingService keyMapping,
        NoteRangeService noteRange,
        bool smartTranspose,
        bool strictNoteRange,
        int octaveShift,
        NoteMappingMode mappingMode)
    {
        if (velocity <= 0)
            return null;

        var shifted = rawMidiNote + octaveShift * 12;
        var normalized = new NormalizedNote
        {
            OriginalNoteNumber = rawMidiNote,
            NoteNumber = shifted,
            NoteName = NoteNames.FromMidiNumber(Math.Clamp(shifted, 0, 127)),
            Velocity = velocity
        };

        var ranged = noteRange.ApplyRange([normalized], smartTranspose, strictNoteRange);
        if (ranged.Notes.Count == 0)
            return null;

        var mapped = NoteMappingService.ApplyMappingMode(ranged.Notes, mappingMode);
        if (mapped.Count == 0 || mapped[0].Skipped)
            return null;

        return keyMapping.GetKeyCombo(mapped[0].NoteNumber);
    }

    public static int? MapToGameNoteNumber(
        int rawMidiNote,
        int velocity,
        NoteRangeService noteRange,
        bool smartTranspose,
        bool strictNoteRange,
        int octaveShift,
        NoteMappingMode mappingMode)
    {
        if (velocity <= 0)
            return null;

        var shifted = rawMidiNote + octaveShift * 12;
        var normalized = new NormalizedNote
        {
            OriginalNoteNumber = rawMidiNote,
            NoteNumber = shifted,
            NoteName = NoteNames.FromMidiNumber(Math.Clamp(shifted, 0, 127)),
            Velocity = velocity
        };

        var ranged = noteRange.ApplyRange([normalized], smartTranspose, strictNoteRange);
        if (ranged.Notes.Count == 0)
            return null;

        var mapped = NoteMappingService.ApplyMappingMode(ranged.Notes, mappingMode);
        if (mapped.Count == 0 || mapped[0].Skipped)
            return null;

        return mapped[0].NoteNumber;
    }
}
