namespace WhereWindsMeetMidiPlayer.Models;

/// <summary>
/// Hosted JSON on Discord CDN (or any HTTPS URL) — republish when you upload a new portable .rar.
/// </summary>
public sealed class ReleaseManifest
{
    public string Version { get; set; } = "1.0.0";
    public string? DownloadUrl { get; set; }
    public string? FileName { get; set; }
    public string? ReleaseNotes { get; set; }
    public DateTime? PublishedAt { get; set; }
}
