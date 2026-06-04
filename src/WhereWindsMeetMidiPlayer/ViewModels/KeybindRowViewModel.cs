using System.Collections.ObjectModel;

namespace WhereWindsMeetMidiPlayer.ViewModels;

public sealed class KeybindRowViewModel
{
    public required string PitchLabel { get; init; }
    public ObservableCollection<KeybindCellViewModel> Cells { get; } = [];
}
