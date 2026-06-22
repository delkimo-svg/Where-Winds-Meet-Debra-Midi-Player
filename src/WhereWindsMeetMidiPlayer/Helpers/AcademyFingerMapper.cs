using WhereWindsMeetMidiPlayer.Models;

namespace WhereWindsMeetMidiPlayer.Helpers;

public static class AcademyFingerMapper
{
  private static readonly Dictionary<int, int> RightFingerByMidi = new()
  {
      [60] = 1, [62] = 2, [64] = 3, [65] = 4, [67] = 5,
      [69] = 5, [71] = 5,
      [72] = 1, [74] = 2, [76] = 3, [77] = 4, [79] = 5,
      [81] = 5, [83] = 5
  };

  private static readonly Dictionary<int, int> LeftFingerByMidi = new()
  {
      [45] = 5, [47] = 5,
      [48] = 5, [50] = 4, [52] = 3, [53] = 2, [55] = 1,
      [57] = 1, [59] = 2
  };

  public static int GetFinger(int midiNote, AcademyHand hand)
  {
    if (hand is AcademyHand.Right)
      return RightFingerByMidi.TryGetValue(midiNote, out var right) ? right : 0;

    if (hand is AcademyHand.Left)
      return LeftFingerByMidi.TryGetValue(midiNote, out var left) ? left : 0;

    if (hand is AcademyHand.Both)
    {
      if (RightFingerByMidi.TryGetValue(midiNote, out var right))
        return right;
      if (LeftFingerByMidi.TryGetValue(midiNote, out var left))
        return left;
    }

    return InferFingerFromNote(midiNote);
  }

  public static int InferFingerFromNote(int midiNote)
  {
    if (RightFingerByMidi.TryGetValue(midiNote, out var right))
      return right;

    if (LeftFingerByMidi.TryGetValue(midiNote, out var left))
      return left;

    return 0;
  }

    public static IReadOnlyList<PracticeVisualNote> StampAcademyNotes(
        IReadOnlyList<PracticeVisualNote> notes,
        AcademyHand hand,
        bool assignFingerNumbers = true)
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
                FingerNumber = assignFingerNumbers
                    ? n.FingerNumber > 0
                        ? n.FingerNumber
                        : hand == AcademyHand.Any
                            ? InferFingerFromNote(n.NoteNumber)
                            : GetFinger(n.NoteNumber, hand)
                    : n.FingerNumber
            })
            .ToList();
    }
}
