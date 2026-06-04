namespace WhereWindsMeetMidiPlayer.Models;

public sealed class ScheduledNote
{
    public int NoteNumber { get; set; }
    public long StartMs { get; set; }
    public long DurationMs { get; set; }
    public string KeyCombo { get; set; } = string.Empty;
}
