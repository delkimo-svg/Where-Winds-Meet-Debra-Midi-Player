namespace WhereWindsMeetMidiPlayer.Models;

/// <summary>Schema for practice .fingers.json sidecar files.</summary>
public sealed class PracticeFingersSidecar
{
    public string Title { get; set; } = string.Empty;
    public string SourceVideo { get; set; } = string.Empty;
    public PracticeFingersFrameSize? FrameSize { get; set; }
    public double Fps { get; set; }
    public string HandSplit { get; set; } = string.Empty;
    public List<PracticeFingersSidecarNote> Notes { get; set; } = [];
}

public sealed class PracticeFingersFrameSize
{
    public int Width { get; set; }
    public int Height { get; set; }
}

public sealed class PracticeFingersSidecarNote
{
    public long StartMs { get; set; }
    public long DurationMs { get; set; }
    public int Midi { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Hand { get; set; } = string.Empty;
    public int Finger { get; set; }
}
