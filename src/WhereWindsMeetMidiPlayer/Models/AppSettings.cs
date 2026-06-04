using System.Text.Json.Serialization;

namespace WhereWindsMeetMidiPlayer.Models;

public sealed class AppSettings
{
    public string KeyMappingFile { get; set; } = "debra-36-keys.json";
    public bool Shuffle { get; set; }
    public bool Repeat { get; set; }
    public int Volume { get; set; } = 64;
    public List<string> FavoritePaths { get; set; } = [];
    public string? LastImportFolder { get; set; }
    public bool SmartTranspose { get; set; } = true;
    public bool StrictNoteRange { get; set; }
    /// <summary>Minimum milliseconds between consecutive scheduled notes (default 2).</summary>
    public int NoteDelayMs { get; set; } = 2;
    /// <summary>Stagger chord notes (0 = SnowiyQ-style simultaneous taps).</summary>
    public int ChordRollDelayMs { get; set; }
    /// <summary>When enabled, advance to the next track in the active list after each song ends.</summary>
    public bool AutoPlayEnabled { get; set; }
    /// <summary>Seconds to wait after a song ends before playing the next track (0 = immediate).</summary>
    public int AutoPlayNextDelaySeconds { get; set; }
    /// <summary>Unused for file playback (tap-only like SnowiyQ). Kept for advanced tuning.</summary>
    public int MinKeyPressDurationMs { get; set; }
    public int IdenticalKeyGapMs { get; set; }
    public string? LastPlaylistPath { get; set; }
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }
    public double WindowWidth { get; set; } = 1280;
    public double WindowHeight { get; set; } = 720;

    /// <summary>Game process for attachment (e.g. wwm.exe) — direct keyboard injection target.</summary>
    public string TargetProcessName { get; set; } = "wwm.exe";

    /// <summary>Substring matched against the game window title (fallback).</summary>
    public string GameWindowTitleContains { get; set; } = "Where Winds Meet";

    /// <summary>Try to focus the game window before playback starts.</summary>
    public bool FocusGameBeforePlay { get; set; }

    /// <summary>Seconds to wait before playback starts (0 = immediate).</summary>
    public int PrePlayCountdownSeconds { get; set; } = 1;

    /// <summary>Optional URL to refresh the community catalogue (GitHub raw JSON).</summary>
    public string? SharedCatalogueManifestUrl { get; set; }

    /// <summary>HTTPS URL to debra-update-manifest.json (Discord CDN). Empty = use debra-update-manifest.url or local JSON beside the exe.</summary>
    public string? ReleaseManifestUrl { get; set; }

    /// <summary>When set, the update button stays hidden for this remote version until a newer one appears.</summary>
    public string? LastDismissedUpdateVersion { get; set; }

    public List<string> CustomWindowKeywords { get; set; } = [];

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DiscordBotToken { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DiscordGuildId { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DiscordCategoryChannelId { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DiscordCategoryName { get; set; }

    /// <summary>UI language code: en, es, fr, pt, zh, ja, de, it, ar.</summary>
    public string UiLanguage { get; set; } = "en";

    /// <summary>UI theme: sakura (pink) or wuxia (dark gold).</summary>
    public string UiTheme { get; set; } = "sakura";
}
