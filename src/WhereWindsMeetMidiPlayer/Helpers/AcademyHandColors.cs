using WhereWindsMeetMidiPlayer.Models;

namespace WhereWindsMeetMidiPlayer.Helpers;

public static class AcademyHandColors
{
    public const string RightHandHex = "#4A9EFF";
    public const string LeftHandHex = "#F59E0B";

    public static string ForHand(AcademyHand hand) =>
        PracticeHandColorResolver.ForHand(hand);

    public static string ForNote(int midiNote, AcademyHand hand) =>
        PracticeHandColorResolver.ForNote(midiNote, hand);
}
