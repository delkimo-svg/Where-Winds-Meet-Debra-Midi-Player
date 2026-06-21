using CommunityToolkit.Mvvm.ComponentModel;

namespace WhereWindsMeetMidiPlayer.ViewModels;

public partial class PracticeTrackOption : ObservableObject
{
    public int TrackIndex { get; init; }
    public string DisplayName { get; init; } = string.Empty;

    [ObservableProperty] private bool _isEnabled = true;
    [ObservableProperty] private string _colorHex = "#4A9EFF";
}
