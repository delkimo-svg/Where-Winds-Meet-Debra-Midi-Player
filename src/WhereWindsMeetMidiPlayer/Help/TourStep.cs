using WhereWindsMeetMidiPlayer.ViewModels;

namespace WhereWindsMeetMidiPlayer.Help;

public enum TourCalloutPlacement
{
    Auto,
    Center,
    Below,
    Above
}

public sealed class TourStep
{
    public required string Title { get; init; }
    public required string Description { get; init; }
    public string? TargetName { get; init; }
    public NavigationSection? ShowSection { get; init; }
    public TourCalloutPlacement Placement { get; init; } = TourCalloutPlacement.Auto;
}
