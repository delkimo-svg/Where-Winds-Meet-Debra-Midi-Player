using WhereWindsMeetMidiPlayer.Models;

namespace WhereWindsMeetMidiPlayer.Helpers;

public static class AcademyFingerMapper
{
    public static int GetFinger(int midiNote, AcademyHand hand)
    {
        if (hand is AcademyHand.Right or AcademyHand.Both)
        {
            switch (midiNote)
            {
                case 60: return 1;
                case 62: return 2;
                case 64: return 3;
                case 65: return 4;
                case 67: return 5;
            }
        }

        if (hand is AcademyHand.Left or AcademyHand.Both)
        {
            switch (midiNote)
            {
                case 48: return 5;
                case 50: return 4;
                case 52: return 3;
                case 53: return 2;
                case 55: return 1;
            }
        }

        return InferFingerFromNote(midiNote);
    }

    public static int InferFingerFromNote(int midiNote)
    {
        switch (midiNote)
        {
            case 60: return 1;
            case 62: return 2;
            case 64: return 3;
            case 65: return 4;
            case 67: return 5;
            case 48: return 5;
            case 50: return 4;
            case 52: return 3;
            case 53: return 2;
            case 55: return 1;
            default: return 0;
        }
    }

    public static IReadOnlyList<PracticeVisualNote> StampAcademyNotes(
        IReadOnlyList<PracticeVisualNote> notes,
        AcademyHand hand)
    {
        return notes
            .Select(n => new PracticeVisualNote
            {
                StartMs = n.StartMs,
                DurationMs = n.DurationMs,
                NoteNumber = n.NoteNumber,
                GameNoteNumber = n.GameNoteNumber,
                TrackIndex = n.TrackIndex,
                ColorHex = AcademyHandColors.ForNote(n.NoteNumber, hand),
                FingerNumber = n.FingerNumber > 0
                    ? n.FingerNumber
                    : hand == AcademyHand.Any
                        ? InferFingerFromNote(n.NoteNumber)
                        : GetFinger(n.NoteNumber, hand)
            })
            .ToList();
    }
}
