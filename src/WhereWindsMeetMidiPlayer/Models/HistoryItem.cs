namespace WhereWindsMeetMidiPlayer.Models;

public enum PlaybackStatus
{
    Completed,
    Skipped,
    Stopped,
    Error
}

public sealed class HistoryItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string SongTitle { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public DateTime PlayedAt { get; set; } = DateTime.UtcNow;
    public long DurationMs { get; set; }
    public PlaybackStatus Status { get; set; }
}
