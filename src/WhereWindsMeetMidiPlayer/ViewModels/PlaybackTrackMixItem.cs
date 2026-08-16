using CommunityToolkit.Mvvm.ComponentModel;

namespace WhereWindsMeetMidiPlayer.ViewModels;

/// <summary>One row of the player's track mixer popup.</summary>
public partial class PlaybackTrackMixItem : ObservableObject
{
    public int TrackIndex { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string NoteCountDisplay { get; init; } = string.Empty;

    [ObservableProperty] private bool _isMuted;
}
