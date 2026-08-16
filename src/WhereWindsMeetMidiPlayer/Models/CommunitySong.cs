using System.Text.Json.Serialization;
using WhereWindsMeetMidiPlayer.Helpers;

namespace WhereWindsMeetMidiPlayer.Models;

public enum CommunityOrigin
{
    Bmp,
    Debra
}

/// <summary>One row of the Community page: a BMP-website solo MIDI, or a Debra catalogue track.</summary>
public sealed class CommunitySong
{
    /// <summary>"bmp:{id}" or "debra:{catalogueTrackId}".</summary>
    public string Key { get; set; } = string.Empty;
    public CommunityOrigin Origin { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    /// <summary>Who arranged/uploaded it (BMP arranger, Debra catalogue).</summary>
    public string Creator { get; set; } = string.Empty;
    /// <summary>Work the song comes from (game/anime/movie), when the site knows it.</summary>
    public string SourceWork { get; set; } = string.Empty;
    public string Genre { get; set; } = string.Empty;
    public long DurationMs { get; set; }
    public int Downloads { get; set; }
    public string Filename { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
    public DateTime? CreatedAt { get; set; }

    /// <summary>Set for Origin=Debra rows: the live catalogue track (play path, cache, NEW badge).</summary>
    [JsonIgnore]
    public CatalogueTrack? DebraTrack { get; set; }

    [JsonIgnore]
    public string DisplayTitle => Origin == CommunityOrigin.Debra && DebraTrack is not null
        ? DebraTrack.DisplayTitle
        : Title;

    [JsonIgnore]
    public string DisplayArtist => string.IsNullOrWhiteSpace(Artist) || Artist == "?" ? string.Empty : Artist;

    [JsonIgnore]
    public bool HasGenre => !string.IsNullOrWhiteSpace(Genre);

    [JsonIgnore]
    public string DurationDisplay
    {
        get
        {
            var ms = Origin == CommunityOrigin.Debra && DebraTrack is not null && DebraTrack.DurationMs > 0
                ? DebraTrack.DurationMs
                : DurationMs;
            return ms > 0 ? TimeFormat.FromMilliseconds(ms) : string.Empty;
        }
    }

    [JsonIgnore]
    public bool IsNew
    {
        get
        {
            if (Origin == CommunityOrigin.Debra)
                return DebraTrack?.IsNew == true;
            if (CreatedAt is null)
                return false;

            var created = CreatedAt.Value.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(CreatedAt.Value, DateTimeKind.Utc)
                : CreatedAt.Value.ToUniversalTime();
            return created >= DateTime.UtcNow.AddMonths(-1);
        }
    }

    [JsonIgnore]
    public long CreatedAtSortTicks
    {
        get
        {
            if (Origin == CommunityOrigin.Debra)
                return DebraTrack?.PostedAtSortTicks ?? long.MinValue;
            if (CreatedAt is null)
                return long.MinValue;

            var created = CreatedAt.Value.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(CreatedAt.Value, DateTimeKind.Utc)
                : CreatedAt.Value.ToUniversalTime();
            return created.Ticks;
        }
    }
}
