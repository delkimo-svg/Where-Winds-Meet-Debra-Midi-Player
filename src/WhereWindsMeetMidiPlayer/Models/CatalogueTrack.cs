using WhereWindsMeetMidiPlayer.Helpers;

namespace WhereWindsMeetMidiPlayer.Models;

/// <summary>MIDI entry discovered from a Discord style channel.</summary>
public sealed class CatalogueTrack
{
    public string Id { get; set; } = string.Empty;
    public string StyleName { get; set; } = string.Empty;
    /// <summary>Lower = earlier in the style list (Discord channel position).</summary>
    public int StyleSortOrder { get; set; }
    public string Title { get; set; } = string.Empty;
    /// <summary>Original Discord attachment file name (source of truth for catalogue titles).</summary>
    public string? SourceFileName { get; set; }
    public long DurationMs { get; set; }

    /// <summary>Title without Debra / Debra Yume prefix for list display.</summary>
    public string DisplayTitle => CatalogueTitleHelper.GetDisplayTitle(
        Title,
        !string.IsNullOrWhiteSpace(CachedFilePath) ? CachedFilePath : SourceFileName);
    public ulong GuildId { get; set; }
    public ulong ChannelId { get; set; }
    public ulong MessageId { get; set; }
    public string? AttachmentId { get; set; }
    public string DownloadUrl { get; set; } = string.Empty;
    public string? CachedFilePath { get; set; }
    /// <summary>Relative path under app folder (e.g. catalogue-pack/style/song.mid) for shared offline packs.</summary>
    public string? BundledMidiPath { get; set; }
    public DateTime? PostedAt { get; set; }

    public bool IsNew
    {
        get
        {
            if (PostedAt is null)
                return false;

            var posted = PostedAt.Value.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(PostedAt.Value, DateTimeKind.Utc)
                : PostedAt.Value.ToUniversalTime();
            return posted >= DateTime.UtcNow.AddMonths(-1);
        }
    }

    /// <summary>0 = new (last month), 1 = older — for list sorting.</summary>
    public int NewSortRank => IsNew ? 0 : 1;

    public long PostedAtSortTicks
    {
        get
        {
            if (PostedAt is null)
                return long.MinValue;

            var posted = PostedAt.Value.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(PostedAt.Value, DateTimeKind.Utc)
                : PostedAt.Value.ToUniversalTime();
            return posted.Ticks;
        }
    }
}
