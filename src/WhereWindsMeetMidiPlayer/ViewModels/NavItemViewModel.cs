using CommunityToolkit.Mvvm.ComponentModel;

namespace WhereWindsMeetMidiPlayer.ViewModels;

public partial class NavItemViewModel : ObservableObject
{
    public NavigationSection Section { get; init; }
    public string Icon { get; init; } = string.Empty;

    [ObservableProperty] private string _label = string.Empty;

    [ObservableProperty] private bool _isActive;
}
