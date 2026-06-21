using WhereWindsMeetMidiPlayer.Infrastructure;
using WhereWindsMeetMidiPlayer.Models;

namespace WhereWindsMeetMidiPlayer.Helpers;

public static class PracticeNoteLabelFormatter
{
    private static readonly string[] Solfege =
        ["Do", "Do#", "Ré", "Ré#", "Mi", "Fa", "Fa#", "Sol", "Sol#", "La", "La#", "Si"];

    public static string Format(
        PracticeVisualNote note,
        PracticeNoteLabelMode mode,
        IReadOnlyDictionary<int, string>? keyCombos)
    {
        switch (mode)
        {
            case PracticeNoteLabelMode.KeyboardKeys:
                if (keyCombos is null)
                    return string.Empty;
                var combo = LookupKeyCombo(note, keyCombos);
                return PracticeKeyLabelHelper.FormatCompact(combo);

            case PracticeNoteLabelMode.Solfege:
                return FormatSolfege(note.NoteNumber);

            case PracticeNoteLabelMode.FingerNumbers:
                return note.FingerNumber > 0
                    ? note.FingerNumber.ToString()
                    : string.Empty;

            default:
                return FormatLetter(note.NoteNumber);
        }
    }

    public static string FormatSolfege(int midiNote)
    {
        var pitch = midiNote % 12;
        if (pitch < 0)
            pitch += 12;
        return Solfege[pitch];
    }

    public static string FormatLetter(int midiNote) =>
        NoteNames.PitchClassName(midiNote);

    public static string LookupKeyCombo(
        PracticeVisualNote note,
        IReadOnlyDictionary<int, string>? keyCombos)
    {
        if (keyCombos is null)
            return string.Empty;

        if (note.GameNoteNumber > 0 && keyCombos.TryGetValue(note.GameNoteNumber, out var gameCombo))
            return gameCombo;

        if (keyCombos.TryGetValue(note.NoteNumber, out var midiCombo))
            return midiCombo;

        return string.Empty;
    }
}
