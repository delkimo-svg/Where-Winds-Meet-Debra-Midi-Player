namespace WhereWindsMeetMidiPlayer.Models;

public sealed class PracticeVisualNote
{
    public long StartMs { get; init; }
    public long DurationMs { get; init; }
    public int NoteNumber { get; init; }
    /// <summary>Mapped in-game note (48–83) for key labels and learn mode.</summary>
    public int GameNoteNumber { get; init; }
    public int TrackIndex { get; init; }
    public string ColorHex { get; init; } = "#4A9EFF";
    /// <summary>Piano finger number (1–5) for academy falling-note labels. 0 = none.</summary>
    public int FingerNumber { get; init; }
}
