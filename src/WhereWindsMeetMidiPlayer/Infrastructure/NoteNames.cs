namespace WhereWindsMeetMidiPlayer.Infrastructure;

public static class NoteNames
{
    private static readonly string[] Names = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"];

    public const int MinGameNote = 48; // C3
    public const int MaxGameNote = 83; // B5
    public const int MinPianoNote = 21; // A0
    public const int MaxPianoNote = 108; // C8

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
