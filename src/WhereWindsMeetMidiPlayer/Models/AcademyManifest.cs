namespace WhereWindsMeetMidiPlayer.Models;

public sealed class AcademyManifest
{
    public int Version { get; set; } = 1;
    public string? Title { get; set; }
    public List<AcademyModule> Modules { get; set; } = [];
}

public sealed class AcademyModule
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Band { get; set; } = string.Empty;
    public string Sub { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public string? Guide { get; set; }
    public bool ComingSoon { get; set; }
    public List<AcademyLesson> Lessons { get; set; } = [];
}

public sealed class AcademyLesson
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public AcademyLessonKind Kind { get; set; }
    public int Order { get; set; }
    public string? Guide { get; set; }
    public AcademyHand Hand { get; set; } = AcademyHand.Any;
    public int[]? EnabledTracks { get; set; }
    public bool LearnMode { get; set; } = true;
    public bool ComingSoon { get; set; }
    public string? BundledMidiPath { get; set; }
    public List<AcademyTourStep>? TourSteps { get; set; }
    public AcademyDiscordRef? Discord { get; set; }
}

public sealed class AcademyDiscordRef
{
    public ulong ChannelId { get; set; }
    public ulong MessageId { get; set; }
    public string? AttachmentId { get; set; }
    public string? DownloadUrl { get; set; }
    public string? SourceFileName { get; set; }
}
