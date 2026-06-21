namespace WhereWindsMeetMidiPlayer.Models;

/// <summary>MIDI note event for local software-synth chart playback.</summary>
public sealed class SoundChartNote
{
    public int NoteNumber { get; init; }
    public long StartMs { get; init; }
    public long DurationMs { get; init; }
    public int Velocity { get; init; } = 90;
}
