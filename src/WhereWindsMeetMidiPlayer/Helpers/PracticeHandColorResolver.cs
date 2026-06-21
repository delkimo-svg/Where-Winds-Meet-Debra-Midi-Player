using WhereWindsMeetMidiPlayer.Models;

namespace WhereWindsMeetMidiPlayer.Helpers;

public static class PracticeHandColorResolver
{
    public const int SplitMidiNote = 60;

    public static string RightHandHex { get; set; } = AcademyHandColors.RightHandHex;
    public static string LeftHandHex { get; set; } = AcademyHandColors.LeftHandHex;

    public static void ApplySettings(string? rightHex, string? leftHex)
    {
        if (!string.IsNullOrWhiteSpace(rightHex))
            RightHandHex = rightHex.Trim();

        if (!string.IsNullOrWhiteSpace(leftHex))
            LeftHandHex = leftHex.Trim();
    }

    public static string ForPitch(int midiNote) =>
        midiNote < SplitMidiNote ? LeftHandHex : RightHandHex;

    public static string ForHand(AcademyHand hand) =>
        hand is AcademyHand.Left ? LeftHandHex : RightHandHex;

    public static string ForNote(int midiNote, AcademyHand hand)
    {
        if (hand is AcademyHand.Left)
            return LeftHandHex;

        if (hand is AcademyHand.Right)
            return RightHandHex;

        return ForPitch(midiNote);
    }
}
