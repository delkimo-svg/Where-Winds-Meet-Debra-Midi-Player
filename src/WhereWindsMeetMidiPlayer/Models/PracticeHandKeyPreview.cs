namespace WhereWindsMeetMidiPlayer.Models;

/// <summary>Maps MIDI notes to track indices for hand-colored keyboard preview.</summary>
public sealed class PracticeHandKeyPreview
{
    public Dictionary<int, int> MidiToTrack { get; init; } = new();
    public Dictionary<int, string> TrackColors { get; init; } = new();
}
