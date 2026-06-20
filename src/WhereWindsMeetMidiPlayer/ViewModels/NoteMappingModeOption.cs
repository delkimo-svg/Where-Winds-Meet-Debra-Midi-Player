using WhereWindsMeetMidiPlayer.Models;

namespace WhereWindsMeetMidiPlayer.ViewModels;

public sealed class NoteMappingModeOption
{
    public NoteMappingMode Mode { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
}
