namespace WhereWindsMeetMidiPlayer.Models;

public sealed class NormalizedNote
{
    public int NoteNumber { get; set; }
    public string NoteName { get; set; } = string.Empty;
    public long StartMs { get; set; }
    public long DurationMs { get; set; }
    public int Velocity { get; set; }
    public int TrackIndex { get; set; }
    public int Channel { get; set; }
    public bool Skipped { get; set; }
    public int OriginalNoteNumber { get; set; }
    /// <summary>Piano finger (1–5) from MIDI LyricEvent or sidecar. 0 = none.</summary>
    public int FingerNumber { get; set; }
}
