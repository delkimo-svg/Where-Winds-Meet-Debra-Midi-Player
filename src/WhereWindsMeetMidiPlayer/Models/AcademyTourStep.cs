namespace WhereWindsMeetMidiPlayer.Models;

public sealed class AcademyTourStep
{
    public string Text { get; set; } = string.Empty;
    public int[]? HighlightNotes { get; set; }

    /// <summary>Optional pictogram: middle-c, hand, steps-up, steps-down, count, listen, mirror, go.</summary>
    public string? Hint { get; set; }
}
