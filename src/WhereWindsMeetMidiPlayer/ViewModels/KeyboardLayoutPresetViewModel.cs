namespace WhereWindsMeetMidiPlayer.ViewModels;

public sealed class KeyboardLayoutPresetViewModel
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public bool IsSelected { get; init; }
}
