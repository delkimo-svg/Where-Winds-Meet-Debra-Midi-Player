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
    /// <summary>Folders imported via “Import folder” (legacy; refresh uses dedicated folder settings).</summary>
    public List<string> ImportFolderPaths { get; set; } = [];
    /// <summary>Default folder scanned by Library → Refresh.</summary>
    public string? LibraryRefreshFolder { get; set; }
    /// <summary>When true, Library refresh scans subfolders.</summary>
    public bool LibraryIncludeSubfolders { get; set; } = true;
    /// <summary>Default folder scanned by Practice library → Refresh.</summary>
    public string? PracticeLibraryRefreshFolder { get; set; }
    /// <summary>When true, Practice library refresh scans subfolders.</summary>
    public bool PracticeLibraryIncludeSubfolders { get; set; } = true;
    /// <summary>Persisted library MIDI file paths (restored on launch).</summary>
    public List<string> LibrarySongPaths { get; set; } = [];
    /// <summary>Persisted practice library MIDI file paths (restored on launch).</summary>
    public List<string> PracticeLibrarySongPaths { get; set; } = [];
    /// <summary>Last practice chart file path (restored on launch).</summary>
    public string? LastPracticeSongPath { get; set; }
    /// <summary>User dismissed the first-visit Practice guided tour.</summary>
    public bool PracticeTourDismissed { get; set; }
    /// <summary>Falling-note / hand highlight color for the right hand.</summary>
    public string PracticeRightHandColorHex { get; set; } = "#4A9EFF";
    /// <summary>Falling-note / hand highlight color for the left hand.</summary>
    public string PracticeLeftHandColorHex { get; set; } = "#F59E0B";
    public NoteMappingMode DefaultNoteMappingMode { get; set; } = NoteMappingMode.Chromatic36;
    public bool SmartTranspose { get; set; } = true;
    public bool StrictNoteRange { get; set; }
    /// <summary>Minimum milliseconds between consecutive scheduled notes (default 2).</summary>
    public int NoteDelayMs { get; set; } = 0;
    /// <summary>Stagger chord notes (0 = SnowiyQ-style simultaneous taps).</summary>
    public int ChordRollDelayMs { get; set; }
    /// <summary>Delay between modifier and main key for Shift/Ctrl combos (ms).</summary>
    public int ModifierDelayMs { get; set; }
    /// <summary>Player chrome opacity (15–100). Not persisted — always 100% on launch.</summary>
    [JsonIgnore]
    public int PlayerChromeOpacityPercent { get; set; } = 100;
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
    public double WindowWidth { get; set; } = 1024;
    public double WindowHeight { get; set; } = 682;

    /// <summary>Library/catalogue share of the left+playlist pair (0.06–0.94). Playlist gets the remainder.</summary>
    public double MainPanelLeftRatio { get; set; } = 0.5;

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

    /// <summary>Library list sort: Manual, Name, TimeAddedNewest, TimeAddedOldest.</summary>
    public string LibrarySortMode { get; set; } = nameof(SongListSortMode.Manual);

    /// <summary>Playlist list sort: Manual, Name, TimeAddedNewest, TimeAddedOldest.</summary>
    public string PlaylistSortMode { get; set; } = nameof(SongListSortMode.Manual);

    /// <summary>Catalogue list sort: PublishingDate, Alphabetical.</summary>
    public string CatalogueSortMode { get; set; } = "PublishingDate";

    /// <summary>Win32 virtual-key code for play/pause (default F3 = 0x72).</summary>
    public int PlaybackHotkeyPlayPause { get; set; } = PlaybackHotkeyDefaults.PlayPause;

    /// <summary>Win32 virtual-key code for stop (default F4).</summary>
    public int PlaybackHotkeyStop { get; set; } = PlaybackHotkeyDefaults.Stop;

    /// <summary>Win32 virtual-key code for previous track (default F5).</summary>
    public int PlaybackHotkeyPrevious { get; set; } = PlaybackHotkeyDefaults.Previous;

    /// <summary>Win32 virtual-key code for next track (default F6).</summary>
    public int PlaybackHotkeyNext { get; set; } = PlaybackHotkeyDefaults.Next;

    /// <summary>Last selected MIDI input device name for live keyboard mode.</summary>
    public string? LastMidiInputDeviceName { get; set; }

    /// <summary>Falling-note tip labels in practice mode.</summary>
    public PracticeNoteLabelMode PracticeNoteLabelMode { get; set; } = PracticeNoteLabelMode.LetterNames;

    /// <summary>Play chart and live MIDI through the built-in software synth during practice.</summary>
    public bool PracticeSoundEnabled { get; set; }

    /// <summary>Mute built-in synth for live key input; game instrument only (keys still injected).</summary>
    public bool PracticeGameSoundOnly { get; set; }

    /// <summary>User dismissed the practice "no MIDI keyboard" reminder (PC keyboard is fine).</summary>
    public bool PracticeNoMidiKeyboardWarningDismissed { get; set; }

    /// <summary>Completed Piano Academy lesson IDs (local progress).</summary>
    public List<string> CompletedAcademyLessonIds { get; set; } = [];

    /// <summary>Countdown seconds before academy armed practice starts (default 5).</summary>
    public int AcademyPracticeCountdownSeconds { get; set; } = 5;

    /// <summary>Last selected Piano Academy module id (e.g. BB).</summary>
    public string? LastAcademyModuleId { get; set; }

    /// <summary>Last selected exercise lesson id within the module.</summary>
    public string? LastAcademyExerciseLessonId { get; set; }

    /// <summary>Last selected practice-song lesson id within the module.</summary>
    public string? LastAcademySongLessonId { get; set; }

    /// <summary>Lesson id last previewed (exercise or song).</summary>
    public string? LastAcademyLessonId { get; set; }
}
