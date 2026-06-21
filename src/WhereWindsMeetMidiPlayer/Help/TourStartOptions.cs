namespace WhereWindsMeetMidiPlayer.Help;

public sealed class TourStartOptions
{
    public bool AllowDontShowAgain { get; init; }
    public Action<bool>? OnCompleted { get; init; }
    public Func<IReadOnlyList<TourStep>>? RefreshSteps { get; init; }
}
