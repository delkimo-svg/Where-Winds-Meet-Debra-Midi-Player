namespace WhereWindsMeetMidiPlayer.Infrastructure;

public static class NoteNames
{
    private static readonly string[] Names = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"];

    // Set from the selected GameProfile: WWM = 48..83 (C3–B5), FFXIV = 48..84 (C3–C6).
    public static int MinGameNote { get; private set; } = 48;
    public static int MaxGameNote { get; private set; } = 83;
    public const int MinPianoNote = 21; // A0
    public const int MaxPianoNote = 108; // C8

    public static void SetGameRange(int minNote, int maxNote)
    {
        MinGameNote = minNote;
        MaxGameNote = maxNote;
    }

    public static string FromMidiNumber(int noteNumber)
    {
        var octave = noteNumber / 12 - 1;
        var name = Names[noteNumber % 12];
        return $"{name}{octave}";
    }

    public static string PitchClassName(int noteNumber)
    {
        var pitch = noteNumber % 12;
        if (pitch < 0)
            pitch += 12;
        return Names[pitch];
    }
}
