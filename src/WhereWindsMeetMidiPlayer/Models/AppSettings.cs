using System.Text.Json.Serialization;

namespace WhereWindsMeetMidiPlayer.Models;

public sealed class AppSettings
{
    /// <summary>Target game profile id: wwm or ffxiv.</summary>
    public string SelectedGameId { get; set; } = "wwm";
    /// <summary>Migration marker: 1 = FFXIV keymaps regenerated with the game-default keybind scheme.</summary>
    public int FfxivKeyMapVersion { get; set; }
    /// <summary>FFXIV: window (ms) merging near-simultaneous chord notes onto one onset. 0 = off.</summary>
    public int FfxivChordAlignWindowMs { get; set; } = 60;
    /// <summary>FFXIV: outer-voices chord reduction (3–4 notes → 2, 5+ → 3).</summary>
    public bool FfxivChordReduction { get; set; } = true;
    /// <summary>FFXIV: honor BMP-style "Track+1"/"Track-2" octave suffixes in track names.</summary>
    public bool FfxivTrackOctaveSuffix { get; set; } = true;
    /// <summary>FFXIV: minimum ms between consecutive note-ons (30 = musical floor, ~125 = midi2ffxiv-safe).</summary>
    public int FfxivMinNoteSpacingMs { get; set; } = 30;
    /// <summary>FFXIV: shed chord voices in fast passages (melody always kept) to respect the game's input rate.</summary>
    public bool FfxivAdaptiveVoicing { get; set; } = true;
    /// <summary>FFXIV Chat panel below the player is expanded.</summary>
    public bool FfxivChatPanelExpanded { get; set; }
    /// <summary>FFXIV Chat: channel code for the free-text box (Hypnotoad wire value, default Say).</summary>
    public int FfxivChatChannelCode { get; set; } = 0x000A;
    /// <summary>FFXIV Chat: channel code for the now-playing announcement (default Yell).</summary>
    public int FfxivChatAnnounceChannelCode { get; set; } = 0x001E;
    /// <summary>FFXIV Chat: now-playing template; {title} and {duration} are replaced.</summary>
    public string FfxivChatAnnounceTemplate { get; set; } = "♪ Now playing: {title} ♪";
    /// <summary>FFXIV Chat: announce the song automatically when playback starts.</summary>
    public bool FfxivChatAutoAnnounce { get; set; }
    public string KeyMappingFile { get; set; } = "debra-36-keys.json";
    /// <summary>Last selected keyboard layout preset (qwerty, qwertz, azerty). Empty when using a custom map.</summary>
    public string? KeyboardLayoutPresetId { get; set; } = "qwerty";
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
    public string PracticeRightHandColorHex { get; set; } = "#4ADE80";
    /// <summary>Falling-note / hand highlight color for the left hand.</summary>
    public string PracticeLeftHandColorHex { get; set; } = "#4A9EFF";
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

    /// <summary>Keep the main window above other applications.</summary>
    public bool WindowAlwaysOnTop { get; set; }

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

    /// <summary>UI theme: sakura (pink), wuxia (dark gold) or ffxiv (Eorzea night).</summary>
    public string UiTheme { get; set; } = "sakura";

    /// <summary>Theme picked by the player for each game profile id. Missing entry = the game's
    /// signature theme, so switching to FFXIV dresses the app in Eorzea colours on its own.</summary>
    public Dictionary<string, string> UiThemeByGame { get; set; } = new(StringComparer.OrdinalIgnoreCase);

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

    /// <summary>Playback hotkeys work system-wide, even when neither the player nor the game is focused.</summary>
    public bool PlaybackHotkeysGlobal { get; set; }

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
