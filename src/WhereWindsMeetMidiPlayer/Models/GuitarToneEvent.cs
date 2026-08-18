namespace WhereWindsMeetMidiPlayer.Models;

/// <summary>
/// A MIDI program change mapped to an FFXIV electric-guitar tone
/// (0 Overdriven, 1 Clean, 2 Muted, 3 Power Chords, 4 Special).
/// </summary>
public sealed class GuitarToneEvent
{
    public long StartMs { get; init; }
    public int TrackIndex { get; init; }
    public int Tone { get; init; }
}
