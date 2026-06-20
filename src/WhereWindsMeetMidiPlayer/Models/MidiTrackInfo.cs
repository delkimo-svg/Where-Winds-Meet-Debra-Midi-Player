namespace WhereWindsMeetMidiPlayer.Models;

public sealed class MidiTrackInfo
{
    public int Index { get; init; }
    public string Name { get; init; } = string.Empty;
    public int NoteCount { get; init; }
}
