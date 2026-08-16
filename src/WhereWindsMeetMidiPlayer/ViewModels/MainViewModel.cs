using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using WhereWindsMeetMidiPlayer.Help;
using WhereWindsMeetMidiPlayer.Helpers;
using WhereWindsMeetMidiPlayer.Themes;
using WhereWindsMeetMidiPlayer.Infrastructure;
using WhereWindsMeetMidiPlayer.Localization;
using WhereWindsMeetMidiPlayer.Models;
using WhereWindsMeetMidiPlayer.Services;
using WhereWindsMeetMidiPlayer.Services.Audio;
using WhereWindsMeetMidiPlayer.Services.Discord;

namespace WhereWindsMeetMidiPlayer.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly MidiParserService _midiParser = new();
    private readonly SongMetadataCacheService _songMetadataCache = new();
    private readonly NoteRangeService _noteRange = new();
    private readonly KeyMappingService _keyMapping = new();
    private readonly GameWindowService _gameWindow = new();
    private readonly InputService _input;
    private readonly PlaybackEngine _playback;
    private readonly PlaylistService _playlistService;
    private readonly LibraryService _library;
    private readonly HistoryService _history = new();
    private readonly AppSettingsService _settings = new();
    private readonly DispatcherTimer _uiTimer;
    private readonly CollectionViewSource _libraryViewSource = new();
    private readonly CollectionViewSource _practiceLibraryViewSource = new();
    private readonly CollectionViewSource _favoritesViewSource = new();
    private readonly CollectionViewSource _catalogueViewSource = new();
    private readonly CollectionViewSource _playlistViewSource = new();
    private readonly DiscordCatalogueService _discordCatalogue = new();
    private readonly SharedCatalogueService _sharedCatalogue = new();
    private readonly AppUpdateService _appUpdate = new();
    private readonly GlobalPlaybackHotkeyService _globalHotkey;
    private ReleaseManifest? _pendingUpdateManifest;
    private readonly SystemVolumeService _systemVolume = new();
    private readonly SongTempoStore _songTempo = new();
    private readonly SongPlaybackCalibrationStore _songPlayback = new();
    private readonly MidiPlaybackPreparer _midiPlaybackPreparer;
    private readonly MidiLiveInputService _liveMidi;
    private readonly PracticePrepareService _practicePrepare;
    private readonly PracticeSessionService _practiceSession;
    private readonly MidiSoundEngine _midiSoundEngine = new();
    private readonly PracticeSoundService _practiceSound;
    private readonly PracticeKeyboardHighlightService _practiceKeyboardPress = new();
    private readonly DiscordAcademyService _discordAcademy = new();

    private bool _suppressVolumeSync;
    private bool _suppressCatalogueStats;
    private bool _libraryStatsRefreshScheduled;
    private bool _catalogueViewRefreshScheduled;
    private bool _libraryViewRefreshScheduled;
    private bool _favoritesViewRefreshScheduled;
    private CancellationTokenSource? _settingsSaveDebounce;
    private bool _suppressPlaybackUi;
    private bool _historyPersistScheduled;
    private DateTime _lastPlaybackStatusUtc = DateTime.MinValue;
    private Dictionary<string, CatalogueTrack> _catalogueByCachedPath = new(StringComparer.OrdinalIgnoreCase);

    private CatalogueTrack? _nowPlayingCatalogueTrack;

    private Playlist _currentPlaylist = new();
    private string? _currentPlaylistPath;
    private string? _deferredPlaylistPath;
    private ActivePlaybackList _activePlaybackList = ActivePlaybackList.None;
    private int _activeListIndex = -1;
    private Song? _nowPlaying;
    private HistoryItem? _activeHistoryItem;
    private int _nowPlayingNoteCount;

    [ObservableProperty] private NavigationSection _selectedSection = NavigationSection.Catalogue;
    [ObservableProperty] private string _librarySearchText = string.Empty;
    [ObservableProperty] private string _practiceLibrarySearchText = string.Empty;
    [ObservableProperty] private Song? _selectedLibrarySong;
    [ObservableProperty] private Song? _selectedPlaylistSong;
    [ObservableProperty] private Song? _selectedFavoriteSong;
    [ObservableProperty] private string _favoritesSearchText = string.Empty;
    [ObservableProperty] private string _favoritesStatsText = "0 songs";
    [ObservableProperty] private string _nowPlayingTitle = string.Empty;
    [ObservableProperty] private string _nowPlayingPath = string.Empty;
    [ObservableProperty] private string _nowPlayingCatalogueTrackId = string.Empty;
    [ObservableProperty] private string _nowPlayingRange = "C3 - B5";
    [ObservableProperty] private string _nowPlayingNotesDisplay = "—";
    [ObservableProperty] private string _nowPlayingDurationDisplay = "—";
    [ObservableProperty] private string _nowPlayingSubtitle = string.Empty;
    [ObservableProperty] private double _progress;
    [ObservableProperty] private string _currentTimeText = "0:00";
    [ObservableProperty] private string _totalTimeText = "0:00";
    [ObservableProperty] private string _playlistName = string.Empty;
    [ObservableProperty] private bool _isPlaying;
    [ObservableProperty] private bool _smartTranspose = true;
    [ObservableProperty] private bool _strictNoteRange;
    [ObservableProperty] private int _noteDelayMs;
    [ObservableProperty] private int _chordRollDelayMs;
    [ObservableProperty] private int _modifierDelayMs;
    [ObservableProperty] private int _playbackOctaveShift;
    [ObservableProperty] private bool _playbackPhraseFold;
    [ObservableProperty] private bool _showMidiTrackSelector;
    [ObservableProperty] private bool _isPlayerTuningPanelOpen;
    [ObservableProperty] private int _playerChromeOpacityPercent = 100;
    [ObservableProperty] private NoteMappingModeOption? _selectedNoteMappingMode;
    [ObservableProperty] private int _autoPlayNextDelaySeconds;
    [ObservableProperty] private bool _autoPlayEnabled;
    [ObservableProperty] private bool _shuffle;
    [ObservableProperty] private bool _repeat;
    [ObservableProperty] private bool _windowAlwaysOnTop;
    [ObservableProperty] private bool _playbackHotkeysGlobal;
    [ObservableProperty] private int _volume = 64;
    [ObservableProperty] private double _songTempoBpm = 120;
    [ObservableProperty] private int _playbackTempoPercent = 100;
    [ObservableProperty] private KeyLayoutOption? _selectedLayout;
    [ObservableProperty] private string _libraryStatsText = "0 songs • 0 B";
    [ObservableProperty] private string _playlistStatsText = "0 songs • 0:00";
    [ObservableProperty] private string _libraryHeaderText = "All Songs (0)";
    [ObservableProperty] private bool _isFavorite;
    [ObservableProperty] private bool _playlistFavoritesOnly;
    [ObservableProperty] private string _targetProcessName = "wwm.exe";
    [ObservableProperty] private string _gameWindowTitleContains = "Where Winds Meet";
    [ObservableProperty] private GameProfile _selectedGameProfile = GameProfiles.WhereWindsMeet;
    [ObservableProperty] private bool _focusGameBeforePlay;
    [ObservableProperty] private int _prePlayCountdownSeconds = 1;
    [ObservableProperty] private string _gameConnectionStatus = string.Empty;
    [ObservableProperty] private bool _isLiveMidiEnabled;
    [ObservableProperty] private string _liveMidiStatusText = string.Empty;
    [ObservableProperty] private Song? _selectedPracticeSong;
    [ObservableProperty] private string _practiceTitle = string.Empty;
    [ObservableProperty] private bool _isPracticePlaying;
    [ObservableProperty] private double _practiceProgress;
    [ObservableProperty] private string _practiceTimeText = "0:00";
    [ObservableProperty] private string _practiceDurationText = "0:00";
    [ObservableProperty] private bool _isPracticeLearnMode;
    [ObservableProperty] private bool _isPracticeFollowMode = true;
    [ObservableProperty] private bool _isPracticeGameKeysView = true;
    [ObservableProperty] private bool _isPracticeFullPianoView;

    private bool _suppressPracticeModeSync;
    private bool _suppressPracticeViewSync;
    private bool _suppressPracticeLabelSync;
    private bool _suppressPracticeSoundSave;
    private bool _suppressPracticeGameSoundSave;
    private bool _suppressPracticeTempoChange;
    private bool _practiceSessionOwnsLiveMidi;
    private readonly HashSet<string> _practicePcKeysHeld = new(StringComparer.OrdinalIgnoreCase);

    [ObservableProperty] private bool _isPracticeSolfegeLabels;
    [ObservableProperty] private bool _isPracticeLetterLabels = true;
    [ObservableProperty] private bool _isPracticeKeyboardLabels;
    [ObservableProperty] private bool _isPracticeLibraryPanelOpen;
    [ObservableProperty] private bool _isPracticeSoundEnabled;
    [ObservableProperty] private bool _isPracticeGameSoundOnly;
    [ObservableProperty] private bool _isAcademyPracticeMode;
    [ObservableProperty] private string _academyGuideText = string.Empty;
    [ObservableProperty] private bool _isPracticeAcademyOverlayOpen;
    [ObservableProperty] private bool _isPracticeLessonArmed;
    [ObservableProperty] private bool _isPracticeCountdownActive;
    [ObservableProperty] private string _practiceCountdownDisplay = string.Empty;
    [ObservableProperty] private int _practiceAcademyCountdownSeconds = 5;

    [ObservableProperty] private int _practiceTempoPercent = 100;
    [ObservableProperty] private PracticeHandKeyPreview? _practiceHandKeyPreview;
    [ObservableProperty] private bool _isAcademyTourVisible;
    [ObservableProperty] private string _academyTourText = string.Empty;
    [ObservableProperty] private string _academyTourStepLabel = string.Empty;
    [ObservableProperty] private int[] _academyTourHighlightNotes = [];
    [ObservableProperty] private AcademyTourHintKind _academyTourPictogramHint = AcademyTourHintKind.None;
    [ObservableProperty] private bool _isAcademyTourSongPickerVisible;

    [ObservableProperty] private string _catalogueSearchText = string.Empty;
    [ObservableProperty] private string? _catalogueStyleFilter;
    [ObservableProperty] private CatalogueTrack? _selectedCatalogueTrack;
    [ObservableProperty] private string _catalogueStatsText = "0 tracks";
    [ObservableProperty] private string _catalogueStatusText = "Connecting to Discord catalogue…";
    [ObservableProperty] private string _historyStatsText = "0 plays";

    [ObservableProperty] private bool _isCatalogueLoading;
    [ObservableProperty] private bool _isPlaylistLoading;
    [ObservableProperty] private bool _isUpdateAvailable;
    [ObservableProperty] private string _appVersionLabel = AppReleaseInfo.CurrentVersionLabel;
    [ObservableProperty] private string _releaseManifestUrl = string.Empty;
    [ObservableProperty] private string _libraryRefreshFolderPath = string.Empty;
    [ObservableProperty] private bool _libraryIncludeSubfolders = true;
    [ObservableProperty] private string _practiceLibraryRefreshFolderPath = string.Empty;
    [ObservableProperty] private bool _practiceLibraryIncludeSubfolders = true;
    [ObservableProperty] private string _practiceRightHandColorHex = "#4ADE80";
    [ObservableProperty] private string _practiceLeftHandColorHex = "#4A9EFF";
    [ObservableProperty] private bool _isPracticeHandColorPickerOpen;

    private DiscordCredentials? _discordCredentials;

    public ObservableCollection<Song> LibrarySongs { get; } = [];
    public ObservableCollection<Song> PracticeLibrarySongs { get; } = [];
    public ObservableCollection<Song> FavoriteSongs { get; } = [];
    public BulkObservableCollection<CatalogueTrack> CatalogueTracks { get; } = [];
    public BulkObservableCollection<string> CatalogueStyles { get; } = [];
    public BulkObservableCollection<Song> PlaylistSongs { get; } = [];
    public BulkObservableCollection<HistoryItem> HistoryItems { get; } = [];
    public ObservableCollection<KeyLayoutOption> KeyLayouts { get; } = [];
    public ObservableCollection<KeyboardLayoutPresetViewModel> KeyboardLayoutPresets { get; } = [];
    public ObservableCollection<NavItemViewModel> NavItems { get; } = [];
    public BulkObservableCollection<SavedPlaylistEntry> SavedPlaylists { get; } = [];
    public ObservableCollection<LanguageOption> AvailableLanguages { get; } = [];
    public ObservableCollection<ThemeOption> AvailableThemes { get; } = [];
    public ObservableCollection<NoteMappingModeOption> NoteMappingModes { get; } = [];
    public ObservableCollection<PlaybackTrackMixItem> PlaybackTrackMixItems { get; } = [];
    public ObservableCollection<MidiInputDeviceOption> MidiInputDevices { get; } = [];
    public ObservableCollection<PracticeTrackOption> PracticeTrackOptions { get; } = [];
    public ObservableCollection<SongSortOption> LibrarySortOptions { get; } = [];
    public ObservableCollection<SongSortOption> PlaylistSortOptions { get; } = [];
    public ObservableCollection<CatalogueSortOption> CatalogueSortOptions { get; } = [];
    public LocalizedUi Ui { get; } = new();

    [ObservableProperty] private SavedPlaylistEntry? _selectedSavedPlaylist;
    [ObservableProperty] private SongSortOption? _selectedLibrarySortOption;
    [ObservableProperty] private SongSortOption? _selectedPlaylistSortOption;
    [ObservableProperty] private CatalogueSortOption? _selectedCatalogueSortOption;
    [ObservableProperty] private LanguageOption? _selectedLanguage;
    [ObservableProperty] private ThemeOption? _selectedTheme;
    [ObservableProperty] private bool _isTrackMixerOpen;
    [ObservableProperty] private MidiInputDeviceOption? _selectedMidiInputDevice;

    private bool _suppressThemeChange;

    private bool _suppressSavedPlaylistSelection;
    private bool _suppressPlaylistCollectionUpdates;
    private bool _playlistStatsRefreshScheduled;
    private int _playlistLoadGeneration;
    private HashSet<string> _favoritePathSet = new(StringComparer.OrdinalIgnoreCase);
    private bool _suppressAutoSave;
    private CancellationTokenSource? _autoSaveDebounce;
    private DispatcherTimer? _autoAdvanceTimer;
    private int _autoAdvanceTicksRemaining;
    private string? _gameStatusBeforeAutoAdvance;
    private List<int>? _shuffleOrder;
    private int _shuffleOrderCursor = -1;
    private string? _shuffleOrderKey;
    private Song? _lastSelectedLibrarySong;
    private Song? _lastSelectedPlaylistSong;
    private Song? _lastSelectedFavoriteSong;
    private CatalogueTrack? _lastSelectedCatalogueTrack;
    private PrimarySelectionSource _primarySelection = PrimarySelectionSource.None;
    private bool _suppressExclusiveSelection;
    private bool _suppressSortChange;
    private bool _suppressTempoChange;
    private int _sessionTempoPercent = 100;

    public bool IsPlaylistManualSort => SelectedPlaylistSortOption?.Mode == SongListSortMode.Manual;

    private enum PrimarySelectionSource
    {
        None,
        Playlist,
        Catalogue,
        Community,
        Favorites,
        Library
    }

    public ICollectionView FilteredLibrarySongs => _libraryViewSource.View;
    public ICollectionView FilteredPracticeLibrarySongs => _practiceLibraryViewSource.View;
    public ICollectionView FilteredCatalogueTracks => _catalogueViewSource.View;
    public ICollectionView FilteredPlaylistSongs => _playlistViewSource.View;
    public ICollectionView FilteredFavoriteSongs => _favoritesViewSource.View;

    public bool ShowMainPanels => SelectedSection is not NavigationSection.Settings
        and not NavigationSection.Practice;
    public bool ShowSettingsPanel => SelectedSection == NavigationSection.Settings;
    public bool ShowPracticePanel => SelectedSection == NavigationSection.Practice;
    public bool ShowDebraPlayerChrome => SelectedSection != NavigationSection.Practice;
    public bool ShowLibraryPanel => SelectedSection == NavigationSection.Library;
    public bool ShowFavoritesPanel => SelectedSection == NavigationSection.Favorites;
    public bool ShowPlaylistPanel =>
        ShowMainPanels && SelectedSection is not NavigationSection.Settings
            and not NavigationSection.Practice;
    public bool ShowHistoryPanel => SelectedSection == NavigationSection.History;
    public bool ShowCataloguePanel => SelectedSection == NavigationSection.Catalogue;
    public bool ShowCommunityPanel => SelectedSection == NavigationSection.Community;

    public bool ShowPracticeCenterPlay =>
        IsPracticeLessonArmed && !IsPracticePlaying && !IsPracticeCountdownActive && _practiceSession.State == PlaybackState.Stopped;

    public bool ShowPracticeHandPreview =>
        PracticeHandKeyPreview is not null
        && _practiceSession.Notes.Count > 0
        && !IsPracticeCountdownActive
        && (!IsPracticePlaying || IsAcademyPracticeMode);

    public PracticeNoteLabelMode PracticeFallingNoteLabelMode => PracticeNoteLabelMode;

    public bool ShowAcademyFingerLabelsOnNotes => IsAcademyPracticeMode;

    public IReadOnlyList<string> PracticeHandColorSwatches => PracticePrepareService.DefaultTrackColors;

    public PracticeSessionService PracticeSession => _practiceSession;

    public AcademyPanelViewModel AcademyPanel { get; }

    public PracticeKeyboardHighlightService PracticeKeyboardPressState => _practiceKeyboardPress;

    public PracticeKeyboardViewMode PracticeKeyboardViewMode =>
        IsPracticeFullPianoView ? PracticeKeyboardViewMode.FullPiano88 : PracticeKeyboardViewMode.GameAdapted36;

    public PracticeNoteLabelMode PracticeNoteLabelMode =>
        IsPracticeKeyboardLabels ? PracticeNoteLabelMode.KeyboardKeys
        : IsPracticeSolfegeLabels ? PracticeNoteLabelMode.Solfege
        : PracticeNoteLabelMode.LetterNames;

    public IReadOnlyDictionary<int, string> PracticeKeyCombos => _keyMapping.Mapping;

    public FlowDirection UiFlowDirection => LocalizationService.Instance.FlowDirection;

    public string ChromeAutoPlayNextText =>
        !AutoPlayEnabled
            ? L.T(UiText.ChromeAutoPlayNextOff)
            : AutoPlayNextDelaySeconds > 0
                ? L.F(UiText.ChromeAutoPlayNext, AutoPlayNextDelaySeconds)
                : L.T(UiText.ChromeAutoPlayNextImmediate);

    public string SmartTransposeStateLabel =>
        SmartTranspose ? L.T(UiText.ChromeOn) : L.T(UiText.ChromeOff);

    public string SelectedNoteMappingModeDescription =>
        SelectedNoteMappingMode?.Description ?? string.Empty;

    [ObservableProperty] private PlaybackHotkeyRole? _playbackHotkeyCapture;

    public string PlayPauseToolTip => FormatTransportTooltip(_playback.State switch
    {
        PlaybackState.Playing => UiText.PauseTooltip,
        PlaybackState.Paused => UiText.ResumeTooltip,
        _ => UiText.PlayTooltip
    }, PlaybackHotkeyRole.PlayPause);

    public string StopToolTip => FormatTransportTooltip(UiText.ChromeStop, PlaybackHotkeyRole.Stop);

    public string PreviousToolTip => FormatTransportTooltip(UiText.PreviousTooltip, PlaybackHotkeyRole.Previous);

    public string NextToolTip => FormatTransportTooltip(UiText.NextTooltip, PlaybackHotkeyRole.Next);

    public string PlaybackHotkeyPlayPauseLabel => GetPlaybackHotkeyLabel(PlaybackHotkeyRole.PlayPause);

    public string PlaybackHotkeyStopLabel => GetPlaybackHotkeyLabel(PlaybackHotkeyRole.Stop);

    public string PlaybackHotkeyPreviousLabel => GetPlaybackHotkeyLabel(PlaybackHotkeyRole.Previous);

    public string PlaybackHotkeyNextLabel => GetPlaybackHotkeyLabel(PlaybackHotkeyRole.Next);

    public string PlaybackHotkeyCaptureStatus =>
        PlaybackHotkeyCapture is null ? string.Empty : L.T(UiText.KeybindEditorPressKey);

    public bool IsTempoSliderEnabled => _nowPlaying is not null;

    public bool CanResetPlaybackTempo => IsTempoSliderEnabled && PlaybackTempoPercent != 100;

    public bool CanSaveSongTempo =>
        _nowPlaying is not null && PlaybackTempoPercent != _sessionTempoPercent;

    public string PlaybackTempoDisplay =>
        _nowPlaying is null ? "—" : $"{EffectiveTempoBpm}";

    public int EffectiveTempoBpm =>
        (int)Math.Round(SongTempoBpm * PlaybackTempoPercent / 100.0, MidpointRounding.AwayFromZero);

    public string PracticeTempoDisplay => $"{PracticeTempoPercent}%";

    public string PracticeSoundToggleLabel =>
        IsPracticeSoundEnabled ? L.T(UiText.PracticeSoundOn) : L.T(UiText.PracticeSoundOff);

    public string PracticeGameSoundOnlyToggleLabel =>
        IsPracticeGameSoundOnly ? L.T(UiText.PracticeGameSoundOnlyOn) : L.T(UiText.PracticeGameSoundOnlyOff);

    private static string AllStylesLabel => L.T(UiText.AllStyles);

    public MainViewModel()
    {
        var uiDispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

        _songMetadataCache.Load();
        _playlistService = new PlaylistService(_midiParser, _noteRange, _songMetadataCache);
        _library = new LibraryService(_playlistService);
        _midiPlaybackPreparer = new MidiPlaybackPreparer(_midiParser, _noteRange);
        _input = new InputService(_gameWindow);
        _playback = new PlaybackEngine(_input);
        _liveMidi = new MidiLiveInputService(_input, _keyMapping, _noteRange, BuildLiveMidiContext);
        _liveMidi.DevicesChanged += (_, _) => UiDispatcher.Post(RefreshMidiInputDevices);
        _liveMidi.RawNoteOn += OnLiveRawNote;
        _liveMidi.RawNoteOff += OnLiveRawNoteOff;
        _practicePrepare = new PracticePrepareService(_midiParser, _noteRange);
        _practiceSession = new PracticeSessionService();
        _practiceSound = new PracticeSoundService(_midiSoundEngine);
        _practiceSession.PositionChanged += OnPracticePositionChanged;
        _practiceSession.StateChanged += OnPracticeStateChanged;
        _practiceSession.Completed += OnPracticeCompleted;
        _practiceSession.WaitingNotesChanged += OnPracticeWaitingNotesChanged;

        AcademyPanel = new AcademyPanelViewModel(
            () => _discordCredentials,
            () => _settings.Settings.CompletedAcademyLessonIds,
            MarkAcademyLessonComplete,
            PreviewAcademyLessonAsync,
            ReadyAcademyLessonAsync,
            ListenAcademyLessonAsync,
            () => IsPracticeAcademyOverlayOpen = false,
            () => IsPracticeAcademyOverlayOpen,
            () => (
                _settings.Settings.LastAcademyModuleId,
                _settings.Settings.LastAcademyExerciseLessonId,
                _settings.Settings.LastAcademySongLessonId,
                _settings.Settings.LastAcademyLessonId),
            (moduleId, exerciseId, songId, lessonId) =>
            {
                _settings.Settings.LastAcademyModuleId = moduleId;
                _settings.Settings.LastAcademyExerciseLessonId = exerciseId;
                _settings.Settings.LastAcademySongLessonId = songId;
                _settings.Settings.LastAcademyLessonId = lessonId;
                ScheduleSettingsSave();
            });

        _libraryViewSource.Source = LibrarySongs;
        _libraryViewSource.View.Filter = FilterLibrarySong;
        LibrarySongs.CollectionChanged += (_, _) =>
        {
            ScheduleRefreshLibraryStats();
            RefreshFavoriteSongs();
            NotifyTrashCommandsCanExecute();
            ClearLibraryCommand.NotifyCanExecuteChanged();
            if (!_suppressLibraryPersist)
            {
                SyncPersistedLibraryPaths();
                ScheduleSettingsSave();
            }
        };

        _practiceLibraryViewSource.Source = PracticeLibrarySongs;
        _practiceLibraryViewSource.View.Filter = FilterPracticeLibrarySong;
        PracticeLibrarySongs.CollectionChanged += (_, _) =>
        {
            NotifyTrashCommandsCanExecute();
            ClearPracticeLibraryCommand.NotifyCanExecuteChanged();
            if (!_suppressPracticeLibraryPersist)
            {
                SyncPersistedPracticeLibraryPaths();
                ScheduleSettingsSave();
            }
        };

        _favoritesViewSource.Source = FavoriteSongs;
        _favoritesViewSource.View.Filter = FilterFavoriteSong;
        FavoriteSongs.CollectionChanged += (_, _) =>
        {
            RefreshFavoritesStats();
            NotifyTrashCommandsCanExecute();
        };

        _catalogueViewSource.Source = CatalogueTracks;
        _catalogueViewSource.View.Filter = FilterCatalogueTrack;
        CatalogueTracks.CollectionChanged += (_, _) =>
        {
            if (!_suppressCatalogueStats)
                ScheduleRefreshCatalogueView();
        };

        _playlistViewSource.Source = PlaylistSongs;
        _playlistViewSource.View.Filter = FilterPlaylistSong;
        PlaylistSongs.CollectionChanged += (_, _) =>
        {
            if (_suppressPlaylistCollectionUpdates)
                return;

            ScheduleRefreshPlaylistStats();
            NotifyTrashCommandsCanExecute();
        };

        _playback.PlaybackCompleted += (_, _) => UiDispatcher.Post(OnPlaybackCompleted);
        _playback.StateChanged += (_, state) => UiDispatcher.Post(() =>
        {
            if (_suppressPlaybackUi)
                return;

            IsPlaying = state == PlaybackState.Playing;
            RefreshPlayPauseUi();
        });

        _uiTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _uiTimer.Tick += OnUiTimerTick;
        _uiTimer.Start();

        _globalHotkey = new GlobalPlaybackHotkeyService(
            _gameWindow,
            IsPlaybackHotkeyContextActive,
            () => _nowPlaying is not null &&
                  _playback.State is PlaybackState.Playing or PlaybackState.Paused,
            () => PlayPauseCommand.Execute(null),
            () => StopCommand.Execute(null),
            () => PreviousCommand.Execute(null),
            () => NextCommand.Execute(null),
            () => SeekRelativeSeconds(-5),
            () => SeekRelativeSeconds(5),
            uiDispatcher);

        NavItems.Add(new NavItemViewModel { Section = NavigationSection.Library, Icon = "📚" });
        NavItems.Add(new NavItemViewModel { Section = NavigationSection.Catalogue, Icon = "☁" });
        NavItems.Add(new NavItemViewModel { Section = NavigationSection.Community, Icon = "🌐" });
        NavItems.Add(new NavItemViewModel { Section = NavigationSection.Practice, Icon = "🎹" });
        NavItems.Add(new NavItemViewModel { Section = NavigationSection.Favorites, Icon = "♥" });
        NavItems.Add(new NavItemViewModel { Section = NavigationSection.History, Icon = "🕐" });
        NavItems.Add(new NavItemViewModel { Section = NavigationSection.Settings, Icon = "⚙" });

        InitializeCommunity();

        AvailableLanguages.Add(new LanguageOption { Code = "en", DisplayName = "English" });
        AvailableLanguages.Add(new LanguageOption { Code = "es", DisplayName = "Español" });
        AvailableLanguages.Add(new LanguageOption { Code = "fr", DisplayName = "Français" });
        AvailableLanguages.Add(new LanguageOption { Code = "pt", DisplayName = "Português" });
        AvailableLanguages.Add(new LanguageOption { Code = "zh", DisplayName = "中文" });
        AvailableLanguages.Add(new LanguageOption { Code = "ja", DisplayName = "日本語" });
        AvailableLanguages.Add(new LanguageOption { Code = "de", DisplayName = "Deutsch" });
        AvailableLanguages.Add(new LanguageOption { Code = "it", DisplayName = "Italiano" });
        AvailableLanguages.Add(new LanguageOption { Code = "ar", DisplayName = "العربية" });
        AvailableLanguages.Add(new LanguageOption { Code = "vi", DisplayName = "Tiếng Việt" });

        AvailableThemes.Add(new ThemeOption { Id = ThemeService.Sakura, DisplayName = "Sakura" });
        AvailableThemes.Add(new ThemeOption { Id = ThemeService.Wuxia, DisplayName = "Wuxia Dark" });
        AvailableThemes.Add(new ThemeOption { Id = ThemeService.Ffxiv, DisplayName = "Eorzea Night" });

        LocalizationService.Instance.LanguageChanged += (_, _) => ScheduleApplyLocalization();

        Initialize();
    }

    /// <summary>Call after the main window is loaded (safe for single-file publish).</summary>
    public void StartGlobalHotkey()
    {
        try
        {
            _globalHotkey.Start();
        }
        catch (Exception ex)
        {
            AppPaths.WriteDiagnosticLog("global-hotkey", ex);
        }
    }

    private void Initialize()
    {
        AppPaths.EnsureCreated();

        try
        {
            _settings.Load();
        }
        catch (Exception ex)
        {
            AppPaths.WriteDiagnosticLog("settings-load", ex);
        }

        // One-time fix: earlier builds wrote FFXIV keymaps that didn't match the game's
        // "Assign all notes to keyboard" keybinds (v1 = WWM Shift=sharp scheme, v2 = piano-row scheme).
        if (_settings.Settings.FfxivKeyMapVersion < 2)
        {
            _keyMapping.RegenerateFinalFantasyXivDefaultMaps();
            _settings.Settings.FfxivKeyMapVersion = 2;
        }

        ApplyGameProfileFromSettings();
        InitializeFfxivChat();

        try
        {
            _history.Load();
        }
        catch (Exception ex)
        {
            AppPaths.WriteDiagnosticLog("history-load", ex);
        }

        try
        {
            _songTempo.Load();
        }
        catch (Exception ex)
        {
            AppPaths.WriteDiagnosticLog("song-tempo-load", ex);
        }

        try
        {
            _songPlayback.Load();
        }
        catch (Exception ex)
        {
            AppPaths.WriteDiagnosticLog("song-playback-load", ex);
        }

        SmartTranspose = _settings.Settings.SmartTranspose;
        StrictNoteRange = _settings.Settings.StrictNoteRange;
        NoteDelayMs = _settings.Settings.NoteDelayMs;
        ChordRollDelayMs = _settings.Settings.ChordRollDelayMs;
        ModifierDelayMs = _settings.Settings.ModifierDelayMs;
        PlayerChromeOpacityPercent = 100;
        SelectedNoteMappingMode = null;
        AutoPlayEnabled = _settings.Settings.AutoPlayEnabled;
        AutoPlayNextDelaySeconds = _settings.Settings.AutoPlayNextDelaySeconds;
        Shuffle = _settings.Settings.Shuffle;
        Repeat = _settings.Settings.Repeat;
        if (_systemVolume.IsAvailable)
            Volume = _systemVolume.GetMasterVolumePercent();
        else
            Volume = Math.Clamp(_settings.Settings.Volume, 0, 100);

        _settings.Settings.Volume = Volume;
        ApplyVolumeToSystem();
        ApplySynthVolume();
        TargetProcessName = ReconcileGameScopedSetting(
            _settings.Settings.TargetProcessName, p => p.DefaultProcessName);
        GameWindowTitleContains = ReconcileGameScopedSetting(
            _settings.Settings.GameWindowTitleContains, p => p.DefaultWindowTitleContains);
        ReleaseManifestUrl = _settings.Settings.ReleaseManifestUrl ?? string.Empty;
        MigrateDefaultRefreshFolders();
        LibraryRefreshFolderPath = _settings.Settings.LibraryRefreshFolder ?? string.Empty;
        LibraryIncludeSubfolders = _settings.Settings.LibraryIncludeSubfolders;
        PracticeLibraryRefreshFolderPath = _settings.Settings.PracticeLibraryRefreshFolder ?? string.Empty;
        PracticeLibraryIncludeSubfolders = _settings.Settings.PracticeLibraryIncludeSubfolders;
        PracticeRightHandColorHex = _settings.Settings.PracticeRightHandColorHex;
        PracticeLeftHandColorHex = _settings.Settings.PracticeLeftHandColorHex;
        PracticeHandColorResolver.ApplySettings(PracticeRightHandColorHex, PracticeLeftHandColorHex);
        RefreshLibraryFoldersCommand.NotifyCanExecuteChanged();
        RefreshPracticeLibraryFoldersCommand.NotifyCanExecuteChanged();
        FocusGameBeforePlay = false;
        _settings.Settings.FocusGameBeforePlay = false;
        WindowAlwaysOnTop = _settings.Settings.WindowAlwaysOnTop;
        PlaybackHotkeysGlobal = _settings.Settings.PlaybackHotkeysGlobal;
        PrePlayCountdownSeconds = 1;
        _settings.Settings.PrePlayCountdownSeconds = 1;
        ApplyPracticeCountdownFromSettings();
        DiscordCredentialStore.MigrateFromSettings(_settings);
        _discordCredentials = DiscordCredentialStore.Load();

        RebuildNoteMappingModes();
        SelectedNoteMappingMode = NoteMappingModes.FirstOrDefault(m =>
            m.Mode == _settings.Settings.DefaultNoteMappingMode)
            ?? NoteMappingModes.FirstOrDefault();
        ApplyInputAndWindowSettings();
        ApplyUiLanguageFromSettings();
        ApplyUiThemeFromSettings();
        RebuildSongSortOptions();
        ApplySortSettingsFromSaved();
        ApplyPlaybackHotkeysFromSettings();
        RebuildFavoritePathSet();
        SyncAllSongFavoriteFlags();
        RefreshFavoriteSongs();

        EnsureKeyMaps();
        SyncFfxivKeybindsFromGame();
        LoadKeyMapping(ResolveInitialKeyMappingFile());
        RefreshKeyLayouts();

        _liveMidi.StartDevicesWatcher();
        RefreshMidiInputDevices();
        RestoreSavedMidiInputDevice();
        RefreshLiveMidiStatusText();

        ApplyPracticeLabelModeFromSettings();
        ApplyPracticeSoundFromSettings();
        ApplyPracticeGameSoundFromSettings();

        ResetToBlankPlaylist();

        foreach (var item in _history.Items)
            HistoryItems.Add(item);

        HistoryItems.CollectionChanged += (_, _) => RefreshHistoryStats();
        RefreshHistoryStats();

        RefreshSavedPlaylists();

        if (!string.IsNullOrWhiteSpace(_settings.Settings.LastPlaylistPath)
            && File.Exists(_settings.Settings.LastPlaylistPath))
            _deferredPlaylistPath = _settings.Settings.LastPlaylistPath;

        UpdateNavActive();
        RefreshLibraryStats();
        RefreshPlaylistStats();
    }

    private bool _suppressLayoutChange;
    private bool _suppressLibraryPersist;
    private bool _suppressPracticeLibraryPersist;
    private bool _suppressPracticeSongPersist;
    private bool _suppressGameProfileChange;

    public IReadOnlyList<GameProfile> GameOptions => GameProfiles.All;

    public string SelectedGameDisplayName => SelectedGameProfile?.DisplayName ?? GameProfiles.WhereWindsMeet.DisplayName;

    public string WindowTitleText => $"Debra MIDI Player — {SelectedGameDisplayName}";

    private void ApplyGameProfileFromSettings()
    {
        var profile = GameProfiles.Find(_settings.Settings.SelectedGameId);
        GameProfiles.Apply(profile);
        _suppressGameProfileChange = true;
        try
        {
            SelectedGameProfile = profile;
        }
        finally
        {
            _suppressGameProfileChange = false;
        }

        NotifyGameProfileLabels();
    }

    /// <summary>Heals settings saved while another game was selected: a value equal to a
    /// different profile's default snaps back to the current profile's default.</summary>
    private static string ReconcileGameScopedSetting(string? saved, Func<GameProfile, string> defaultOf)
    {
        var currentDefault = defaultOf(GameProfiles.Current);
        if (string.IsNullOrWhiteSpace(saved))
            return currentDefault;

        var belongsToOtherGame = GameProfiles.All.Any(p =>
            !ReferenceEquals(p, GameProfiles.Current)
            && string.Equals(saved, defaultOf(p), StringComparison.OrdinalIgnoreCase)
            && !string.Equals(saved, currentDefault, StringComparison.OrdinalIgnoreCase));

        return belongsToOtherGame ? currentDefault : saved;
    }

    partial void OnSelectedGameProfileChanged(GameProfile value)
    {
        if (_suppressGameProfileChange || value is null)
            return;

        if (_playback.State is PlaybackState.Playing or PlaybackState.Paused)
            StopCommand.Execute(null);

        GameProfiles.Apply(value);
        _settings.Settings.SelectedGameId = value.Id;
        TargetProcessName = value.DefaultProcessName;
        GameWindowTitleContains = value.DefaultWindowTitleContains;
        _gameWindow.ClearCache();

        EnsureKeyMaps();
        SyncFfxivKeybindsFromGame();
        RefreshKeyLayouts();
        LoadKeyMapping(ResolveGameKeyMappingFile(value, _settings.Settings.KeyboardLayoutPresetId));
        RebuildNoteMappingModes();
        ApplyThemeForGame(value);
        NotifyGameProfileLabels();
        UpdateFfxivChatForGame();
        ScheduleSettingsSave();
        _ = RefreshGameConnectionStatusAsync();
    }

    /// <summary>Probes the newly targeted game right away so the status reflects the switch.</summary>
    private async Task RefreshGameConnectionStatusAsync()
    {
        var name = TargetProcessName;
        var (running, found) = await Task.Run(() =>
        {
            _gameWindow.ClearCache();
            var isRunning = _gameWindow.IsProcessRunning();
            return (isRunning, isRunning && _gameWindow.IsGameWindowFound());
        }).ConfigureAwait(false);

        await UiDispatcher.RunAsync(() => GameConnectionStatus = found
            ? $"{name}: window found — ready to play."
            : running
                ? $"{name}: running, window not detected yet."
                : $"{name}: not running.").ConfigureAwait(true);
    }

    /// <summary>
    /// FFXIV adapts to each player's own layout: reads the game's KEYBIND.DAT (like LightAmp)
    /// and rewrites the default FFXIV keymap with the player's actual Performance keybinds.
    /// Falls back to the bundled "assign all notes" default map when the DAT can't be read.
    /// </summary>
    private void SyncFfxivKeybindsFromGame()
    {
        if (GameProfiles.Current != GameProfiles.FinalFantasyXiv)
            return;

        try
        {
            if (!FfxivKeybindReader.TryReadPerformanceKeybinds(out var map, out var source))
                return;

            _keyMapping.WriteKeyMapFile(GameProfiles.FinalFantasyXiv.DefaultKeyMapFile, map);
            // The synced game layout becomes the active map (not a QWERTY/AZERTY preset).
            _settings.Settings.KeyboardLayoutPresetId = null;
            _settings.Settings.KeyMappingFile = GameProfiles.FinalFantasyXiv.DefaultKeyMapFile;
            _ = source; // e.g. "FFXIV_CHR0123… (37/37)" — surfaced later if we add a status line
        }
        catch (Exception ex)
        {
            AppPaths.WriteDiagnosticLog("ffxiv-keybind-sync", ex);
        }
    }

    private void NotifyGameProfileLabels()
    {
        OnPropertyChanged(nameof(SelectedGameDisplayName));
        OnPropertyChanged(nameof(WindowTitleText));
    }

    private static string ResolveGameKeyMappingFile(GameProfile game, string? presetId)
    {
        var preset = string.IsNullOrWhiteSpace(presetId) ? null : GameKeyboardLayoutPresets.Find(presetId);
        return preset is null ? game.DefaultKeyMapFile : game.KeyMapFileName(preset.FileName);
    }

    private void EnsureKeyMaps()
    {
        // Seed bundled defaults only when missing — never overwrite user-edited keymaps on startup.
        _keyMapping.EnsureDefaultKeyMap("default-keymap.json");
        _keyMapping.EnsureDefaultKeyMap(GameProfiles.Current.DefaultKeyMapFile);
        _keyMapping.EnsurePresetKeyMaps();
    }

    private string ResolveInitialKeyMappingFile()
    {
        var game = GameProfiles.Current;
        if (!string.IsNullOrWhiteSpace(_settings.Settings.KeyboardLayoutPresetId))
        {
            var preset = GameKeyboardLayoutPresets.Find(_settings.Settings.KeyboardLayoutPresetId);
            if (preset is not null)
                return game.KeyMapFileName(preset.FileName);
        }

        var saved = _settings.Settings.KeyMappingFile;
        if (GameProfiles.FileBelongsTo(game, saved))
            return saved;

        return game.DefaultKeyMapFile;
    }

    private void RefreshKeyboardLayoutPresets()
    {
        var selectedId = _settings.Settings.KeyboardLayoutPresetId;
        if (string.IsNullOrWhiteSpace(selectedId))
            selectedId = KeyboardLayoutPresetUiHelper.DetectPresetId(_keyMapping.CloneMapping());

        KeyboardLayoutPresetUiHelper.RefreshPresets(KeyboardLayoutPresets, selectedId);
    }

    [RelayCommand]
    private void ApplyKeyboardLayoutPreset(string presetId)
    {
        var preset = GameKeyboardLayoutPresets.Find(presetId);
        if (preset is null)
            return;

        var fileName = GameProfiles.Current.KeyMapFileName(preset.FileName);
        _keyMapping.EnsureDefaultKeyMap(fileName);
        LoadKeyMapping(fileName, preset.Id);
    }

    [RelayCommand]
    private void ResetNoteKeysToDefault()
    {
        ApplyKeyboardLayoutPreset(GameKeyboardLayoutPresets.QwertyId);
    }

    private void RefreshKeyLayouts()
    {
        KeyLayouts.Clear();
        var game = GameProfiles.Current;
        foreach (var file in Directory.GetFiles(AppPaths.KeyMapsFolder, "*.json").OrderBy(f => f))
        {
            var name = Path.GetFileName(file);
            if (!GameProfiles.FileBelongsTo(game, name))
                continue;

            var display = name switch
            {
                "debra-36-keys.json" => "Debra 36 Keys",
                "default-keymap.json" => "Default 36 Keys",
                "ffxiv-37-keys.json" => "FFXIV 37 Keys",
                _ => GameProfiles.StripKeyMapPrefix(name) switch
                {
                    "preset-qwerty.json" => L.T(UiText.SettingsNoteKeysPresetQwerty),
                    "preset-qwertz.json" => L.T(UiText.SettingsNoteKeysPresetQwertz),
                    "preset-azerty.json" => L.T(UiText.SettingsNoteKeysPresetAzerty),
                    _ => Path.GetFileNameWithoutExtension(name)
                }
            };
            KeyLayouts.Add(new KeyLayoutOption { FileName = name, DisplayName = display });
        }

        var pick = KeyLayouts.FirstOrDefault(k => k.FileName == _settings.Settings.KeyMappingFile)
                   ?? KeyLayouts.FirstOrDefault();
        _suppressLayoutChange = true;
        try
        {
            SelectedLayout = pick;
        }
        finally
        {
            _suppressLayoutChange = false;
        }
    }

    private void LoadKeyMapping(string fileName, string? presetId = null)
    {
        var path = Path.Combine(AppPaths.KeyMapsFolder, fileName);
        if (!File.Exists(path))
            path = _keyMapping.EnsureDefaultKeyMap(fileName);
        _keyMapping.LoadFromFile(path);
        _settings.Settings.KeyMappingFile = fileName;
        _settings.Settings.KeyboardLayoutPresetId = presetId
            ?? GameKeyboardLayoutPresets.FindByFileName(GameProfiles.StripKeyMapPrefix(fileName))?.Id;
        _settings.Save();
        OnPropertyChanged(nameof(PracticeKeyCombos));

        _suppressLayoutChange = true;
        try
        {
            SelectedLayout = KeyLayouts.FirstOrDefault(k => k.FileName == fileName) ?? SelectedLayout;
        }
        finally
        {
            _suppressLayoutChange = false;
        }

        RefreshKeyboardLayoutPresets();
    }

    private void OnLiveRawNote(int rawMidi, int velocity) =>
        UiDispatcher.Post(() => HandleLiveRawNote(rawMidi, velocity));

    private void HandleLiveRawNote(int rawMidi, int velocity)
    {
        UpdatePracticeKeyboardPress(rawMidi, pressed: true);

        if (SelectedSection == NavigationSection.Practice)
            TryPlayPracticeLiveNote(rawMidi, velocity);

        if (SelectedSection != NavigationSection.Practice)
            return;

        var ctx = BuildLiveMidiContext();
        var shifted = rawMidi + ctx.OctaveShift * 12;
        var mapped = LiveMidiMapper.MapToGameNoteNumber(
            rawMidi,
            velocity,
            _noteRange,
            ctx.SmartTranspose,
            ctx.StrictNoteRange,
            ctx.OctaveShift,
            ctx.MappingMode);
        _practiceSession.RecordPressedNote(shifted, mapped);

        if (_practiceSession.IsWaitingForInput)
        {
            _practiceSession.TryRegisterHitFromRawMidi(
                rawMidi,
                velocity,
                _noteRange,
                ctx.SmartTranspose,
                ctx.StrictNoteRange,
                ctx.OctaveShift,
                ctx.MappingMode);
            ReconcilePracticeInput();
        }
    }

    public bool TryHandlePracticeTransportKey(Key key)
    {
        if (SelectedSection != NavigationSection.Practice || key != Key.Space)
            return false;

        if (PlaybackHotkeyCapture is not null || IsTextInputFocused())
            return false;

        if (IsPracticeCountdownActive)
            return true;

        if (_practiceSession.State == PlaybackState.Stopped)
        {
            if (ShowPracticeCenterPlay)
                ConfirmPracticePlayCommand.Execute(null);
            else
                StartPracticeCommand.Execute(null);
            return true;
        }

        PausePracticeCommand.Execute(null);
        return true;
    }

    public bool TryHandlePracticeKeyDown(Key key, ModifierKeys modifiers, bool isRepeat)
    {
        if (!IsPracticePcInputActive)
            return false;

        if (PlaybackHotkeyCapture is not null)
            return false;

        if (IsTextInputFocused())
            return false;

        if (!KeyComboParser.TryFromWpfKey(key, modifiers, out var combo))
            return false;

        var gameNote = _keyMapping.TryGetNoteForCombo(combo);
        if (gameNote is null)
            return false;

        var alreadyHeld = _practicePcKeysHeld.Contains(combo);
        if (!alreadyHeld)
            _practicePcKeysHeld.Add(combo);

        // OS key-repeat while holding: skip duplicate game taps; staccato re-presses still send.
        if (!isRepeat && !IsAcademyPracticeMode && !_practiceSession.IsInPlaybackLeadIn)
            _input.QueuePressKeyCombo(combo);

        if (!alreadyHeld)
            HandlePracticeGameNotePress(gameNote.Value);

        return true;
    }

    public void TryHandlePracticeKeyUp(Key key, ModifierKeys modifiers)
    {
        if (!IsPracticePcInputActive)
            return;

        if (key is Key.LeftShift or Key.RightShift)
        {
            ReleaseHeldPracticeCombosWithModifier("Shift");
            return;
        }

        if (key is Key.LeftCtrl or Key.RightCtrl)
        {
            ReleaseHeldPracticeCombosWithModifier("Ctrl");
            return;
        }

        if (!KeyComboParser.TryGetMainKeyFromWpfKey(key, out var mainKey))
            return;

        var toRelease = _practicePcKeysHeld
            .Where(held =>
            {
                var heldMain = KeyComboParser.GetMainKeyToken(held);
                return heldMain is not null &&
                    heldMain.Equals(mainKey, StringComparison.OrdinalIgnoreCase);
            })
            .ToList();

        if (KeyComboParser.TryFromWpfKey(key, modifiers, out var exact) && _practicePcKeysHeld.Contains(exact))
        {
            if (!toRelease.Contains(exact))
                toRelease.Add(exact);
        }

        foreach (var combo in toRelease)
            ReleasePracticePcCombo(combo);
    }

    private void ReleaseHeldPracticeCombosWithModifier(string modifier)
    {
        foreach (var combo in _practicePcKeysHeld.Where(c => KeyComboParser.HasModifierPrefix(c, modifier)).ToList())
            ReleasePracticePcCombo(combo);
    }

    private void ReleasePracticePcCombo(string combo)
    {
        if (!_practicePcKeysHeld.Remove(combo))
            return;

        var gameNote = _keyMapping.TryGetNoteForCombo(combo);
        if (gameNote is null)
            return;

        HandlePracticeGameNoteRelease(gameNote.Value);
    }

    private bool IsPracticeFreePlayActive =>
        SelectedSection == NavigationSection.Practice &&
        _practiceSession.State == PlaybackState.Stopped;

    /// <summary>Chart synth is playing along with the session (not lead-in).</summary>
    private bool IsPracticeChartSoundActive =>
        IsPracticeSoundEnabled &&
        _practiceSession.State == PlaybackState.Playing &&
        _practiceSession.CurrentPositionMs >= 0;

    /// <summary>Live MIDI/PC preview is muted while chart playback sound is active to avoid doubles.</summary>
    private bool ShouldSuppressLivePracticeInputSound =>
        IsPracticeChartSoundActive;

    private bool IsPracticePcInputActive =>
        SelectedSection == NavigationSection.Practice &&
        (_practiceSession.State is PlaybackState.Playing or PlaybackState.Paused ||
         IsPracticeFreePlayActive);

    private void HandlePracticeGameNotePress(int gameNoteNumber, int velocity = 127)
    {
        _practiceKeyboardPress.PressGame(gameNoteNumber);
        _practiceKeyboardPress.PressDisplay(gameNoteNumber);
        _practiceSession.RecordPressedNote(gameNoteNumber, gameNoteNumber);
        _practiceSession.TryRegisterHit(gameNoteNumber);
        ReconcilePracticeInput();

        TryPlayPracticeLiveNote(gameNoteNumber, velocity);
    }

    private void HandlePracticeGameNoteRelease(int gameNoteNumber)
    {
        _practiceKeyboardPress.ReleaseGame(gameNoteNumber);
        _practiceKeyboardPress.ReleaseDisplay(gameNoteNumber);

        TryStopPracticeLiveNote(gameNoteNumber);
    }

    private void TryPlayPracticeLiveNote(int noteNumber, int velocity)
    {
        if (SelectedSection != NavigationSection.Practice || ShouldSuppressLivePracticeInputSound || IsPracticeGameSoundOnly)
            return;

        if (!_practiceSound.IsEnabled)
            SyncPracticeSoundState();

        if (_practiceSound.IsEnabled)
            _practiceSound.PlayLiveNote(noteNumber, velocity);
    }

    private void TryStopPracticeLiveNote(int noteNumber)
    {
        if (SelectedSection != NavigationSection.Practice || ShouldSuppressLivePracticeInputSound || IsPracticeGameSoundOnly)
            return;

        if (!_practiceSound.IsEnabled)
            SyncPracticeSoundState();

        if (_practiceSound.IsEnabled)
            _practiceSound.StopLiveNote(noteNumber);
    }

    private static bool IsTextInputFocused()
    {
        var focused = Keyboard.FocusedElement;
        return focused is System.Windows.Controls.Primitives.TextBoxBase;
    }

    private void ReconcilePracticeInput()
    {
        if (!_practiceSession.IsWaitingForInput)
            return;

        _practiceSession.TryReconcileActiveInput(
            _practiceKeyboardPress.ActiveGameNotes,
            _practiceKeyboardPress.ActiveDisplayNotes);
    }

    private void OnLiveRawNoteOff(int rawMidi) =>
        UiDispatcher.Post(() => HandleLiveRawNoteOff(rawMidi));

    private void HandleLiveRawNoteOff(int rawMidi)
    {
        UpdatePracticeKeyboardPress(rawMidi, pressed: false);

        if (SelectedSection == NavigationSection.Practice)
            TryStopPracticeLiveNote(rawMidi);
    }

    private void UpdatePracticeKeyboardPress(int rawMidi, bool pressed)
    {
        var ctx = BuildLiveMidiContext();
        var shifted = rawMidi + ctx.OctaveShift * 12;
        var mapped = LiveMidiMapper.MapToGameNoteNumber(
            rawMidi,
            127,
            _noteRange,
            ctx.SmartTranspose,
            ctx.StrictNoteRange,
            ctx.OctaveShift,
            ctx.MappingMode);

        if (pressed)
        {
            _practiceKeyboardPress.PressDisplay(shifted);
            if (mapped is not null)
            {
                _practiceKeyboardPress.PressGame(mapped.Value);
                if (mapped.Value != shifted)
                    _practiceKeyboardPress.PressDisplay(mapped.Value);
            }
        }
        else
        {
            _practiceKeyboardPress.ReleaseDisplay(shifted);
            if (mapped is not null)
            {
                _practiceKeyboardPress.ReleaseGame(mapped.Value);
                if (mapped.Value != shifted)
                    _practiceKeyboardPress.ReleaseDisplay(mapped.Value);
            }
        }
    }

    public void ShowLibrarySection()
    {
        if (SelectedSection != NavigationSection.Library)
            Navigate(NavigationSection.Library);
    }

    public event Action? TourRequested;
    public event Action? PracticeTourRequested;

    [RelayCommand]
    private void ShowHelp() => TourRequested?.Invoke();

    [RelayCommand]
    private void ShowPracticeTour() => RequestPracticeTour(force: true);

    public const string DiscordInviteUrl = "https://discord.gg/uVyXZ3QFpd";

    [RelayCommand]
    private void ShowPatreonRewards()
    {
        var owner = Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                    ?? Application.Current?.MainWindow;
        PatreonRewardsWindow.ShowForOwner(owner);
    }

    [RelayCommand]
    private void OpenDiscord()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = DiscordInviteUrl,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            DebraDialogs.Error("Discord", ex.Message);
        }
    }

    public async Task CheckForUpdatesOnStartupAsync()
    {
        try
        {
            await RefreshUpdateAvailabilityAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            AppPaths.WriteDiagnosticLog("update-check-startup", ex);
        }
    }

    [RelayCommand]
    private async Task CheckForUpdates()
    {
        await RefreshUpdateAvailabilityAsync().ConfigureAwait(true);
        if (IsUpdateAvailable)
            ShowUpdateOverlay();
    }

    private async Task RefreshUpdateAvailabilityAsync()
    {
        var manifest = await _appUpdate.ResolveManifestAsync(_settings.Settings, _discordCredentials)
            .ConfigureAwait(false);
        var available = _appUpdate.IsUpdateAvailable(manifest, _settings.Settings.LastDismissedUpdateVersion);

        if (manifest is null)
            AppPaths.WriteDiagnosticLog("update-check", new InvalidOperationException(
                "No release manifest resolved. Ensure discord-catalogue.json includes releaseManifestChannelId and releaseManifestMessageId, or set a manifest URL in Settings."));

        await UiDispatcher.RunAsync(() =>
        {
            _pendingUpdateManifest = available ? manifest : null;
            IsUpdateAvailable = available;
        }).ConfigureAwait(false);
    }

    [RelayCommand]
    private void ShowUpdateOverlay()
    {
        if (_pendingUpdateManifest is null)
            return;

        var owner = Application.Current?.MainWindow;
        Windows.UpdateOverlayWindow? window = null;
        var vm = new UpdateOverlayViewModel(
            _pendingUpdateManifest,
            _appUpdate,
            dismissedVersion =>
            {
                if (!string.IsNullOrWhiteSpace(dismissedVersion))
                {
                    _settings.Settings.LastDismissedUpdateVersion = dismissedVersion;
                    _settings.Save();
                    IsUpdateAvailable = false;
                }
            },
            () => window?.Close());

        window = new Windows.UpdateOverlayWindow(vm) { Owner = owner };
        window.ShowDialog();
    }

    [RelayCommand]
    private void Navigate(NavigationSection? section)
    {
        if (section is null)
            return;
        SelectedSection = section.Value;
        UpdateNavActive();
        OnPropertyChanged(nameof(ShowMainPanels));
        OnPropertyChanged(nameof(ShowSettingsPanel));
        OnPropertyChanged(nameof(ShowPracticePanel));
        OnPropertyChanged(nameof(ShowLibraryPanel));
        OnPropertyChanged(nameof(ShowHistoryPanel));
        OnPropertyChanged(nameof(ShowCataloguePanel));
        OnPropertyChanged(nameof(ShowCommunityPanel));
        OnPropertyChanged(nameof(ShowFavoritesPanel));
        OnPropertyChanged(nameof(ShowPlaylistPanel));
    }

    private void UpdateNavActive()
    {
        foreach (var item in NavItems)
            item.IsActive = item.Section == SelectedSection;
    }

    partial void OnLibrarySearchTextChanged(string value) => ScheduleRefreshLibraryView();

    partial void OnPracticeLibrarySearchTextChanged(string value) => _practiceLibraryViewSource.View.Refresh();

    partial void OnCatalogueSearchTextChanged(string value) => ScheduleRefreshCatalogueView();

    partial void OnCatalogueStyleFilterChanged(string? value)
    {
        ApplyCatalogueSort();
        ScheduleRefreshCatalogueView();
    }

    partial void OnSelectedLibrarySortOptionChanged(SongSortOption? value)
    {
        if (_suppressSortChange || value is null)
            return;

        SongListSortHelper.Apply(_libraryViewSource, value.Mode);
        _settings.Settings.LibrarySortMode = value.Mode.ToString();
        ScheduleSettingsSave();
        ScheduleRefreshLibraryView();
    }

    partial void OnSelectedPlaylistSortOptionChanged(SongSortOption? value)
    {
        if (_suppressSortChange || value is null)
            return;

        SongListSortHelper.Apply(_playlistViewSource, value.Mode);
        _settings.Settings.PlaylistSortMode = value.Mode.ToString();
        ScheduleSettingsSave();
        OnPropertyChanged(nameof(IsPlaylistManualSort));
        MovePlaylistSongUpCommand.NotifyCanExecuteChanged();
        MovePlaylistSongDownCommand.NotifyCanExecuteChanged();
        _playlistViewSource.View.Refresh();
    }

    partial void OnSelectedCatalogueSortOptionChanged(CatalogueSortOption? value)
    {
        if (_suppressSortChange || value is null)
            return;

        ApplyCatalogueSort();
        _settings.Settings.CatalogueSortMode = value.Mode.ToString();
        ScheduleSettingsSave();
        ScheduleRefreshCatalogueView();
    }

    private void ScheduleRefreshCatalogueView()
    {
        if (_catalogueViewRefreshScheduled)
            return;

        _catalogueViewRefreshScheduled = true;
        var dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
        dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            _catalogueViewRefreshScheduled = false;
            _catalogueViewSource.View.Refresh();
            RefreshCatalogueStats();
        });
    }

    private void ScheduleRefreshLibraryView()
    {
        if (_libraryViewRefreshScheduled)
            return;

        _libraryViewRefreshScheduled = true;
        var dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
        dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            _libraryViewRefreshScheduled = false;
            _libraryViewSource.View.Refresh();
            RefreshLibraryStats();
        });
    }

    private void ScheduleRefreshFavoritesView()
    {
        if (_favoritesViewRefreshScheduled)
            return;

        _favoritesViewRefreshScheduled = true;
        var dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
        dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            _favoritesViewRefreshScheduled = false;
            _favoritesViewSource.View.Refresh();
            RefreshFavoritesStats();
        });
    }

    private void ScheduleSettingsSave(int delayMs = 400)
    {
        _settingsSaveDebounce?.Cancel();
        _settingsSaveDebounce = new CancellationTokenSource();
        var token = _settingsSaveDebounce.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(delayMs, token).ConfigureAwait(false);
                if (!token.IsCancellationRequested)
                    _settings.Save();
            }
            catch (OperationCanceledException)
            {
                // superseded by a newer save request
            }
        }, token);
    }

    private void UpdateCatalogueStatsText(int count)
    {
        var word = count == 1 ? L.T(UiText.StatsTrack) : L.T(UiText.StatsTracks);
        CatalogueStatsText = $"{count} {word}";
    }

    public string PlaylistSectionActiveName
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(PlaylistName))
                return PlaylistName.Trim();

            return SelectedSavedPlaylist?.Name ?? string.Empty;
        }
    }

    private void NotifyPlaylistSectionActiveName() => OnPropertyChanged(nameof(PlaylistSectionActiveName));

    private bool FilterLibrarySong(object obj)
    {
        if (obj is not Song song)
            return false;
        if (string.IsNullOrWhiteSpace(LibrarySearchText))
            return true;
        return song.DisplayTitle.Contains(LibrarySearchText, StringComparison.OrdinalIgnoreCase)
               || song.Title.Contains(LibrarySearchText, StringComparison.OrdinalIgnoreCase)
               || song.FilePath.Contains(LibrarySearchText, StringComparison.OrdinalIgnoreCase);
    }

    private bool FilterPracticeLibrarySong(object obj)
    {
        if (obj is not Song song)
            return false;
        if (string.IsNullOrWhiteSpace(PracticeLibrarySearchText))
            return true;
        return song.DisplayTitle.Contains(PracticeLibrarySearchText, StringComparison.OrdinalIgnoreCase)
               || song.Title.Contains(PracticeLibrarySearchText, StringComparison.OrdinalIgnoreCase)
               || song.FilePath.Contains(PracticeLibrarySearchText, StringComparison.OrdinalIgnoreCase);
    }

    private bool FilterPlaylistSong(object obj)
    {
        if (obj is not Song song)
            return false;

        if (!PlaylistFavoritesOnly)
            return true;

        return IsSongFavorite(song);
    }

    private void RebuildFavoritePathSet() =>
        _favoritePathSet = new HashSet<string>(_settings.Settings.FavoritePaths, StringComparer.OrdinalIgnoreCase);

    private bool IsSongFavorite(Song song) =>
        !string.IsNullOrWhiteSpace(song.FilePath) && _favoritePathSet.Contains(song.FilePath);

    private void SyncSongFavoriteFlag(Song song) => song.IsFavorite = IsSongFavorite(song);

    private void SyncAllSongFavoriteFlags()
    {
        foreach (var song in LibrarySongs)
            SyncSongFavoriteFlag(song);
        foreach (var song in PlaylistSongs)
            SyncSongFavoriteFlag(song);
        foreach (var song in FavoriteSongs)
            SyncSongFavoriteFlag(song);
    }

    private void SetSongFavorite(Song song, bool favorite)
    {
        var favorites = _settings.Settings.FavoritePaths;
        if (favorite)
        {
            if (!favorites.Contains(song.FilePath, StringComparer.OrdinalIgnoreCase))
                favorites.Add(song.FilePath);
        }
        else
        {
            favorites.RemoveAll(p => p.Equals(song.FilePath, StringComparison.OrdinalIgnoreCase));
        }

        song.IsFavorite = favorite;
        if (_nowPlaying is not null
            && _nowPlaying.FilePath.Equals(song.FilePath, StringComparison.OrdinalIgnoreCase))
            IsFavorite = favorite;

        RebuildFavoritePathSet();
        _settings.Save();
        SyncAllSongFavoriteFlags();
        RefreshFavoriteSongs();
        _playlistViewSource.View.Refresh();
        RefreshPlaylistStats();
    }

    private void RefreshFavoriteSongs()
    {
        var ordered = new List<Song>();
        foreach (var path in _settings.Settings.FavoritePaths)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                continue;

            var song = LibrarySongs.FirstOrDefault(s => s.FilePath.Equals(path, StringComparison.OrdinalIgnoreCase))
                       ?? PlaylistSongs.FirstOrDefault(s => s.FilePath.Equals(path, StringComparison.OrdinalIgnoreCase))
                       ?? FavoriteSongs.FirstOrDefault(s => s.FilePath.Equals(path, StringComparison.OrdinalIgnoreCase));

            if (song is null)
            {
                try
                {
                    // Favorites keep their own Song instances — no library auto-add.
                    song = _library.AddFile(path, SmartTranspose, StrictNoteRange);
                    song.IsFavorite = true;
                }
                catch
                {
                    continue;
                }
            }

            if (ordered.All(s => s.Id != song.Id))
                ordered.Add(song);
        }

        for (var i = FavoriteSongs.Count - 1; i >= 0; i--)
        {
            if (ordered.All(s => s.Id != FavoriteSongs[i].Id))
                FavoriteSongs.RemoveAt(i);
        }

        foreach (var song in ordered)
        {
            if (FavoriteSongs.All(s => s.Id != song.Id))
                FavoriteSongs.Add(song);
        }

        _favoritesViewSource.View.Refresh();
        RefreshFavoritesStats();
        NotifyTrashCommandsCanExecute();
    }

    private bool FilterFavoriteSong(object obj)
    {
        if (obj is not Song song)
            return false;

        if (string.IsNullOrWhiteSpace(FavoritesSearchText))
            return true;

        return song.DisplayTitle.Contains(FavoritesSearchText, StringComparison.OrdinalIgnoreCase)
               || song.Title.Contains(FavoritesSearchText, StringComparison.OrdinalIgnoreCase)
               || song.FilePath.Contains(FavoritesSearchText, StringComparison.OrdinalIgnoreCase);
    }

    private void RefreshFavoritesStats()
    {
        var visible = FavoriteSongs.Where(FilterFavoriteSong).ToList();
        var totalMs = visible.Sum(s => s.DurationMs);
        FavoritesStatsText = visible.Count == 0
            ? L.T(UiText.StatsZeroSongs)
            : $"{visible.Count} {L.T(UiText.StatsSongs)} • {TimeFormat.FromMilliseconds(totalMs)}";
    }

    partial void OnFavoritesSearchTextChanged(string value) => ScheduleRefreshFavoritesView();

    private void RebuildCataloguePathIndex() =>
        _catalogueByCachedPath = CatalogueIndexBuilder.BuildPathIndex(CatalogueTracks);

    private CatalogueTrack? FindCatalogueTrackForSong(Song song) =>
        _catalogueByCachedPath.TryGetValue(song.FilePath, out var track) ? track : null;

    partial void OnPlaylistFavoritesOnlyChanged(bool value)
    {
        _playlistViewSource.View.Refresh();
        RefreshPlaylistStats();
    }

    private bool FilterCatalogueTrack(object obj)
    {
        if (obj is not CatalogueTrack track)
            return false;

        if (!IsAllStylesFilter(CatalogueStyleFilter) &&
            !track.StyleName.Equals(CatalogueStyleFilter, StringComparison.OrdinalIgnoreCase))
            return false;

        if (string.IsNullOrWhiteSpace(CatalogueSearchText))
            return true;

        return track.DisplayTitle.Contains(CatalogueSearchText, StringComparison.OrdinalIgnoreCase)
               || track.Title.Contains(CatalogueSearchText, StringComparison.OrdinalIgnoreCase)
               || track.StyleName.Contains(CatalogueSearchText, StringComparison.OrdinalIgnoreCase);
    }

    private void RefreshCatalogueStats()
    {
        var count = IsAllStylesFilter(CatalogueStyleFilter) && string.IsNullOrWhiteSpace(CatalogueSearchText)
            ? CatalogueTracks.Count
            : _catalogueViewSource.View.Cast<object>().Count();
        UpdateCatalogueStatsText(count);
    }

    private static bool IsAllStylesFilter(string? value) =>
        LocalizationService.Instance.MatchesAnyTranslation(UiText.AllStyles, value);

    private void RebuildCatalogueStyles() =>
        ApplyCatalogueStyleNames(CatalogueIndexBuilder.BuildStyleNames(CatalogueTracks));

    private void ApplyCatalogueStyleNames(IReadOnlyList<string> styleNames)
    {
        var wasAll = IsAllStylesFilter(CatalogueStyleFilter);
        var previous = CatalogueStyleFilter;

        var list = new List<string>(1 + styleNames.Count) { AllStylesLabel };
        list.AddRange(styleNames);
        CatalogueStyles.ReplaceAll(list);

        if (wasAll)
            CatalogueStyleFilter = AllStylesLabel;
        else if (!string.IsNullOrWhiteSpace(previous) && list.Contains(previous))
            CatalogueStyleFilter = previous;
        else
            CatalogueStyleFilter = AllStylesLabel;
    }

    private void ScheduleEnrichCatalogueDurations(IReadOnlyList<CatalogueTrack> tracks)
    {
        if (tracks.Count == 0)
            return;

        Application.Current?.Dispatcher.BeginInvoke(
            DispatcherPriority.ApplicationIdle,
            () => _ = EnrichCatalogueDurationsInBackgroundAsync(tracks));
    }

    public async Task LoadCatalogueOnStartupAsync()
    {
        try
        {
            await LoadDeferredPlaylistAsync().ConfigureAwait(false);

            await UiDispatcher.RunAsync(() =>
            {
                RestorePersistedLibrary();
                return Task.CompletedTask;
            }).ConfigureAwait(false);

            _discordCredentials ??= DiscordCredentialStore.Load();

            var cachedTracks = await Task.Run(() => _discordCatalogue.LoadCatalogueIndex()).ConfigureAwait(false);
            if (cachedTracks.Count > 0)
            {
                await UiDispatcher.RunAsync(async () =>
                {
                    IsCatalogueLoading = true;
                    CatalogueStatusText = $"Loading {cachedTracks.Count} catalogue tracks…";
                    try
                    {
                        await ApplyCatalogueTracksAsync(
                                cachedTracks,
                                $"{cachedTracks.Count} tracks (cached) — syncing Discord…")
                            .ConfigureAwait(true);
                    }
                    finally
                    {
                        IsCatalogueLoading = false;
                    }
                }).ConfigureAwait(false);

                ScheduleEnrichCatalogueDurations(cachedTracks);
            }

            if (_discordCredentials is not null)
                await FetchCatalogueFromDiscordAsync(showErrors: false).ConfigureAwait(false);
            else
                await UiDispatcher.RunAsync(() =>
                    CatalogueStatusText = "Missing discord-catalogue.json — see discord-catalogue.json.example");
        }
        catch (Exception ex)
        {
            CatalogueStatusText = "Catalogue startup failed.";
            AppPaths.WriteDiagnosticLog("catalogue-startup", ex);
        }
    }

    private async Task LoadDeferredPlaylistAsync()
    {
        var path = _deferredPlaylistPath;
        _deferredPlaylistPath = null;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return;

        try
        {
            await LoadPlaylistFromPathAsync(path, refreshSavedList: true).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            AppPaths.WriteDiagnosticLog("playlist-startup", ex);
        }
    }

    [RelayCommand]
    private async Task RefreshDiscordCatalogue() =>
        await FetchCatalogueFromDiscordAsync(showErrors: true);

    private async Task FetchCatalogueFromDiscordAsync(bool showErrors)
    {
        _discordCredentials ??= DiscordCredentialStore.Load();
        if (_discordCredentials is null ||
            string.IsNullOrWhiteSpace(_discordCredentials.BotToken) ||
            string.IsNullOrWhiteSpace(_discordCredentials.GuildId))
        {
            if (showErrors)
            {
                DebraDialogs.Info(
                    "Discord Catalogue",
                    "Discord catalogue is not configured.\n\n" +
                    "Place discord-catalogue.json next to the .exe (see discord-catalogue.json.example).");
            }

            return;
        }

        if (!ulong.TryParse(_discordCredentials.GuildId.Trim(), out var guildId))
        {
            if (showErrors)
                DebraDialogs.Warning("Discord Catalogue", "Guild ID must be numeric.");
            return;
        }

        ulong? categoryId = null;
        if (!string.IsNullOrWhiteSpace(_discordCredentials.CategoryChannelId) &&
            ulong.TryParse(_discordCredentials.CategoryChannelId.Trim(), out var cat))
            categoryId = cat;

        try
        {
            UiDispatcher.Run(() =>
            {
                IsCatalogueLoading = true;
                CatalogueStatusText = "Connecting to Discord…";
            });

            var progress = new Progress<string>(s => UiDispatcher.Run(() => CatalogueStatusText = s));
            var tracks = await _discordCatalogue.FetchCatalogueAsync(
                _discordCredentials.BotToken,
                guildId,
                categoryId,
                null,
                progress).ConfigureAwait(false);

            await UiDispatcher.RunAsync(async () =>
            {
                IsCatalogueLoading = true;
                try
                {
                    await ApplyCatalogueTracksAsync(tracks, $"Loaded {tracks.Count} tracks from Discord.");
                    _discordCatalogue.SaveCatalogueIndex(tracks);
                }
                finally
                {
                    IsCatalogueLoading = false;
                }
            }).ConfigureAwait(false);
            ScheduleEnrichCatalogueDurations(tracks);
        }
        catch (Exception ex)
        {
            UiDispatcher.Run(() => CatalogueStatusText = "Discord sync failed.");
            if (showErrors)
                UiDispatcher.Run(() => DebraDialogs.Error("Discord Catalogue", ex.Message));
        }
        finally
        {
            UiDispatcher.Run(() => IsCatalogueLoading = false);
        }
    }

    private async Task ApplyCatalogueTracksAsync(IReadOnlyList<CatalogueTrack> tracks, string status)
    {
        _suppressCatalogueStats = true;
        try
        {
            var styleNames = await Task.Run(() => CatalogueIndexBuilder.BuildStyleNames(tracks)).ConfigureAwait(true);
            var pathIndex = await Task.Run(() => CatalogueIndexBuilder.BuildPathIndex(tracks)).ConfigureAwait(true);

            CatalogueTracks.ReplaceAll(tracks);
            ApplyCatalogueStyleNames(styleNames);
            _catalogueByCachedPath = pathIndex;
            CatalogueStatusText = status;
        }
        finally
        {
            _suppressCatalogueStats = false;
            ApplyCatalogueSort();
            _catalogueViewSource.View.Refresh();
            RefreshCatalogueStats();
        }
    }

    private async Task EnrichCatalogueDurationsInBackgroundAsync(IReadOnlyList<CatalogueTrack> tracks)
    {
        if (tracks.Count == 0)
            return;

        const int batchSize = 24;
        try
        {
            for (var i = 0; i < tracks.Count; i += batchSize)
            {
                var end = Math.Min(i + batchSize, tracks.Count);
                await Task.Run(() =>
                {
                    for (var j = i; j < end; j++)
                        CatalogueTrackMetadata.EnrichDuration(tracks[j], _midiParser, _songMetadataCache);
                }).ConfigureAwait(false);

                await Task.Delay(32).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            AppPaths.WriteDiagnosticLog("catalogue-enrich-durations", ex);
        }
    }

    [RelayCommand]
    private async Task PlayCatalogueTrack(CatalogueTrack? track)
    {
        track ??= GetNavigationCatalogueTrack();
        if (track is null)
            return;

        try
        {
            CatalogueStatusText = $"Downloading {track.DisplayTitle}…";
            var path = await _discordCatalogue.ResolvePlayablePathAsync(
                track, _discordCredentials?.BotToken).ConfigureAwait(true);
            if (!string.IsNullOrWhiteSpace(path))
                _catalogueByCachedPath[path] = track;

            var song = await Task.Run(() =>
                _library.AddFile(path, SmartTranspose, StrictNoteRange, track.Title)).ConfigureAwait(true);
            track.DurationMs = song.DurationMs;
            _nowPlayingCatalogueTrack = track;
            SetPrimaryListSelection(PrimarySelectionSource.Catalogue, null, track);
            SetActivePlaybackContext(ActivePlaybackList.Catalogue, track);
            await StartSongAsync(song);
            CatalogueStatusText = "Ready.";
        }
        catch (Exception ex)
        {
            AppPaths.WriteDiagnosticLog("catalogue-play", ex);
            DebraDialogs.Error("Catalogue play", ExceptionMessageHelper.FormatUserMessage(ex));
            CatalogueStatusText = "Could not open track.";
        }
    }

    [RelayCommand]
    private async Task AddCatalogueToFavorites(CatalogueTrack? track)
    {
        track ??= GetSelectedCatalogueTrack();
        if (track is null)
            return;

        try
        {
            CatalogueStatusText = $"Loading {track.DisplayTitle}…";
            var song = await ResolveCatalogueTrackAsSongAsync(track);
            if (song is null)
                return;

            SetSongFavorite(song, true);
            SetPrimaryListSelection(PrimarySelectionSource.Favorites, song);
            CatalogueStatusText = "Added to favorites.";
        }
        catch (Exception ex)
        {
            AppPaths.WriteDiagnosticLog("catalogue-favorite", ex);
            DebraDialogs.Error("Catalogue", ExceptionMessageHelper.FormatUserMessage(ex));
            CatalogueStatusText = "Download failed.";
        }
    }

    [RelayCommand]
    private void AddSongToFavorites(Song? song)
    {
        song ??= GetSelectedSongForList(ResolveActivePlaybackList());
        if (song is null)
            return;

        SetSongFavorite(song, true);
        SetPrimaryListSelection(PrimarySelectionSource.Favorites, song);
    }

    public async Task AddCatalogueTrackToPlaylistAtAsync(CatalogueTrack? track, int? insertIndex = null)
    {
        track ??= GetSelectedCatalogueTrack();
        if (track is null)
            return;

        try
        {
            CatalogueStatusText = $"Loading {track.Title}…";
            var song = await ResolveCatalogueTrackAsSongAsync(track);
            if (song is null)
                return;

            AddToPlaylistAt(song, insertIndex);
            CatalogueStatusText = "Added to playlist.";
        }
        catch (Exception ex)
        {
            AppPaths.WriteDiagnosticLog("catalogue-playlist", ex);
            DebraDialogs.Error("Catalogue", ExceptionMessageHelper.FormatUserMessage(ex));
            CatalogueStatusText = "Download failed.";
        }
    }

    public async Task<Song?> ResolveCatalogueTrackAsSongAsync(CatalogueTrack? track)
    {
        track ??= GetSelectedCatalogueTrack();
        if (track is null)
            return null;

        var path = await _discordCatalogue.ResolvePlayablePathAsync(
            track, _discordCredentials?.BotToken);
        return _library.AddFile(path, SmartTranspose, StrictNoteRange, track.Title);
    }

    [RelayCommand]
    private void LoadMidiFile()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "MIDI files (*.mid;*.midi)|*.mid;*.midi"
        };
        if (dialog.ShowDialog() != true)
            return;

        AddSongToLibrary(dialog.FileName);
    }

    private void AddSongToLibrary(string path)
    {
        var song = _library.AddFile(path, SmartTranspose, StrictNoteRange);
        if (!LibrarySongs.Any(s => s.FilePath.Equals(song.FilePath, StringComparison.OrdinalIgnoreCase)))
        {
            SyncSongFavoriteFlag(song);
            LibrarySongs.Add(song);
        }

        RememberImportDirectory(path);
        SelectedLibrarySong = song;
        RefreshLibraryStats();
    }

    private void RestorePersistedLibrary()
    {
        _suppressLibraryPersist = true;
        try
        {
            MigrateImportFolderPaths();
            MigrateDefaultRefreshFolders();
            RemoveCatalogueCachePathsFromLibrary();
            foreach (var path in _settings.Settings.LibrarySongPaths)
            {
                if (!File.Exists(path) || !IsMidiFile(path))
                    continue;

                try
                {
                    if (LibrarySongs.Any(s => s.FilePath.Equals(path, StringComparison.OrdinalIgnoreCase)))
                        continue;

                    var song = _library.AddFile(path, SmartTranspose, StrictNoteRange);
                    SyncSongFavoriteFlag(song);
                    LibrarySongs.Add(song);
                }
                catch
                {
                    // Skip unreadable paths from previous sessions.
                }
            }

            // No automatic folder re-import at startup — the library only reloads its
            // persisted entries; use the manual Refresh button to pick up new files.
            RestorePersistedPracticeLibrary();
            RestoreLastPracticeSongSelection();
            RefreshLibraryStats();
        }
        finally
        {
            _suppressLibraryPersist = false;
            _suppressPracticeLibraryPersist = false;
            SyncPersistedLibraryPaths();
            SyncPersistedPracticeLibraryPaths();
            _settings.Save();
        }
    }

    private void RestorePersistedPracticeLibrary()
    {
        _suppressPracticeLibraryPersist = true;
        try
        {
            if (_settings.Settings.PracticeLibrarySongPaths.Count == 0
                && _settings.Settings.LibrarySongPaths.Count > 0)
            {
                _settings.Settings.PracticeLibrarySongPaths =
                    new List<string>(_settings.Settings.LibrarySongPaths);
            }

            foreach (var path in _settings.Settings.PracticeLibrarySongPaths)
            {
                if (!File.Exists(path) || !IsMidiFile(path))
                    continue;

                try
                {
                    if (PracticeLibrarySongs.Any(s => s.FilePath.Equals(path, StringComparison.OrdinalIgnoreCase)))
                        continue;

                    var song = _library.AddFile(path, SmartTranspose, StrictNoteRange);
                    SyncSongFavoriteFlag(song);
                    PracticeLibrarySongs.Add(song);
                }
                catch
                {
                    // Skip unreadable paths from previous sessions.
                }
            }
        }
        finally
        {
            _suppressPracticeLibraryPersist = false;
        }
    }

    /// <summary>One-time cleanup: catalogue downloads used to be auto-added to the library.</summary>
    private void RemoveCatalogueCachePathsFromLibrary()
    {
        var cacheRoot = AppPaths.CatalogueCacheFolder;
        _settings.Settings.LibrarySongPaths.RemoveAll(p =>
            !string.IsNullOrWhiteSpace(p) &&
            p.StartsWith(cacheRoot, StringComparison.OrdinalIgnoreCase));
    }

    private void RestoreLastPracticeSongSelection()
    {
        var path = _settings.Settings.LastPracticeSongPath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return;

        var song = PracticeLibrarySongs.FirstOrDefault(s => s.FilePath.Equals(path, StringComparison.OrdinalIgnoreCase))
                   ?? LibrarySongs.FirstOrDefault(s => s.FilePath.Equals(path, StringComparison.OrdinalIgnoreCase));
        if (song is null)
        {
            try
            {
                song = _library.AddFile(path, SmartTranspose, StrictNoteRange);
                SyncSongFavoriteFlag(song);
                PracticeLibrarySongs.Add(song);
            }
            catch
            {
                return;
            }
        }
        else if (!PracticeLibrarySongs.Contains(song))
        {
            PracticeLibrarySongs.Add(song);
        }

        _suppressPracticeSongPersist = true;
        _suppressAcademyPracticeSongReload = true;
        try
        {
            SelectedPracticeSong = song;
        }
        finally
        {
            _suppressAcademyPracticeSongReload = false;
            _suppressPracticeSongPersist = false;
        }
    }

    private void SyncPersistedLibraryPaths()
    {
        _settings.Settings.LibrarySongPaths = LibrarySongs
            .Select(s => s.FilePath)
            .Where(static p => !string.IsNullOrWhiteSpace(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void SyncPersistedPracticeLibraryPaths()
    {
        _settings.Settings.PracticeLibrarySongPaths = PracticeLibrarySongs
            .Select(s => s.FilePath)
            .Where(static p => !string.IsNullOrWhiteSpace(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void RememberImportDirectory(string path)
    {
        if (Directory.Exists(path))
            TrackImportFolder(path);
        else if (File.Exists(path))
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
                _settings.Settings.LastImportFolder = directory;
        }
    }

    private void MigrateImportFolderPaths()
    {
        var folder = _settings.Settings.LastImportFolder;
        if (!string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder))
            TrackImportFolder(folder, save: false);
    }

    private void MigrateDefaultRefreshFolders()
    {
        var last = _settings.Settings.LastImportFolder;
        if (string.IsNullOrWhiteSpace(last) || !Directory.Exists(last))
            return;

        if (string.IsNullOrWhiteSpace(_settings.Settings.LibraryRefreshFolder))
            _settings.Settings.LibraryRefreshFolder = last;

        if (string.IsNullOrWhiteSpace(_settings.Settings.PracticeLibraryRefreshFolder))
            _settings.Settings.PracticeLibraryRefreshFolder = last;
    }

    private void TrackImportFolder(string folderPath, bool save = true)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
            return;

        _settings.Settings.LastImportFolder = folderPath;
        var list = _settings.Settings.ImportFolderPaths;
        if (!list.Any(p => string.Equals(p, folderPath, StringComparison.OrdinalIgnoreCase)))
            list.Add(folderPath);

        if (save)
            ScheduleSettingsSave();
    }

    private static string? GetFolderInitialDirectory(string? folderPath) =>
        !string.IsNullOrWhiteSpace(folderPath) && Directory.Exists(folderPath) ? folderPath : null;

    [RelayCommand(CanExecute = nameof(CanRefreshLibraryFolder))]
    private void RefreshLibraryFolders()
    {
        var folder = _settings.Settings.LibraryRefreshFolder;
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            DebraDialogs.Info(L.T(UiText.SectionLibrary), L.T(UiText.LibraryRefreshNone));
            return;
        }

        var before = LibrarySongs.Count;
        ImportDroppedPaths(new[] { folder });

        var added = LibrarySongs.Count - before;
        RefreshLibraryStats();

        if (added > 0)
            DebraDialogs.Info(L.T(UiText.SectionLibrary), L.F(UiText.LibraryRefreshDone, added));
        else
            DebraDialogs.Info(L.T(UiText.SectionLibrary), L.T(UiText.LibraryRefreshUpToDate));
    }

    [RelayCommand(CanExecute = nameof(CanRefreshPracticeLibraryFolder))]
    private void RefreshPracticeLibraryFolders()
    {
        var folder = _settings.Settings.PracticeLibraryRefreshFolder;
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            DebraDialogs.Info(L.T(UiText.SectionPractice), L.T(UiText.LibraryRefreshNone));
            return;
        }

        var before = PracticeLibrarySongs.Count;
        ImportDroppedPathsForPractice(new[] { folder });

        var added = PracticeLibrarySongs.Count - before;
        RefreshLibraryStats();

        if (added > 0)
            DebraDialogs.Info(L.T(UiText.SectionPractice), L.F(UiText.LibraryRefreshDone, added));
        else
            DebraDialogs.Info(L.T(UiText.SectionPractice), L.T(UiText.LibraryRefreshUpToDate));
    }

    private bool CanRefreshLibraryFolder() =>
        !string.IsNullOrWhiteSpace(_settings.Settings.LibraryRefreshFolder) &&
        Directory.Exists(_settings.Settings.LibraryRefreshFolder);

    private bool CanRefreshPracticeLibraryFolder() =>
        !string.IsNullOrWhiteSpace(_settings.Settings.PracticeLibraryRefreshFolder) &&
        Directory.Exists(_settings.Settings.PracticeLibraryRefreshFolder);

    [RelayCommand]
    private void ChooseLibraryRefreshFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = L.T(UiText.SettingsLibraryRefreshFolder),
            InitialDirectory = GetFolderInitialDirectory(LibraryRefreshFolderPath)
        };
        if (dialog.ShowDialog() != true)
            return;

        LibraryRefreshFolderPath = dialog.FolderName;
    }

    [RelayCommand]
    private void ChoosePracticeLibraryRefreshFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = L.T(UiText.SettingsPracticeLibraryRefreshFolder),
            InitialDirectory = GetFolderInitialDirectory(PracticeLibraryRefreshFolderPath)
                ?? GetFolderInitialDirectory(LibraryRefreshFolderPath)
        };
        if (dialog.ShowDialog() != true)
            return;

        PracticeLibraryRefreshFolderPath = dialog.FolderName;
    }

    partial void OnLibraryRefreshFolderPathChanged(string value)
    {
        _settings.Settings.LibraryRefreshFolder = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        ScheduleSettingsSave();
        RefreshLibraryFoldersCommand.NotifyCanExecuteChanged();
    }

    partial void OnLibraryIncludeSubfoldersChanged(bool value)
    {
        _settings.Settings.LibraryIncludeSubfolders = value;
        ScheduleSettingsSave();
    }

    partial void OnPracticeLibraryRefreshFolderPathChanged(string value)
    {
        _settings.Settings.PracticeLibraryRefreshFolder = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        ScheduleSettingsSave();
        RefreshPracticeLibraryFoldersCommand.NotifyCanExecuteChanged();
    }

    partial void OnPracticeLibraryIncludeSubfoldersChanged(bool value)
    {
        _settings.Settings.PracticeLibraryIncludeSubfolders = value;
        ScheduleSettingsSave();
    }

    private string? GetImportInitialDirectory()
    {
        var folder = _settings.Settings.LastImportFolder;
        return !string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder) ? folder : null;
    }

    /// <summary>Import MIDI files or folders dropped onto the library (Explorer drag-and-drop).</summary>
    public int ImportDroppedPaths(IEnumerable<string> paths)
    {
        var added = 0;
        Song? lastAdded = null;

        foreach (var path in paths)
        {
            foreach (var song in EnumerateSongsFromDropPath(path))
            {
                added++;
                lastAdded = song;
            }
        }

        if (added > 0)
        {
            if (lastAdded is not null)
                SelectedLibrarySong = lastAdded;
            RefreshLibraryStats();
        }

        return added;
    }

    public int ImportDroppedPathsForPractice(IEnumerable<string> paths)
    {
        var added = 0;
        Song? lastAdded = null;

        foreach (var path in paths)
        {
            foreach (var song in EnumerateSongsFromPracticePath(path))
            {
                added++;
                lastAdded = song;
            }
        }

        if (added > 0 && lastAdded is not null)
            SelectedPracticeSong = lastAdded;

        return added;
    }

    private IEnumerable<Song> EnumerateSongsFromPracticePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return [];

        try
        {
            if (File.Exists(path))
            {
                if (!IsMidiFile(path))
                    return [];

                RememberImportDirectory(path);
                var song = EnsureSongInLibrary(path);
                AddSongToPracticeLibrary(song);
                return [song];
            }

            if (Directory.Exists(path))
            {
                RememberImportDirectory(path);
                var searchOption = PracticeLibraryIncludeSubfolders
                    ? SearchOption.AllDirectories
                    : SearchOption.TopDirectoryOnly;
                var imported = _library.ImportFolder(path, SmartTranspose, StrictNoteRange, searchOption);
                var songs = new List<Song>(imported.Count);
                foreach (var song in imported)
                {
                    if (!LibrarySongs.Any(s => s.FilePath.Equals(song.FilePath, StringComparison.OrdinalIgnoreCase)))
                        LibrarySongs.Add(song);

                    AddSongToPracticeLibrary(song);
                    songs.Add(song);
                }

                return songs;
            }
        }
        catch
        {
            // Skip unreadable paths.
        }

        return [];
    }

    private void AddSongToPracticeLibrary(Song song)
    {
        if (PracticeLibrarySongs.Any(s => s.FilePath.Equals(song.FilePath, StringComparison.OrdinalIgnoreCase)))
            return;

        SyncSongFavoriteFlag(song);
        PracticeLibrarySongs.Add(song);
    }

    /// <summary>Import MIDI paths into the library and append/insert into the active playlist.</summary>
    public int ImportDroppedPathsToPlaylist(IEnumerable<string> paths, int? insertIndex = null)
    {
        var added = 0;
        var index = insertIndex;

        foreach (var path in paths)
        {
            foreach (var song in EnumerateSongsFromDropPath(path))
            {
                AddToPlaylistAt(song, index);
                added++;
                if (index is int i)
                    index = i + 1;
            }
        }

        if (added > 0)
        {
            _settings.Save();
            RefreshLibraryStats();
            RefreshPlaylistStats();
            ScheduleAutoSave();
        }

        return added;
    }

    private IEnumerable<Song> EnumerateSongsFromDropPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return [];

        try
        {
            if (File.Exists(path))
            {
                if (!IsMidiFile(path))
                    return [];

                RememberImportDirectory(path);
                return [EnsureSongInLibrary(path)];
            }

            if (Directory.Exists(path))
            {
                RememberImportDirectory(path);
                var searchOption = LibraryIncludeSubfolders
                    ? SearchOption.AllDirectories
                    : SearchOption.TopDirectoryOnly;
                var imported = _library.ImportFolder(path, SmartTranspose, StrictNoteRange, searchOption);
                var songs = new List<Song>(imported.Count);
                foreach (var song in imported)
                {
                    if (!LibrarySongs.Any(s => s.FilePath.Equals(song.FilePath, StringComparison.OrdinalIgnoreCase)))
                        LibrarySongs.Add(song);

                    songs.Add(song);
                }

                return songs;
            }
        }
        catch
        {
            // Skip unreadable paths.
        }

        return [];
    }

    private Song EnsureSongInLibrary(string path)
    {
        var existing = LibrarySongs.FirstOrDefault(s =>
            s.FilePath.Equals(path, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
            return existing;

        AddSongToLibrary(path);
        return LibrarySongs.FirstOrDefault(s =>
                   s.FilePath.Equals(path, StringComparison.OrdinalIgnoreCase))
               ?? SelectedLibrarySong
               ?? throw new InvalidOperationException("Failed to import song.");
    }

    private static bool IsMidiFile(string path) =>
        path.EndsWith(".mid", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(".midi", StringComparison.OrdinalIgnoreCase);

    [RelayCommand]
    private void ImportLibrary() => ImportMidiInteractive(navigateToLibrary: true);

    [RelayCommand]
    private void ImportPracticeSong() => ImportMidiInteractive(navigateToLibrary: false);

    private void ImportMidiInteractive(bool navigateToLibrary)
    {
        var choice = DebraDialogs.Choose(
            L.T(UiText.PracticeImport),
            navigateToLibrary
                ? "Import individual MIDI files or all MIDI files in a folder."
                : "Import MIDI files to practice. They are also saved in your library.",
            "MIDI files…",
            "Folder…");
        if (choice is null)
            return;

        if (choice == 0)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Import MIDI files",
                Filter = "MIDI files (*.mid;*.midi)|*.mid;*.midi",
                Multiselect = true,
                InitialDirectory = GetImportInitialDirectory()
            };
            if (dialog.ShowDialog() != true || dialog.FileNames.Length == 0)
                return;

            var added = navigateToLibrary
                ? ImportDroppedPaths(dialog.FileNames)
                : ImportDroppedPathsForPractice(dialog.FileNames);
            if (added > 0 && navigateToLibrary)
                ShowLibrarySection();
            return;
        }

        var folderDialog = new OpenFolderDialog
        {
            Title = "Import MIDI folder",
            InitialDirectory = navigateToLibrary
                ? GetFolderInitialDirectory(LibraryRefreshFolderPath) ?? GetImportInitialDirectory()
                : GetFolderInitialDirectory(PracticeLibraryRefreshFolderPath)
                    ?? GetFolderInitialDirectory(LibraryRefreshFolderPath)
                    ?? GetImportInitialDirectory()
        };
        if (folderDialog.ShowDialog() != true)
            return;

        var folderAdded = navigateToLibrary
            ? ImportDroppedPaths(new[] { folderDialog.FolderName })
            : ImportDroppedPathsForPractice(new[] { folderDialog.FolderName });

        if (navigateToLibrary && string.IsNullOrWhiteSpace(LibraryRefreshFolderPath))
            LibraryRefreshFolderPath = folderDialog.FolderName;
        else if (!navigateToLibrary && string.IsNullOrWhiteSpace(PracticeLibraryRefreshFolderPath))
            PracticeLibraryRefreshFolderPath = folderDialog.FolderName;

        if (folderAdded > 0 && navigateToLibrary)
            ShowLibrarySection();
    }

    public void AddToPlaylistAt(Song song, int? insertIndex = null)
    {
        var existing = -1;
        for (var i = 0; i < PlaylistSongs.Count; i++)
        {
            if (PlaylistSongs[i].FilePath.Equals(song.FilePath, StringComparison.OrdinalIgnoreCase))
            {
                existing = i;
                break;
            }
        }

        if (existing >= 0)
        {
            if (insertIndex is int target)
                MovePlaylistSongToIndex(PlaylistSongs[existing], target);
            return;
        }

        var playlistSong = ResolvePlaylistSong(song) is not null ? song : song.CloneForPlaylist();

        if (insertIndex is int index)
        {
            _playlistService.InsertSong(_currentPlaylist, playlistSong, index);
            SyncSongFavoriteFlag(playlistSong);
            PlaylistSongs.Insert(index, playlistSong);
        }
        else
        {
            _playlistService.AddSong(_currentPlaylist, playlistSong);
            SyncSongFavoriteFlag(playlistSong);
            PlaylistSongs.Add(playlistSong);
        }

        RefreshPlaylistStats();
        ScheduleAutoSave();
    }

    [RelayCommand]
    private void AddToPlaylist(Song? song)
    {
        song ??= GetSelectedSongForList(ResolveActivePlaybackList());
        if (song is null)
            return;

        AddToPlaylistAt(song);
    }

    [RelayCommand(CanExecute = nameof(CanRemoveFromPlaylist))]
    private void RemoveFromPlaylist(Song? song)
    {
        song = ResolvePlaylistSong(song ?? SelectedPlaylistSong ?? _lastSelectedPlaylistSong);
        if (song is null)
            return;

        _playlistService.RemoveSong(_currentPlaylist, song.Id);
        var playlistItem = PlaylistSongs.FirstOrDefault(s => s.Id == song.Id);
        if (playlistItem is not null)
            PlaylistSongs.Remove(playlistItem);
        if (SelectedPlaylistSong?.Id == song.Id)
            SelectedPlaylistSong = null;
        RefreshPlaylistStats();
        ScheduleAutoSave();
    }

    private bool CanRemoveFromPlaylist(Song? song) =>
        ResolvePlaylistSong(song ?? SelectedPlaylistSong ?? _lastSelectedPlaylistSong) is not null;

    [RelayCommand(CanExecute = nameof(CanRemoveFromFavorites))]
    private void RemoveFromFavorites(Song? song)
    {
        song = ResolveFavoriteSong(song ?? SelectedFavoriteSong ?? _lastSelectedFavoriteSong);
        if (song is null)
            return;

        SetSongFavorite(song, false);
        if (SelectedFavoriteSong?.Id == song.Id)
            SelectedFavoriteSong = null;
    }

    private bool CanRemoveFromFavorites(Song? song) =>
        ResolveFavoriteSong(song ?? SelectedFavoriteSong ?? _lastSelectedFavoriteSong) is not null;

    [RelayCommand(CanExecute = nameof(CanRemoveFromLibrary))]
    private void RemoveFromLibrary(Song? song)
    {
        song = ResolveLibrarySong(song ?? SelectedLibrarySong ?? _lastSelectedLibrarySong);
        if (song is null)
            return;

        _library.RemoveSong(song);
        var libraryItem = LibrarySongs.FirstOrDefault(s => s.Id == song.Id);
        if (libraryItem is not null)
            LibrarySongs.Remove(libraryItem);

        var practiceItem = PracticeLibrarySongs.FirstOrDefault(s =>
            s.Id == song.Id
            || s.FilePath.Equals(song.FilePath, StringComparison.OrdinalIgnoreCase));
        if (practiceItem is not null)
            PracticeLibrarySongs.Remove(practiceItem);

        _settings.Settings.FavoritePaths.RemoveAll(
            p => p.Equals(song.FilePath, StringComparison.OrdinalIgnoreCase));
        RebuildFavoritePathSet();
        _settings.Save();

        if (SelectedLibrarySong?.Id == song.Id)
            SelectedLibrarySong = null;

        if (SelectedPracticeSong?.Id == song.Id)
        {
            StopPracticeSession();
            SelectedPracticeSong = null;
        }

        RefreshLibraryStats();
        SyncAllSongFavoriteFlags();
        RefreshFavoriteSongs();
    }

    private bool CanRemoveFromLibrary(Song? song) =>
        ResolveLibrarySong(song ?? SelectedLibrarySong ?? _lastSelectedLibrarySong) is not null;

    [RelayCommand(CanExecute = nameof(CanRemoveFromPracticeLibrary))]
    private void RemoveFromPracticeLibrary(Song? song)
    {
        song = ResolvePracticeLibrarySong(song ?? SelectedPracticeSong ?? _lastSelectedLibrarySong);
        if (song is null)
            return;

        var practiceItem = PracticeLibrarySongs.FirstOrDefault(s => s.Id == song.Id)
                           ?? PracticeLibrarySongs.FirstOrDefault(s =>
                               s.FilePath.Equals(song.FilePath, StringComparison.OrdinalIgnoreCase));
        if (practiceItem is not null)
            PracticeLibrarySongs.Remove(practiceItem);

        if (SelectedPracticeSong?.Id == song.Id)
        {
            StopPracticeSession();
            SelectedPracticeSong = null;
        }
    }

    private bool CanRemoveFromPracticeLibrary(Song? song) =>
        ResolvePracticeLibrarySong(song ?? SelectedPracticeSong ?? _lastSelectedLibrarySong) is not null;

    [RelayCommand(CanExecute = nameof(CanClearLibrary))]
    private void ClearLibrary()
    {
        var count = LibrarySongs.Count;
        if (count == 0)
            return;

        if (!DebraDialogs.Confirm(
                L.T(UiText.LibraryClearTitle),
                L.F(UiText.LibraryClearMessage, count),
                confirmLabel: L.T(UiText.LibraryClear),
                cancelLabel: "Cancel"))
            return;

        if (_nowPlaying is not null && LibrarySongs.Any(s => s.Id == _nowPlaying.Id))
            Stop();

        var paths = LibrarySongs.Select(s => s.FilePath).ToList();

        foreach (var path in paths)
        {
            _settings.Settings.FavoritePaths.RemoveAll(
                p => p.Equals(path, StringComparison.OrdinalIgnoreCase));
        }

        _library.Clear();
        LibrarySongs.Clear();
        SelectedLibrarySong = null;
        RebuildFavoritePathSet();
        _settings.Save();
        RefreshLibraryStats();
        SyncAllSongFavoriteFlags();
        RefreshFavoriteSongs();
    }

    private bool CanClearLibrary() => LibrarySongs.Count > 0;

    [RelayCommand(CanExecute = nameof(CanClearPracticeLibrary))]
    private void ClearPracticeLibrary()
    {
        var count = PracticeLibrarySongs.Count;
        if (count == 0)
            return;

        if (!DebraDialogs.Confirm(
                L.T(UiText.PracticeClearTitle),
                L.F(UiText.PracticeClearMessage, count),
                confirmLabel: L.T(UiText.PracticeClear),
                cancelLabel: "Cancel"))
            return;

        if (SelectedPracticeSong is not null
            && PracticeLibrarySongs.Any(s => s.Id == SelectedPracticeSong.Id))
        {
            StopPracticeSession();
            SelectedPracticeSong = null;
        }

        PracticeLibrarySongs.Clear();
        _settings.Settings.LastPracticeSongPath = null;
        SyncPersistedPracticeLibraryPaths();
        _settings.Save();
    }

    private bool CanClearPracticeLibrary() => PracticeLibrarySongs.Count > 0;

    private Song? ResolvePracticeLibrarySong(Song? song)
    {
        if (song is null)
            return null;

        return PracticeLibrarySongs.FirstOrDefault(s => s.Id == song.Id)
               ?? PracticeLibrarySongs.FirstOrDefault(s =>
                   s.FilePath.Equals(song.FilePath, StringComparison.OrdinalIgnoreCase));
    }

    private Song? ResolveLibrarySong(Song? song)
    {
        if (song is null)
            return null;

        return LibrarySongs.FirstOrDefault(s => s.Id == song.Id)
               ?? LibrarySongs.FirstOrDefault(s =>
                   s.FilePath.Equals(song.FilePath, StringComparison.OrdinalIgnoreCase));
    }

    private Song? ResolvePlaylistSong(Song? song)
    {
        if (song is null)
            return null;

        return PlaylistSongs.FirstOrDefault(s => s.Id == song.Id)
               ?? PlaylistSongs.FirstOrDefault(s =>
                   s.FilePath.Equals(song.FilePath, StringComparison.OrdinalIgnoreCase));
    }

    private Song? ResolveFavoriteSong(Song? song)
    {
        if (song is null)
            return null;

        var resolved = FavoriteSongs.FirstOrDefault(s => s.Id == song.Id)
                       ?? FavoriteSongs.FirstOrDefault(s =>
                           s.FilePath.Equals(song.FilePath, StringComparison.OrdinalIgnoreCase))
                       ?? ResolveLibrarySong(song);

        return resolved is not null && IsSongFavorite(resolved) ? resolved : null;
    }

    public void MovePlaylistSongToIndex(Song song, int targetIndex)
    {
        var fromIndex = PlaylistSongs.IndexOf(song);
        if (fromIndex < 0)
            return;

        targetIndex = Math.Clamp(targetIndex, 0, PlaylistSongs.Count - 1);
        if (fromIndex == targetIndex)
            return;

        _playlistService.MoveSongToIndex(_currentPlaylist, fromIndex, targetIndex);
        PlaylistSongs.Move(fromIndex, targetIndex);
        ScheduleAutoSave();
    }

    [RelayCommand]
    private void MovePlaylistSongUp(Song? song)
    {
        song ??= GetSelectedSongForList(ActivePlaybackList.Playlist);
        if (song is null)
            return;
        var index = PlaylistSongs.IndexOf(song);
        if (index <= 0)
            return;
        _playlistService.MoveSong(_currentPlaylist, index, index - 1);
        PlaylistSongs.Move(index, index - 1);
        ScheduleAutoSave();
    }

    [RelayCommand]
    private void MovePlaylistSongDown(Song? song)
    {
        song ??= GetSelectedSongForList(ActivePlaybackList.Playlist);
        if (song is null)
            return;
        var index = PlaylistSongs.IndexOf(song);
        if (index < 0 || index >= PlaylistSongs.Count - 1)
            return;
        _playlistService.MoveSong(_currentPlaylist, index, index + 1);
        PlaylistSongs.Move(index, index + 1);
        ScheduleAutoSave();
    }

    [RelayCommand]
    private async Task CreatePlaylistAsNamed()
    {
        var name = PlaylistName.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            DebraDialogs.Warning("Playlist", "Enter a playlist name first.");
            return;
        }

        AppPaths.EnsureCreated();
        var path = PlaylistService.DefaultPlaylistPath(name);
        var replacingOther = File.Exists(path)
                             && !string.Equals(path, _currentPlaylistPath, StringComparison.OrdinalIgnoreCase);
        if (replacingOther
            && !DebraDialogs.Confirm(
                "Replace playlist?",
                $"A playlist named \"{name}\" already exists. Replace it with a new empty playlist?",
                confirmLabel: "Replace",
                cancelLabel: "Cancel"))
            return;

        _suppressAutoSave = true;
        try
        {
            var playlist = _playlistService.CreatePlaylist(name);
            await _playlistService.SaveAsync(playlist, path);
            ApplyCurrentPlaylist(playlist, path, playlist.Songs);
            RefreshSavedPlaylists();
        }
        catch (Exception ex)
        {
            DebraDialogs.Error("Create playlist", ex.Message);
        }
        finally
        {
            _suppressAutoSave = false;
        }
    }

    [RelayCommand]
    private async Task SavePlaylistAsNamed()
    {
        var name = PlaylistName.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            DebraDialogs.Warning("Playlist", "Enter a playlist name first.");
            return;
        }

        AppPaths.EnsureCreated();
        var path = PlaylistService.DefaultPlaylistPath(name);
        var replacingOther = File.Exists(path)
                             && !string.Equals(path, _currentPlaylistPath, StringComparison.OrdinalIgnoreCase);
        if (replacingOther
            && !DebraDialogs.Confirm(
                "Overwrite playlist?",
                $"A playlist named \"{name}\" already exists. Replace it with the current song list?",
                confirmLabel: "Replace",
                cancelLabel: "Cancel"))
            return;

        _currentPlaylist.Name = name;
        await PersistCurrentPlaylistAsync(path);
    }

    [RelayCommand]
    private async Task LoadPlaylist()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "JSON playlist (*.json)|*.json",
            InitialDirectory = AppPaths.PlaylistsFolder
        };
        if (dialog.ShowDialog() != true)
            return;

        await LoadPlaylistFromPathAsync(dialog.FileName, refreshSavedList: true).ConfigureAwait(true);
    }

    [RelayCommand]
    private void NewPlaylist() => ResetToBlankPlaylist();

    [RelayCommand(CanExecute = nameof(CanRenameSavedPlaylist))]
    private async Task RenameSavedPlaylist()
    {
        if (string.IsNullOrWhiteSpace(_currentPlaylistPath) || !File.Exists(_currentPlaylistPath))
            return;

        var newName = PlaylistName.Trim();
        if (string.IsNullOrWhiteSpace(newName))
        {
            DebraDialogs.Warning("Playlist", "Enter a playlist name first.");
            return;
        }

        var oldPath = _currentPlaylistPath;
        var newPath = PlaylistService.DefaultPlaylistPath(newName);
        if (string.Equals(oldPath, newPath, StringComparison.OrdinalIgnoreCase))
            return;

        if (File.Exists(newPath)
            && !DebraDialogs.Confirm(
                "Replace playlist?",
                $"A playlist named \"{newName}\" already exists. Replace it with the current playlist?",
                confirmLabel: "Replace",
                cancelLabel: "Cancel"))
            return;

        try
        {
            _currentPlaylist.Name = newName;
            if (File.Exists(newPath))
                File.Delete(newPath);

            File.Move(oldPath!, newPath);
            _currentPlaylistPath = newPath;
            await _playlistService.SaveAsync(_currentPlaylist, newPath);
            _settings.Settings.LastPlaylistPath = newPath;
            _settings.Save();
            RefreshSavedPlaylists();
            SelectSavedPlaylistEntry(newPath);
        }
        catch (Exception ex)
        {
            DebraDialogs.Error("Rename playlist", ex.Message);
        }
    }

    private bool CanRenameSavedPlaylist() =>
        !string.IsNullOrWhiteSpace(_currentPlaylistPath) && File.Exists(_currentPlaylistPath);

    [RelayCommand]
    private void DeleteSavedPlaylist()
    {
        if (!string.IsNullOrWhiteSpace(_currentPlaylistPath) && File.Exists(_currentPlaylistPath))
        {
            if (!DebraDialogs.Confirm(
                    "Delete playlist?",
                    $"Delete \"{PlaylistName}\" from disk? This cannot be undone.",
                    confirmLabel: "Delete",
                    cancelLabel: "Cancel",
                    danger: true))
                return;

            try
            {
                File.Delete(_currentPlaylistPath);
            }
            catch (Exception ex)
            {
                DebraDialogs.Error("Delete playlist", ex.Message);
                return;
            }

            RefreshSavedPlaylists();
            NewPlaylist();
            RefreshPlaylistCommands();
            return;
        }

        if (!DebraDialogs.Confirm(
                "Delete playlist?",
                "Remove all songs from the current unsaved playlist?",
                confirmLabel: "Delete",
                cancelLabel: "Cancel",
                danger: true))
            return;

        PlaylistSongs.Clear();
        _currentPlaylist.Songs.Clear();
        RefreshPlaylistStats();
    }

    private void RefreshPlaylistCommands()
    {
        RenameSavedPlaylistCommand.NotifyCanExecuteChanged();
        DeleteSavedPlaylistCommand.NotifyCanExecuteChanged();
    }

    private Task LoadPlaylistFromPathAsync(string path, bool refreshSavedList) =>
        LoadPlaylistCoreAsync(path, refreshSavedList);

    private async Task LoadPlaylistCoreAsync(string path, bool refreshSavedList)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return;

        var generation = ++_playlistLoadGeneration;
        IsPlaylistLoading = true;
        _suppressAutoSave = true;

        try
        {
            var prepared = await Task.Run(() => PreparePlaylistLoad(path)).ConfigureAwait(true);
            if (generation != _playlistLoadGeneration)
                return;

            ApplyCurrentPlaylist(prepared.Playlist, path, prepared.Songs, syncFavoriteFlags: false);

            if (refreshSavedList)
                await RefreshSavedPlaylistsCoreAsync().ConfigureAwait(true);
            else
                SelectSavedPlaylistEntry(path);

            _settings.Settings.LastPlaylistPath = path;
            _ = Task.Run(() => _settings.Save());
        }
        finally
        {
            if (generation == _playlistLoadGeneration)
            {
                IsPlaylistLoading = false;
                _suppressAutoSave = false;
            }
        }
    }

    private PreparedPlaylistLoad PreparePlaylistLoad(string path)
    {
        var playlist = _playlistService.Load(path);
        var favorites = new HashSet<string>(_settings.Settings.FavoritePaths, StringComparer.OrdinalIgnoreCase);
        foreach (var song in playlist.Songs)
            song.IsFavorite = !string.IsNullOrWhiteSpace(song.FilePath) && favorites.Contains(song.FilePath);

        return new PreparedPlaylistLoad(playlist, playlist.Songs.ToList());
    }

    private readonly record struct PreparedPlaylistLoad(Playlist Playlist, IReadOnlyList<Song> Songs);

    private void ResetToBlankPlaylist()
    {
        _suppressAutoSave = true;
        try
        {
            var playlist = _playlistService.CreatePlaylist(string.Empty);
            ApplyCurrentPlaylist(playlist, path: null, songs: []);
        }
        finally
        {
            _suppressAutoSave = false;
        }
    }

    private void ApplyCurrentPlaylist(
        Playlist playlist,
        string? path,
        IEnumerable<Song> songs,
        bool syncFavoriteFlags = true)
    {
        _currentPlaylist = playlist;
        _currentPlaylistPath = path;

        _suppressSavedPlaylistSelection = true;
        PlaylistName = playlist.Name;

        var list = songs is IReadOnlyList<Song> ro ? ro : songs.ToList();
        if (syncFavoriteFlags)
        {
            foreach (var song in list)
                SyncSongFavoriteFlag(song);
        }

        SelectedPlaylistSong = null;
        _activePlaybackList = ActivePlaybackList.Playlist;
        _activeListIndex = -1;

        var pathForSelect = path;
        UiDispatcher.Post(() =>
        {
            _suppressPlaylistCollectionUpdates = true;
            try
            {
                PlaylistSongs.ReplaceAll(list);
            }
            finally
            {
                _suppressPlaylistCollectionUpdates = false;
            }

            if (pathForSelect is not null)
                SelectSavedPlaylistEntry(pathForSelect);
            else
                SelectedSavedPlaylist = null;

            _suppressSavedPlaylistSelection = false;
            RefreshPlaylistCommands();
            ScheduleRefreshPlaylistStats();
        });
    }

    private void SyncCurrentPlaylistFromUi()
    {
        _currentPlaylist.Name = PlaylistName.Trim();
        if (string.IsNullOrWhiteSpace(_currentPlaylist.Name))
            _currentPlaylist.Name = L.T(UiText.NewPlaylistName);

        _currentPlaylist.Songs.Clear();
        _currentPlaylist.Songs.AddRange(PlaylistSongs);
    }

    private void ScheduleAutoSave()
    {
        if (_suppressAutoSave)
            return;

        _autoSaveDebounce?.Cancel();
        _autoSaveDebounce = new CancellationTokenSource();
        var token = _autoSaveDebounce.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(350, token);
            }
            catch (TaskCanceledException)
            {
                return;
            }

            if (token.IsCancellationRequested)
                return;

            await UiDispatcher.RunAsync(async () =>
            {
                if (_suppressAutoSave || token.IsCancellationRequested)
                    return;

                await PersistCurrentPlaylistAsync();
            });
        }, token);
    }

    private async Task PersistCurrentPlaylistAsync(string? targetPath = null)
    {
        SyncCurrentPlaylistFromUi();
        AppPaths.EnsureCreated();

        var path = targetPath
                   ?? _currentPlaylistPath
                   ?? PlaylistService.DefaultPlaylistPath(_currentPlaylist.Name);

        try
        {
            var pathChanged = !string.Equals(_currentPlaylistPath, path, StringComparison.OrdinalIgnoreCase);
            await _playlistService.SaveAsync(_currentPlaylist, path);
            _currentPlaylistPath = path;
            _settings.Settings.LastPlaylistPath = path;
            _settings.Save();

            if (pathChanged)
                RefreshSavedPlaylists();
            else
                SelectSavedPlaylistEntry(path);

            RefreshPlaylistCommands();
        }
        catch (Exception ex)
        {
            DebraDialogs.Error("Save playlist", ex.Message);
        }
    }

    private void RefreshSavedPlaylists() => _ = RefreshSavedPlaylistsCoreAsync();

    private async Task RefreshSavedPlaylistsCoreAsync()
    {
        var previousPath = SelectedSavedPlaylist?.FilePath ?? _currentPlaylistPath;
        var entries = await Task.Run(EnumerateSavedPlaylistEntries).ConfigureAwait(true);
        SavedPlaylists.ReplaceAll(entries);

        if (!string.IsNullOrWhiteSpace(previousPath))
            SelectSavedPlaylistEntry(previousPath);
    }

    private static List<SavedPlaylistEntry> EnumerateSavedPlaylistEntries()
    {
        var list = new List<SavedPlaylistEntry>();
        if (!Directory.Exists(AppPaths.PlaylistsFolder))
            return list;

        foreach (var file in Directory.EnumerateFiles(AppPaths.PlaylistsFolder, "*.json").OrderBy(Path.GetFileName))
        {
            list.Add(new SavedPlaylistEntry
            {
                Name = Path.GetFileNameWithoutExtension(file),
                FilePath = file
            });
        }

        return list;
    }

    private void SelectSavedPlaylistEntry(string path)
    {
        var match = SavedPlaylists.FirstOrDefault(p =>
            p.FilePath.Equals(path, StringComparison.OrdinalIgnoreCase));
        if (match is null)
            return;

        _suppressSavedPlaylistSelection = true;
        SelectedSavedPlaylist = match;
        _suppressSavedPlaylistSelection = false;
    }

    partial void OnSelectedSavedPlaylistChanged(SavedPlaylistEntry? value)
    {
        NotifyPlaylistSectionActiveName();

        if (_suppressSavedPlaylistSelection || value is null)
            return;

        if (string.Equals(value.FilePath, _currentPlaylistPath, StringComparison.OrdinalIgnoreCase))
            return;

        var path = value.FilePath;
        UiDispatcher.Post(() =>
        {
            if (_suppressSavedPlaylistSelection
                || SelectedSavedPlaylist is null
                || !string.Equals(SelectedSavedPlaylist.FilePath, path, StringComparison.OrdinalIgnoreCase))
                return;

            _ = LoadSelectedPlaylistAsync(path);
        });
    }

    private async Task LoadSelectedPlaylistAsync(string path)
    {
        try
        {
            await LoadPlaylistFromPathAsync(path, refreshSavedList: false).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            DebraDialogs.Warning("Playlist", ex.Message);
        }
    }

    public async Task PlaySongFromListAsync(Song song, ActivePlaybackList list)
    {
        SetActivePlaybackContext(list, song);
        SyncListSelectionForActivePlayback(list, song);
        await StartSongAsync(song);
    }

    [RelayCommand]
    private async Task PlaySelected(Song? song)
    {
        song ??= GetSelectedSongForList(ResolveActivePlaybackList());
        if (song is null)
            return;

        await PlaySongFromListAsync(song, ResolvePlaybackListForSong(song));
    }

    [RelayCommand]
    private async Task PlayPause()
    {
        if (_playback.State == PlaybackState.Playing)
        {
            _playback.Pause();
            return;
        }

        if (_playback.State == PlaybackState.Paused)
        {
            ResumePlayback();
            return;
        }

        await PlayPrimarySelectionAsync();
    }

    private void RefreshPlayPauseUi() => NotifyTransportTooltips();

    private void NotifyTransportTooltips()
    {
        OnPropertyChanged(nameof(PlayPauseToolTip));
        OnPropertyChanged(nameof(StopToolTip));
        OnPropertyChanged(nameof(PreviousToolTip));
        OnPropertyChanged(nameof(NextToolTip));
    }

    private void NotifyPlaybackHotkeyLabels()
    {
        OnPropertyChanged(nameof(PlaybackHotkeyPlayPauseLabel));
        OnPropertyChanged(nameof(PlaybackHotkeyStopLabel));
        OnPropertyChanged(nameof(PlaybackHotkeyPreviousLabel));
        OnPropertyChanged(nameof(PlaybackHotkeyNextLabel));
        NotifyTransportTooltips();
    }

    private string GetPlaybackHotkeyLabel(PlaybackHotkeyRole role) =>
        PlaybackHotkeyCapture == role
            ? "…"
            : VirtualKeyFormatter.Format(PlaybackHotkeySettingsHelper.GetVk(_settings.Settings, role));

    private string FormatTransportTooltip(string actionKey, PlaybackHotkeyRole role) =>
        $"{L.T(actionKey)} ({VirtualKeyFormatter.Format(PlaybackHotkeySettingsHelper.GetVk(_settings.Settings, role))})";

    private void ApplyPlaybackHotkeysFromSettings()
    {
        _globalHotkey.SetVirtualKeys(
            PlaybackHotkeySettingsHelper.GetVk(_settings.Settings, PlaybackHotkeyRole.PlayPause),
            PlaybackHotkeySettingsHelper.GetVk(_settings.Settings, PlaybackHotkeyRole.Stop),
            PlaybackHotkeySettingsHelper.GetVk(_settings.Settings, PlaybackHotkeyRole.Previous),
            PlaybackHotkeySettingsHelper.GetVk(_settings.Settings, PlaybackHotkeyRole.Next));
        NotifyPlaybackHotkeyLabels();
    }

    [RelayCommand]
    private void BeginCapturePlaybackHotkey(PlaybackHotkeyRole role) =>
        PlaybackHotkeyCapture = role;

    [RelayCommand]
    private void ResetPlaybackHotkeys()
    {
        PlaybackHotkeySettingsHelper.ResetToDefaults(_settings.Settings);
        PlaybackHotkeyCapture = null;
        ApplyPlaybackHotkeysFromSettings();
        ScheduleSettingsSave();
    }

    public bool TryHandlePlaybackHotkeyCapture(Key key)
    {
        if (PlaybackHotkeyCapture is not PlaybackHotkeyRole role)
            return false;

        if (key == Key.Escape)
        {
            PlaybackHotkeyCapture = null;
            NotifyPlaybackHotkeyLabels();
            OnPropertyChanged(nameof(PlaybackHotkeyCaptureStatus));
            return true;
        }

        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift
            or Key.LWin or Key.RWin or Key.System)
            return true;

        var vk = KeyInterop.VirtualKeyFromKey(key);
        if (vk <= 0)
            return true;

        if (PlaybackHotkeySettingsHelper.IsDuplicate(_settings.Settings, role, vk))
        {
            PlaybackHotkeyCapture = null;
            NotifyPlaybackHotkeyLabels();
            OnPropertyChanged(nameof(PlaybackHotkeyCaptureStatus));
            return true;
        }

        PlaybackHotkeySettingsHelper.SetVk(_settings.Settings, role, vk);
        PlaybackHotkeyCapture = null;
        ApplyPlaybackHotkeysFromSettings();
        ScheduleSettingsSave();
        OnPropertyChanged(nameof(PlaybackHotkeyCaptureStatus));
        return true;
    }

    partial void OnPlaybackHotkeyCaptureChanged(PlaybackHotkeyRole? value)
    {
        NotifyPlaybackHotkeyLabels();
        OnPropertyChanged(nameof(PlaybackHotkeyCaptureStatus));
    }

    [RelayCommand]
    private void FocusGameNow()
    {
        if (_gameWindow.TryFocusGame(GameWindowTitleContains, out var message))
            GameConnectionStatus = message;
        else
            DebraDialogs.Info("Focus game", message);
    }

    [RelayCommand]
    private void ResetProcessName()
    {
        TargetProcessName = GameProfiles.Current.DefaultProcessName;
        _gameWindow.ClearCache();
    }

    [RelayCommand]
    private void DetectGameWindows()
    {
        _gameWindow.ClearCache();
        var windows = _gameWindow.FindWindows(GameWindowTitleContains);
        if (windows.Count == 0)
        {
            var proc = _gameWindow.IsProcessRunning() ? "running" : "not running";
            DebraDialogs.Info(
                "Game attachment",
                $"Process {TargetProcessName}: {proc}\n\n" +
                "No matching window. Start the game, open the instrument, then try again.");
            return;
        }

        var targets = _gameWindow.GetMessageTargets().Count;
        var list = string.Join("\n", windows.Select(w => "• " + w.Title));
        DebraDialogs.Info(
            "Game attachment",
            $"Process {TargetProcessName}: attached\n" +
            $"Message targets: {targets}\n\n{list}");
    }

    private void ResumePlayback() => StartPlaybackFromCurrentPosition();

    private void StartPlaybackFromCurrentPosition()
    {
        // The engine resets its tempo multiplier on stop/load; the UI keeps the
        // saved per-song percent, so re-sync the engine with what is displayed.
        _playback.SetTempoMultiplier(PlaybackTempoPercent / 100.0);
        _playback.PlayFromCurrentPosition(
            NoteDelayMs,
            ChordRollDelayMs,
            _settings.Settings.MinKeyPressDurationMs,
            _settings.Settings.IdenticalKeyGapMs);
    }

    [RelayCommand]
    private void SeekToPosition(double normalizedPosition)
    {
        if (_nowPlaying is null || _playback.TotalDurationMs <= 0)
            return;

        var targetMs = (long)(Math.Clamp(normalizedPosition, 0, 1) * _playback.TotalDurationMs);
        _playback.SeekToMs(targetMs);
        StartPlaybackFromCurrentPosition();
        IsPlaying = true;
        UpdateProgress();
    }

    /// <summary>Jump backward/forward in the current song; keeps the paused state.</summary>
    private void SeekRelativeSeconds(double deltaSeconds)
    {
        if (_nowPlaying is null || _playback.TotalDurationMs <= 0)
            return;

        var wasPlaying = _playback.State == PlaybackState.Playing;
        var targetMs = Math.Clamp(
            _playback.CurrentPositionMs + (long)(deltaSeconds * 1000),
            0,
            _playback.TotalDurationMs);
        _playback.SeekToMs(targetMs);

        if (wasPlaying)
        {
            StartPlaybackFromCurrentPosition();
            IsPlaying = true;
        }

        UpdateProgress();
    }

    [RelayCommand]
    private void Stop()
    {
        CancelAutoAdvanceTimer();
        _playback.Stop();
        FinalizeHistory(PlaybackStatus.Stopped);
        IsPlaying = false;
        RefreshPlayPauseUi();
    }

    [RelayCommand]
    private async Task Next()
    {
        CancelAutoAdvanceTimer();
        FinalizeHistory(PlaybackStatus.Skipped);
        await AdvanceActiveListAsync(forward: true, autoStart: _playback.State == PlaybackState.Playing);
    }

    [RelayCommand]
    private async Task Previous()
    {
        CancelAutoAdvanceTimer();
        FinalizeHistory(PlaybackStatus.Skipped);
        await AdvanceActiveListAsync(forward: false, autoStart: _playback.State == PlaybackState.Playing);
    }

    [RelayCommand]
    private void ClearHistory()
    {
        if (!DebraDialogs.Confirm(
                "Clear history?",
                "Clear all playback history?",
                confirmLabel: "Clear",
                cancelLabel: "Cancel"))
            return;

        _history.Clear();
        ReloadHistoryItemsFromStore();
        _history.Save();
    }

    [RelayCommand]
    private void OpenKeymapsFolder()
    {
        AppPaths.EnsureCreated();
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = AppPaths.KeyMapsFolder,
            UseShellExecute = true
        });
    }

    [RelayCommand]
    private void OpenKeybindEditor()
    {
        var owner = Application.Current?.MainWindow;
        var editor = new Windows.KeybindEditorWindow(
            _keyMapping,
            _settings.Settings.KeyMappingFile,
            _settings.Settings.KeyboardLayoutPresetId,
            fileName =>
            {
                LoadKeyMapping(fileName);
                RefreshKeyLayouts();
            })
        {
            Owner = owner
        };
        editor.ShowDialog();
    }

    [RelayCommand]
    private void ToggleShuffle() => Shuffle = !Shuffle;

    [RelayCommand]
    private void ToggleRepeat() => Repeat = !Repeat;

    [RelayCommand]
    private async Task PlayHistoryItem(HistoryItem? item)
    {
        if (item is null || !File.Exists(item.FilePath))
            return;

        var song = LibrarySongs.FirstOrDefault(s =>
            s.FilePath.Equals(item.FilePath, StringComparison.OrdinalIgnoreCase));
        if (song is null)
            song = _library.AddFile(item.FilePath, SmartTranspose, StrictNoteRange);

        if (!LibrarySongs.Contains(song))
            LibrarySongs.Add(song);

        await PlaySelected(song);
    }

    [RelayCommand]
    private void ToggleFavorite()
    {
        var song = _nowPlaying ?? GetSelectedSongForList(ResolveActivePlaybackList());
        if (song is null)
            return;

        SetSongFavorite(song, !IsSongFavorite(song));
        if (IsSongFavorite(song) && _primarySelection == PrimarySelectionSource.Favorites)
            SetPrimaryListSelection(PrimarySelectionSource.Favorites, song);
    }

    private async Task StartSongAsync(Song song)
    {
        try
        {
            await UiDispatcher.RunAsync(() =>
            {
                if (IsLiveMidiEnabled)
                    DisableLiveMidiForFilePlayback();

                _suppressPlaybackUi = true;
                _playback.Stop();
                FinalizeHistory(PlaybackStatus.Stopped);

                NowPlayingTitle = CatalogueTitleHelper.GetDisplayTitle(song.Title, song.FilePath);
                ApplyPlaybackCalibrationOnLoad(song.FilePath);
            }).ConfigureAwait(false);

            var prepared = await PrepareSongOnBackgroundAsync(song).ConfigureAwait(false);

            var parsed = prepared.Parsed;
            var ranged = prepared.Ranged;
            var schedule = prepared.Schedule;

            if (schedule.Count == 0)
            {
                await UiDispatcher.RunAsync(() =>
                {
                    _suppressPlaybackUi = false;
                    DebraDialogs.Warning(
                        "Cannot play",
                        $"No playable notes ({_keyMapping.MappedNoteCount} keys in layout).\n\n" +
                        $"• Settings → pick layout \"{GameProfiles.Current.DefaultKeyMapDisplayName}\"\n" +
                        $"• Enable Smart Transpose if the MIDI is outside " +
                        $"{NoteNames.FromMidiNumber(NoteNames.MinGameNote)}–{NoteNames.FromMidiNumber(NoteNames.MaxGameNote)}");
                }).ConfigureAwait(false);
                return;
            }

            if (!await PrepareGameConnectionAsync().ConfigureAwait(false))
            {
                await UiDispatcher.RunAsync(() => _suppressPlaybackUi = false).ConfigureAwait(false);
                return;
            }

            await UiDispatcher.RunAsync(() =>
            {
                try
                {
                    _nowPlaying = song;
                    var playbackList = _activePlaybackList != ActivePlaybackList.None
                        ? _activePlaybackList
                        : ResolvePlaybackListForSong(song);
                    var catalogueTrack = _nowPlayingCatalogueTrack ?? FindCatalogueTrackForSong(song);
                    _nowPlayingCatalogueTrack = catalogueTrack;
                    if (playbackList != ActivePlaybackList.Community)
                        _nowPlayingCommunitySong = null;

                    if (playbackList == ActivePlaybackList.Community && _nowPlayingCommunitySong is not null)
                        SetPrimaryListSelection(PrimarySelectionSource.Community, null, communitySong: _nowPlayingCommunitySong);
                    else if (playbackList == ActivePlaybackList.Catalogue && catalogueTrack is not null)
                        SetPrimaryListSelection(PrimarySelectionSource.Catalogue, null, catalogueTrack);
                    else
                    {
                        var selectionSource = playbackList switch
                        {
                            ActivePlaybackList.Library => PrimarySelectionSource.Library,
                            ActivePlaybackList.Favorites => PrimarySelectionSource.Favorites,
                            ActivePlaybackList.Playlist => PrimarySelectionSource.Playlist,
                            _ => PrimarySelectionSource.Playlist
                        };
                        SetPrimaryListSelection(selectionSource, song);
                    }

                    var favorite = IsSongFavorite(song);

                    NowPlayingPath = song.FilePath;
                    NowPlayingDurationDisplay = TimeFormat.FromMillisecondsLong(song.DurationMs);
                    IsFavorite = favorite;
                    NowPlayingCatalogueTrackId = catalogueTrack?.Id ?? string.Empty;

                    _nowPlayingNoteCount = ranged.Notes.Count;
                    NowPlayingNotesDisplay = _nowPlayingNoteCount.ToString("N0");
                    song.NoteCount = ranged.Notes.Count;
                    song.OutOfRangeNoteCount = ranged.OutOfRangeNoteCount;

                    ResetPlaybackTempoForSong(parsed.BeatsPerMinute);
                    // LoadSchedule stops the engine, which resets its tempo multiplier —
                    // apply the saved per-song tempo only after the schedule is in place.
                    _playback.LoadSchedule(schedule, parsed.DurationMs);
                    ApplySongTempoOnLoad(song.FilePath);
                    TotalTimeText = TimeFormat.FromMilliseconds(parsed.DurationMs);
                    _input.ResetDiagnostics();

                    CancelAutoAdvanceTimer();
                    StartPlaybackFromCurrentPosition();
                    AutoAnnounceNowPlayingIfEnabled();

                    _activeHistoryItem = new HistoryItem
                    {
                        SongTitle = CatalogueTitleHelper.GetDisplayTitle(song.Title, song.FilePath),
                        FilePath = song.FilePath,
                        DurationMs = song.DurationMs,
                        PlayedAt = DateTime.UtcNow,
                        Status = PlaybackStatus.Completed
                    };

                    IsPlaying = true;
                    UpdatePlaybackStatus();
                    UpdateProgress();
                }
                finally
                {
                    _suppressPlaybackUi = false;
                }
            }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _suppressPlaybackUi = false;
            AppPaths.WriteDiagnosticLog("playback-start", ex);
            var failedItem = new HistoryItem
            {
                SongTitle = CatalogueTitleHelper.GetDisplayTitle(song.Title, song.FilePath),
                FilePath = song.FilePath,
                PlayedAt = DateTime.UtcNow,
                Status = PlaybackStatus.Error
            };
            UiDispatcher.Run(() =>
            {
                CommitHistoryEntry(failedItem);
                DebraDialogs.Error("Playback error", $"Failed to play: {ExceptionMessageHelper.FormatUserMessage(ex)}");
            });
        }
    }

    private Task<MidiPrepareResult> PrepareSongOnBackgroundAsync(Song song) =>
        Task.Run(() => _midiPlaybackPreparer.Prepare(
            song.FilePath,
            new MidiPrepareRequest
            {
                SmartTranspose = SmartTranspose,
                StrictNoteRange = StrictNoteRange,
                OctaveShift = PlaybackOctaveShift,
                TrackIndex = -1,
                MutedTracks = GetMutedTrackIndexes(),
                MappingMode = SelectedNoteMappingMode?.Mode ?? NoteMappingMode.Chromatic36,
                PhraseFold = PlaybackPhraseFold,
                ChordRollDelayMs = ChordRollDelayMs,
                NoteDelayMs = NoteDelayMs,
                FfxivChordAlignWindowMs = Math.Clamp(_settings.Settings.FfxivChordAlignWindowMs, 0, 200),
                FfxivChordReduction = _settings.Settings.FfxivChordReduction,
                FfxivTrackOctaveSuffix = _settings.Settings.FfxivTrackOctaveSuffix,
                FfxivMinNoteSpacingMs = Math.Clamp(_settings.Settings.FfxivMinNoteSpacingMs, 0, 200),
                FfxivAdaptiveVoicing = _settings.Settings.FfxivAdaptiveVoicing
            },
            _keyMapping));

    private async Task ReprepareCurrentSongScheduleAsync()
    {
        if (_nowPlaying is null)
            return;

        var song = _nowPlaying;
        var positionMs = _playback.CurrentPositionMs;
        var resumeState = _playback.State;

        try
        {
            var prepared = await PrepareSongOnBackgroundAsync(song).ConfigureAwait(false);
            var schedule = prepared.Schedule;

            if (schedule.Count == 0)
                return;

            await UiDispatcher.RunAsync(() =>
            {
                _suppressPlaybackUi = true;
                try
                {
                    var ranged = prepared.Ranged;
                    var parsed = prepared.Parsed;

                    _nowPlayingNoteCount = ranged.Notes.Count;
                    NowPlayingNotesDisplay = _nowPlayingNoteCount.ToString("N0");
                    song.NoteCount = ranged.Notes.Count;
                    song.OutOfRangeNoteCount = ranged.OutOfRangeNoteCount;
                    TotalTimeText = TimeFormat.FromMilliseconds(parsed.DurationMs);

                    _playback.ReloadSchedule(schedule, parsed.DurationMs, positionMs, resumeState);

                    if (resumeState == PlaybackState.Playing)
                        StartPlaybackFromCurrentPosition();

                    IsPlaying = resumeState == PlaybackState.Playing;
                    UpdateProgress();
                    RefreshPlayPauseUi();
                }
                finally
                {
                    _suppressPlaybackUi = false;
                }
            }).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            AppPaths.WriteDiagnosticLog("reprepare-playback", ex);
        }
    }

    private LiveMidiContext BuildLiveMidiContext() => new()
    {
        SmartTranspose = IsAcademyPracticeMode ? false : SmartTranspose,
        StrictNoteRange = IsAcademyPracticeMode ? false : StrictNoteRange,
        OctaveShift = IsAcademyPracticeMode ? 0 : PlaybackOctaveShift,
        MappingMode = IsAcademyPracticeMode
            ? NoteMappingMode.Chromatic36
            : SelectedNoteMappingMode?.Mode ?? NoteMappingMode.Chromatic36,
        SuppressGameInput = IsAcademyPracticeMode
    };

    [RelayCommand]
    private void RefreshMidiInputDevices()
    {
        var names = _liveMidi.GetDeviceNames();
        var previous = SelectedMidiInputDevice?.Name;
        _suppressMidiInputDeviceChange = true;
        MidiInputDevices.Clear();

        foreach (var name in names)
            MidiInputDevices.Add(new MidiInputDeviceOption { Name = name });

        if (MidiInputDevices.Count == 0)
        {
            SelectedMidiInputDevice = null;
            _suppressMidiInputDeviceChange = false;
            if (IsLiveMidiEnabled)
                StopLiveMidiListening();
            RefreshLiveMidiStatusText();
            return;
        }

        var saved = _settings.Settings.LastMidiInputDeviceName;
        var pick = MidiInputDevices.FirstOrDefault(d =>
                string.Equals(d.Name, previous, StringComparison.OrdinalIgnoreCase))
            ?? MidiInputDevices.FirstOrDefault(d =>
                string.Equals(d.Name, saved, StringComparison.OrdinalIgnoreCase))
            ?? MidiInputDevices[0];

        SelectedMidiInputDevice = pick;
        _suppressMidiInputDeviceChange = false;

        if (IsLiveMidiEnabled && pick is not null)
        {
            try
            {
                _liveMidi.Reconnect(pick.Name);
            }
            catch (Exception ex)
            {
                IsLiveMidiEnabled = false;
                DebraDialogs.Error(
                    L.T(UiText.SettingsLiveMidi),
                    string.Format(L.T(UiText.SettingsLiveMidiError), ex.Message));
            }
        }

        RefreshLiveMidiStatusText();
    }

    private void RestoreSavedMidiInputDevice()
    {
        var saved = _settings.Settings.LastMidiInputDeviceName;
        if (string.IsNullOrWhiteSpace(saved))
            return;

        SelectedMidiInputDevice = MidiInputDevices.FirstOrDefault(d =>
            string.Equals(d.Name, saved, StringComparison.OrdinalIgnoreCase));
    }

    private void RefreshLiveMidiStatusText()
    {
        if (IsLiveMidiEnabled && _liveMidi.ConnectedDeviceName is { } connected)
            LiveMidiStatusText = string.Format(L.T(UiText.SettingsLiveMidiListening), connected);
        else if (MidiInputDevices.Count == 0)
            LiveMidiStatusText = L.T(UiText.SettingsLiveMidiNoDevices);
        else
            LiveMidiStatusText = L.T(UiText.SettingsLiveMidiDisconnected);
    }

    private void SyncPracticeSoundState()
    {
        var inPractice = SelectedSection == NavigationSection.Practice;
        _practiceSound.IsEnabled = inPractice;
        if (!inPractice)
            _practiceSound.StopAllNotes();
    }

    private async Task SyncPracticeLiveInputAsync()
    {
        if (SelectedSection != NavigationSection.Practice)
            return;

        if (SelectedMidiInputDevice is null)
            RefreshMidiInputDevices();

        if (SelectedMidiInputDevice is null)
            return;

        if (!_practiceSessionOwnsLiveMidi)
            await EnableLiveMidiForPracticeAsync().ConfigureAwait(true);
    }

    private void DisableLiveMidiForFilePlayback()
    {
        if (!IsLiveMidiEnabled)
            return;

        _suppressLiveMidiToggle = true;
        IsLiveMidiEnabled = false;
        _suppressLiveMidiToggle = false;
        StopLiveMidiListening();
    }

    private async Task EnableLiveMidiForPracticeAsync()
    {
        RefreshMidiInputDevices();
        if (SelectedMidiInputDevice is null)
            return;

        _practiceSessionOwnsLiveMidi = true;

        try
        {
            _liveMidi.SetEnabled(true, SelectedMidiInputDevice.Name);
            _suppressLiveMidiToggle = true;
            IsLiveMidiEnabled = true;
            _suppressLiveMidiToggle = false;
            RefreshLiveMidiStatusText();
        }
        catch (Exception ex)
        {
            _practiceSessionOwnsLiveMidi = false;
            AppPaths.WriteDiagnosticLog("practice-live-midi", ex);
        }

        await Task.CompletedTask;
    }

    private void DisableLiveMidiAfterPractice()
    {
        if (!_practiceSessionOwnsLiveMidi)
            return;

        _practiceSessionOwnsLiveMidi = false;
        _practiceKeyboardPress.Clear();
        DisableLiveMidiForFilePlayback();
    }

    private void ApplyPracticeLabelModeFromSettings()
    {
        _suppressPracticeLabelSync = true;
        switch (_settings.Settings.PracticeNoteLabelMode)
        {
            case PracticeNoteLabelMode.Solfege:
                IsPracticeSolfegeLabels = true;
                IsPracticeLetterLabels = false;
                IsPracticeKeyboardLabels = false;
                break;
            case PracticeNoteLabelMode.KeyboardKeys:
                IsPracticeKeyboardLabels = true;
                IsPracticeSolfegeLabels = false;
                IsPracticeLetterLabels = false;
                break;
            default:
                IsPracticeLetterLabels = true;
                IsPracticeSolfegeLabels = false;
                IsPracticeKeyboardLabels = false;
                break;
        }
        _suppressPracticeLabelSync = false;
        OnPropertyChanged(nameof(PracticeNoteLabelMode));
        OnPropertyChanged(nameof(PracticeFallingNoteLabelMode));
    }

    private void SavePracticeLabelMode(PracticeNoteLabelMode mode)
    {
        _settings.Settings.PracticeNoteLabelMode = mode;
        ScheduleSettingsSave();
        OnPropertyChanged(nameof(PracticeNoteLabelMode));
        OnPropertyChanged(nameof(PracticeFallingNoteLabelMode));
        RefreshPracticeFallingNoteLayout();
    }

    private void RefreshPracticeFallingNoteLayout() =>
        _practiceSession.NotifyFallingNoteLayoutChanged();

    private void StopLiveMidiListening()
    {
        _liveMidi.SetEnabled(false, null);
        RefreshLiveMidiStatusText();
    }

    private async Task EnableLiveMidiAsync()
    {
        if (SelectedMidiInputDevice is null)
        {
            RefreshMidiInputDevices();
            if (SelectedMidiInputDevice is null)
            {
                IsLiveMidiEnabled = false;
                DebraDialogs.Warning(
                    L.T(UiText.SettingsLiveMidi),
                    L.T(UiText.SettingsLiveMidiNoDevices));
                return;
            }
        }

        _playback.Stop();
        FinalizeHistory(PlaybackStatus.Stopped);
        IsPlaying = false;
        RefreshPlayPauseUi();

        if (!await PrepareGameConnectionAsync().ConfigureAwait(true))
        {
            IsLiveMidiEnabled = false;
            return;
        }

        try
        {
            _liveMidi.SetEnabled(true, SelectedMidiInputDevice.Name);
            _settings.Settings.LastMidiInputDeviceName = SelectedMidiInputDevice.Name;
            ScheduleSettingsSave();
            RefreshLiveMidiStatusText();
        }
        catch (Exception ex)
        {
            IsLiveMidiEnabled = false;
            RefreshLiveMidiStatusText();
            DebraDialogs.Error(
                L.T(UiText.SettingsLiveMidi),
                string.Format(L.T(UiText.SettingsLiveMidiError), ex.Message));
        }
    }

    private bool _suppressLiveMidiToggle;
    private bool _suppressMidiInputDeviceChange;

    partial void OnIsLiveMidiEnabledChanged(bool value)
    {
        if (_suppressLiveMidiToggle)
            return;

        if (value)
            _ = EnableLiveMidiAsync();
        else
            StopLiveMidiListening();
    }

    partial void OnSelectedMidiInputDeviceChanged(MidiInputDeviceOption? value)
    {
        if (_suppressMidiInputDeviceChange)
            return;

        if (value is not null)
        {
            _settings.Settings.LastMidiInputDeviceName = value.Name;
            ScheduleSettingsSave();
        }

        if (IsLiveMidiEnabled)
        {
            try
            {
                _liveMidi.Reconnect(value?.Name);
                RefreshLiveMidiStatusText();
            }
            catch (Exception ex)
            {
                IsLiveMidiEnabled = false;
                RefreshLiveMidiStatusText();
                DebraDialogs.Error(
                    L.T(UiText.SettingsLiveMidi),
                    string.Format(L.T(UiText.SettingsLiveMidiError), ex.Message));
            }
        }
        else
            RefreshLiveMidiStatusText();
    }

    private MidiPrepareRequest BuildPracticePrepareRequest() => new()
    {
        SmartTranspose = IsAcademyPracticeMode ? false : SmartTranspose,
        StrictNoteRange = IsAcademyPracticeMode ? false : StrictNoteRange,
        OctaveShift = IsAcademyPracticeMode ? 0 : PlaybackOctaveShift,
        TrackIndex = -1,
        MappingMode = IsAcademyPracticeMode
            ? NoteMappingMode.TransposeOnly
            : SelectedNoteMappingMode?.Mode ?? NoteMappingMode.Chromatic36,
        PhraseFold = !IsAcademyPracticeMode && PlaybackPhraseFold,
        ChordRollDelayMs = ChordRollDelayMs,
        NoteDelayMs = NoteDelayMs
    };

    private PracticeTrackOption? _practiceRightHandTrack;
    private PracticeTrackOption? _practiceLeftHandTrack;

    public bool ShowPracticeHandTrackPicker => PracticeTrackOptions.Count == 2;

    public PracticeTrackOption? PracticeRightHandTrack => _practiceRightHandTrack;

    public PracticeTrackOption? PracticeLeftHandTrack => _practiceLeftHandTrack;

    private void RebuildPracticeTrackOptions(IReadOnlyList<MidiTrackInfo> tracks)
    {
        ClearPracticeTrackSubscriptions();
        PracticeTrackOptions.Clear();
        var trackList = tracks.Count > 0
            ? tracks
            : [new MidiTrackInfo { Index = 0, Name = "Track 1", NoteCount = 0 }];

        for (var i = 0; i < trackList.Count; i++)
        {
            var track = trackList[i];
            var option = new PracticeTrackOption
            {
                TrackIndex = track.Index,
                DisplayName = string.IsNullOrWhiteSpace(track.Name)
                    ? $"Track {track.Index + 1}"
                    : track.Name,
                IsEnabled = true,
                ColorHex = PracticePrepareService.DefaultTrackColors[i % PracticePrepareService.DefaultTrackColors.Length]
            };
            option.PropertyChanged += OnPracticeTrackOptionChanged;
            PracticeTrackOptions.Add(option);
        }

        UpdatePracticeHandTrackSlots();
    }

    private void UpdatePracticeHandTrackSlots(IReadOnlyList<PracticeVisualNote>? notes = null)
    {
        _practiceRightHandTrack = null;
        _practiceLeftHandTrack = null;

        if (PracticeTrackOptions.Count == 2)
        {
            var (right, left) = PracticeHandTrackLayout.Classify(
                PracticeTrackOptions[0],
                PracticeTrackOptions[1],
                notes);
            PracticeHandTrackLayout.ApplyHandColors(right, left);
            _practiceRightHandTrack = right;
            _practiceLeftHandTrack = left;
        }

        OnPropertyChanged(nameof(ShowPracticeHandTrackPicker));
        OnPropertyChanged(nameof(PracticeRightHandTrack));
        OnPropertyChanged(nameof(PracticeLeftHandTrack));
    }

    private List<PracticeVisualNote> ColorizePracticeVisualNotes(IReadOnlyList<PracticeVisualNote> notes)
    {
        if (IsAcademyPracticeMode)
        {
            var hand = _activeAcademyLessonKind == AcademyLessonKind.Exercise
                ? _activeAcademyHand
                : AcademyHand.Any;
            var assignFingers = true;
            return AcademyFingerMapper.StampAcademyNotes(notes, hand, assignFingers).ToList();
        }

        if (PracticeTrackOptions.Count == 2)
        {
            UpdatePracticeHandTrackSlots(notes);
            return PracticeNoteColorHelper.ApplyTrackColors(notes, PracticeTrackOptions);
        }

        return PracticeNoteColorHelper.ApplyPitchHandColors(
            notes,
            PracticeHandColorResolver.LeftHandHex,
            PracticeHandColorResolver.RightHandHex);
    }

    private void OnPracticeHandColorsChanged()
    {
        if (_practiceSession.Notes.Count == 0)
        {
            if (PracticeTrackOptions.Count == 2)
                UpdatePracticeHandTrackSlots();
            RefreshPracticeHandKeyPreview();
            OnPropertyChanged(nameof(PracticeHandKeyPreview));
            return;
        }

        var colored = ColorizePracticeVisualNotes(_practiceSession.Notes);
        _practiceSession.UpdateNoteColors(colored);
        if (PracticeTrackOptions.Count == 2)
            UpdatePracticeHandTrackSlots(colored);
        RefreshPracticeHandKeyPreview();
        OnPropertyChanged(nameof(PracticeHandKeyPreview));
    }

    partial void OnPracticeRightHandColorHexChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        _settings.Settings.PracticeRightHandColorHex = value.Trim();
        PracticeHandColorResolver.RightHandHex = value.Trim();
        ScheduleSettingsSave();
        OnPracticeHandColorsChanged();
    }

    partial void OnPracticeLeftHandColorHexChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        _settings.Settings.PracticeLeftHandColorHex = value.Trim();
        PracticeHandColorResolver.LeftHandHex = value.Trim();
        ScheduleSettingsSave();
        OnPracticeHandColorsChanged();
    }

    [RelayCommand]
    private void TogglePracticeHandColorPicker() =>
        IsPracticeHandColorPickerOpen = !IsPracticeHandColorPickerOpen;

    [RelayCommand]
    private void SelectPracticeRightHandColor(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
            return;

        PracticeRightHandColorHex = hex.Trim();
    }

    [RelayCommand]
    private void SelectPracticeLeftHandColor(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
            return;

        PracticeLeftHandColorHex = hex.Trim();
    }

    private void SyncPracticeTrackOptions(IReadOnlyList<MidiTrackInfo> tracks)
    {
        var trackList = tracks.Count > 0
            ? tracks
            : [new MidiTrackInfo { Index = 0, Name = "Track 1", NoteCount = 0 }];

        if (PracticeTrackOptions.Count == trackList.Count && PracticeTrackOptions.Count > 0)
        {
            if (PracticeTrackOptions.Count == 2 && !IsAcademyPracticeMode)
                UpdatePracticeHandTrackSlots();
            return;
        }

        ClearPracticeTrackSubscriptions();
        PracticeTrackOptions.Clear();
        RebuildPracticeTrackOptions(trackList);
    }

    private void ClearPracticeTrackSubscriptions()
    {
        foreach (var option in PracticeTrackOptions)
            option.PropertyChanged -= OnPracticeTrackOptionChanged;
    }

    private void OnPracticeTrackOptionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(PracticeTrackOption.IsEnabled))
            return;

        if (ShowPracticeHandTrackPicker &&
            PracticeTrackOptions.All(t => !t.IsEnabled) &&
            sender is PracticeTrackOption option)
        {
            option.IsEnabled = true;
            return;
        }

        ApplyPracticeEnabledTracks();
    }

    private HashSet<int> GetEnabledPracticeTrackIndices()
    {
        var enabled = PracticeTrackOptions
            .Where(t => t.IsEnabled)
            .Select(t => t.TrackIndex)
            .ToHashSet();

        if (enabled.Count > 0)
            return enabled;

        return PracticeTrackOptions.Select(t => t.TrackIndex).ToHashSet();
    }

    private void ApplyPracticeEnabledTracks()
    {
        _practiceSession.SetEnabledTrackIndices(GetEnabledPracticeTrackIndices());
        _practiceSound.ResetSession();
        RefreshPracticeHandKeyPreview();

        if (IsPracticeSoundEnabled && _practiceSession.State == PlaybackState.Playing)
            _practiceSound.ProcessChartPosition(
                _practiceSession.VisibleNotes,
                _practiceSession.CurrentPositionMs);
    }

    private static List<PracticeTrackOption> SnapshotAllPracticeTracksEnabled(
        IReadOnlyList<PracticeTrackOption> options)
    {
        return options.Select(o => new PracticeTrackOption
        {
            TrackIndex = o.TrackIndex,
            DisplayName = o.DisplayName,
            ColorHex = o.ColorHex,
            IsEnabled = true
        }).ToList();
    }

    private async Task ReloadPracticeChartAsync(Song song)
    {
        try
        {
            var tracks = await Task.Run(() => _midiParser.GetTracks(song.FilePath)).ConfigureAwait(false);
            var viewMode = PracticeKeyboardViewMode;

            await UiDispatcher.RunAsync(() =>
            {
                SyncPracticeTrackOptions(tracks);
            }).ConfigureAwait(true);

            var trackOptionsSnapshot = SnapshotAllPracticeTracksEnabled(PracticeTrackOptions);

            var result = await Task.Run(() => _practicePrepare.Prepare(
                song.FilePath,
                BuildPracticePrepareRequest(),
                _keyMapping,
                trackOptionsSnapshot,
                viewMode)).ConfigureAwait(false);

            await UiDispatcher.RunAsync(() =>
            {
                if (PracticeTrackOptions.Count == 0 && result.Tracks.Count > 0)
                    SyncPracticeTrackOptions(result.Tracks);

                if (!IsAcademyPracticeMode && PracticeTrackOptions.Count == 2)
                    UpdatePracticeHandTrackSlots();

                var visualNotes = ColorizePracticeVisualNotes(result.VisualNotes);

                _practiceSession.SetTempoPercent(PracticeTempoPercent);
                _practiceSession.Load(visualNotes, result.DurationMs);
                _practiceSession.SetEnabledTrackIndices(GetEnabledPracticeTrackIndices());
                PracticeDurationText = TimeFormat.FromMillisecondsLong(result.DurationMs);
                PracticeTimeText = "0:00";
                PracticeProgress = 0;
                IsPracticePlaying = false;
                OnPropertyChanged(nameof(PracticeKeyboardViewMode));

                if (ShowPracticeHandTrackPicker || IsAcademyPracticeMode || _practiceSession.Notes.Count > 0)
                    RefreshPracticeHandKeyPreview();

                SyncPracticeSoundState();
                _ = SyncPracticeLiveInputAsync();
            }).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            AppPaths.WriteDiagnosticLog("practice-load", ex);
            await UiDispatcher.RunAsync(() =>
                DebraDialogs.Error(L.T(UiText.SectionPractice), $"Failed to load practice chart: {ex.Message}"))
                .ConfigureAwait(true);
        }
    }

    private void StopPracticeSession()
    {
        _practiceSession.Stop();
        _practiceKeyboardPress.Clear();
        _practicePcKeysHeld.Clear();
        _practiceSound.ResetSession();
        SyncPracticeSoundState();
        IsPracticePlaying = false;
        PracticeProgress = 0;
        PracticeTimeText = "0:00";
        EndAcademyPracticeMode();
    }

    [RelayCommand]
    private async Task StartPracticeAsync()
    {
        if (SelectedPracticeSong is null)
        {
            DebraDialogs.Warning(L.T(UiText.SectionPractice), L.T(UiText.PracticeSelectSong));
            return;
        }

        _playback.Stop();
        await ReloadPracticeChartAsync(SelectedPracticeSong).ConfigureAwait(true);

        if (_practiceSession.Notes.Count == 0)
        {
            DebraDialogs.Warning(L.T(UiText.SectionPractice), L.T(UiText.PracticeNoNotes));
            return;
        }

        await RunPracticeCountdownAsync().ConfigureAwait(true);
        await RunPracticeStartCoreAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    private void PausePractice()
    {
        if (_practiceSession.State == PlaybackState.Playing)
            _practiceSession.Pause();
        else if (_practiceSession.State == PlaybackState.Paused)
            _practiceSession.Start();
    }

    [RelayCommand]
    private void StopPractice()
    {
        StopPracticeSession();
    }

    [RelayCommand]
    private void ClosePracticeLibraryPanel() => IsPracticeLibraryPanelOpen = false;

    [RelayCommand]
    private async Task LoadPracticeLibrarySongAsync(Song? song)
    {
        if (song is null)
            return;

        EndAcademyPracticeMode();

        if (SelectedPracticeSong?.Id == song.Id)
            await ReloadPracticeChartAsync(song).ConfigureAwait(true);
        else
            SelectedPracticeSong = song;

        IsPracticeLibraryPanelOpen = false;
    }

    [RelayCommand]
    private void ReloadPracticeChart()
    {
        if (SelectedPracticeSong is not null)
            _ = ReloadPracticeChartAsync(SelectedPracticeSong);
    }

    [RelayCommand]
    private void SeekPracticeToPosition(double normalizedPosition)
    {
        if (_practiceSession.DurationMs <= 0)
            return;

        var targetMs = (long)(Math.Clamp(normalizedPosition, 0, 1) * _practiceSession.DurationMs);
        _practiceSession.SeekToMs(targetMs);
    }

    private void OnPracticePositionChanged(long positionMs) =>
        UiDispatcher.Post(() =>
        {
            var displayMs = Math.Max(0, positionMs);
            PracticeTimeText = TimeFormat.FromMilliseconds(displayMs);
            var duration = _practiceSession.DurationMs;
            PracticeProgress = duration > 0 ? Math.Clamp(displayMs * 100.0 / duration, 0, 100) : 0;

            if (IsPracticeSoundEnabled && _practiceSession.State == PlaybackState.Playing && positionMs >= 0)
                _practiceSound.ProcessChartPosition(_practiceSession.VisibleNotes, positionMs);
        });

    private void OnPracticeWaitingNotesChanged() =>
        UiDispatcher.Post(() =>
        {
            ReconcilePracticeInput();

            if (!IsPracticeSoundEnabled)
                return;

            foreach (var note in _practiceSession.WaitingNotes)
                _practiceSound.PlayChartNoteOnce(note);
        });

    private void OnPracticeStateChanged(PlaybackState state) =>
        UiDispatcher.Post(() =>
        {
            IsPracticePlaying = state == PlaybackState.Playing;
            OnPropertyChanged(nameof(ShowPracticeCenterPlay));
            OnPropertyChanged(nameof(ShowPracticeHandPreview));

            if (state == PlaybackState.Playing)
            {
                SyncPracticeSoundState();
                _ = SyncPracticeLiveInputAsync();
            }
        });

    private void OnPracticeCompleted() =>
        UiDispatcher.Post(() =>
        {
            IsPracticePlaying = false;
            PracticeProgress = 100;
            _practiceSound.ResetSession();
            SyncPracticeSoundState();
            _ = SyncPracticeLiveInputAsync();
        });

    partial void OnIsPracticeSoundEnabledChanged(bool value)
    {
        OnPropertyChanged(nameof(PracticeSoundToggleLabel));

        if (_suppressPracticeSoundSave)
            return;

        _settings.Settings.PracticeSoundEnabled = value;
        _settings.Save();
        SyncPracticeSoundState();
    }

    partial void OnIsPracticeGameSoundOnlyChanged(bool value)
    {
        OnPropertyChanged(nameof(PracticeGameSoundOnlyToggleLabel));

        if (_suppressPracticeGameSoundSave)
            return;

        _settings.Settings.PracticeGameSoundOnly = value;
        _settings.Save();
    }

    private void ApplyPracticeCountdownFromSettings()
    {
        // Legacy builds saved 30 when the text box could not be edited.
        if (_settings.Settings.AcademyPracticeCountdownSeconds == 30)
        {
            _settings.Settings.AcademyPracticeCountdownSeconds = 5;
            ScheduleSettingsSave();
        }

        PracticeAcademyCountdownSeconds = Math.Clamp(
            _settings.Settings.AcademyPracticeCountdownSeconds > 0
                ? _settings.Settings.AcademyPracticeCountdownSeconds
                : 5,
            0,
            30);
    }

    private void ApplyPracticeSoundFromSettings()
    {
        _suppressPracticeSoundSave = true;
        IsPracticeSoundEnabled = _settings.Settings.PracticeSoundEnabled;
        _suppressPracticeSoundSave = false;
        OnPropertyChanged(nameof(PracticeSoundToggleLabel));
        SyncPracticeSoundState();
    }

    private void ApplyPracticeGameSoundFromSettings()
    {
        _suppressPracticeGameSoundSave = true;
        IsPracticeGameSoundOnly = _settings.Settings.PracticeGameSoundOnly;
        _suppressPracticeGameSoundSave = false;
        OnPropertyChanged(nameof(PracticeGameSoundOnlyToggleLabel));
    }

    partial void OnIsPracticeLearnModeChanged(bool value)
    {
        if (_suppressPracticeModeSync)
            return;

        _practiceSession.Mode = value ? PracticeMode.Learn : PracticeMode.Follow;

        if (value && IsPracticeFollowMode)
        {
            _suppressPracticeModeSync = true;
            IsPracticeFollowMode = false;
            _suppressPracticeModeSync = false;
        }
        else if (!value && !IsPracticeFollowMode)
        {
            _suppressPracticeModeSync = true;
            IsPracticeFollowMode = true;
            _suppressPracticeModeSync = false;
        }
    }

    partial void OnIsPracticeFollowModeChanged(bool value)
    {
        if (_suppressPracticeModeSync)
            return;

        if (value)
        {
            _practiceSession.Mode = PracticeMode.Follow;
            if (IsPracticeLearnMode)
            {
                _suppressPracticeModeSync = true;
                IsPracticeLearnMode = false;
                _suppressPracticeModeSync = false;
            }
        }
        else if (!IsPracticeLearnMode)
        {
            _suppressPracticeModeSync = true;
            IsPracticeLearnMode = true;
            _suppressPracticeModeSync = false;
            _practiceSession.Mode = PracticeMode.Learn;
        }
    }

    partial void OnIsPracticeGameKeysViewChanged(bool value)
    {
        if (_suppressPracticeViewSync)
            return;

        if (value && IsPracticeFullPianoView)
        {
            _suppressPracticeViewSync = true;
            IsPracticeFullPianoView = false;
            _suppressPracticeViewSync = false;
        }
        else if (!value && !IsPracticeFullPianoView)
        {
            _suppressPracticeViewSync = true;
            IsPracticeFullPianoView = true;
            _suppressPracticeViewSync = false;
        }

        OnPropertyChanged(nameof(PracticeKeyboardViewMode));
        if (SelectedPracticeSong is not null)
            _ = ReloadPracticeChartAsync(SelectedPracticeSong);
    }

    partial void OnIsPracticeFullPianoViewChanged(bool value)
    {
        if (_suppressPracticeViewSync)
            return;

        if (value && IsPracticeGameKeysView)
        {
            _suppressPracticeViewSync = true;
            IsPracticeGameKeysView = false;
            _suppressPracticeViewSync = false;
        }
        else if (!value && !IsPracticeGameKeysView)
        {
            _suppressPracticeViewSync = true;
            IsPracticeGameKeysView = true;
            _suppressPracticeViewSync = false;
        }

        OnPropertyChanged(nameof(PracticeKeyboardViewMode));
        if (SelectedPracticeSong is not null)
            _ = ReloadPracticeChartAsync(SelectedPracticeSong);
    }

    partial void OnIsAcademyPracticeModeChanged(bool value)
    {
        OnPropertyChanged(nameof(PracticeFallingNoteLabelMode));
        OnPropertyChanged(nameof(PracticeNoteLabelMode));
        OnPropertyChanged(nameof(ShowAcademyFingerLabelsOnNotes));
        OnPropertyChanged(nameof(ShowPracticeHandPreview));
        RefreshPracticeFallingNoteLayout();
    }

    partial void OnIsPracticeSolfegeLabelsChanged(bool value)
    {
        if (_suppressPracticeLabelSync)
            return;

        if (value)
        {
            if (IsPracticeLetterLabels || IsPracticeKeyboardLabels)
            {
                _suppressPracticeLabelSync = true;
                IsPracticeLetterLabels = false;
                IsPracticeKeyboardLabels = false;
                _suppressPracticeLabelSync = false;
            }
            SavePracticeLabelMode(PracticeNoteLabelMode.Solfege);
        }
        else if (!IsPracticeLetterLabels && !IsPracticeKeyboardLabels)
        {
            _suppressPracticeLabelSync = true;
            IsPracticeLetterLabels = true;
            _suppressPracticeLabelSync = false;
            SavePracticeLabelMode(PracticeNoteLabelMode.LetterNames);
        }
    }

    partial void OnIsPracticeLetterLabelsChanged(bool value)
    {
        if (_suppressPracticeLabelSync)
            return;

        if (value)
        {
            if (IsPracticeSolfegeLabels || IsPracticeKeyboardLabels)
            {
                _suppressPracticeLabelSync = true;
                IsPracticeSolfegeLabels = false;
                IsPracticeKeyboardLabels = false;
                _suppressPracticeLabelSync = false;
            }
            SavePracticeLabelMode(PracticeNoteLabelMode.LetterNames);
        }
        else if (!IsPracticeSolfegeLabels && !IsPracticeKeyboardLabels)
        {
            _suppressPracticeLabelSync = true;
            IsPracticeLetterLabels = true;
            _suppressPracticeLabelSync = false;
            SavePracticeLabelMode(PracticeNoteLabelMode.LetterNames);
        }
    }

    partial void OnIsPracticeKeyboardLabelsChanged(bool value)
    {
        if (_suppressPracticeLabelSync)
            return;

        if (value)
        {
            if (IsPracticeSolfegeLabels || IsPracticeLetterLabels)
            {
                _suppressPracticeLabelSync = true;
                IsPracticeSolfegeLabels = false;
                IsPracticeLetterLabels = false;
                _suppressPracticeLabelSync = false;
            }
            SavePracticeLabelMode(PracticeNoteLabelMode.KeyboardKeys);
        }
        else if (!IsPracticeSolfegeLabels && !IsPracticeLetterLabels)
        {
            _suppressPracticeLabelSync = true;
            IsPracticeLetterLabels = true;
            _suppressPracticeLabelSync = false;
            SavePracticeLabelMode(PracticeNoteLabelMode.LetterNames);
        }
    }

    partial void OnSelectedPracticeSongChanged(Song? value)
    {
        if (_suppressAcademyPracticeSongReload)
            return;

        if (value is not null && IsAcademyPracticeMode)
            EndAcademyPracticeMode();

        if (!_suppressPracticeSongPersist)
        {
            _settings.Settings.LastPracticeSongPath = value?.FilePath;
            ScheduleSettingsSave();
        }

        if (value is null)
        {
            PracticeTitle = string.Empty;
            _practiceSession.Stop();
            ClearPracticeTrackSubscriptions();
            PracticeTrackOptions.Clear();
            UpdatePracticeHandTrackSlots();
            SyncPracticeSoundState();
            _ = SyncPracticeLiveInputAsync();
            return;
        }

        PracticeTitle = CatalogueTitleHelper.GetDisplayTitle(value.Title, value.FilePath);
        ClearPracticeTrackSubscriptions();
        PracticeTrackOptions.Clear();
        UpdatePracticeHandTrackSlots();
        IsPracticeLibraryPanelOpen = false;
        _ = ReloadPracticeChartAsync(value);
    }

    private void ApplyInputAndWindowSettings()
    {
        _gameWindow.SetTargetProcessName(TargetProcessName);
        _gameWindow.SetCustomKeywords(_settings.Settings.CustomWindowKeywords);
        _input.ConfigureMode(() => InputDeliveryMode.LocalPostMessage);
        _input.ConfigureModifierDelay(() => ModifierDelayMs);
        _input.ConfigureLiveInputTiming(
            () => NoteDelayMs,
            () => _settings.Settings.IdenticalKeyGapMs);
    }

    partial void OnTargetProcessNameChanged(string value)
    {
        _settings.Settings.TargetProcessName = value;
        _settings.Save();
        _gameWindow.SetTargetProcessName(value);
    }

    partial void OnReleaseManifestUrlChanged(string value)
    {
        _settings.Settings.ReleaseManifestUrl = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        ScheduleSettingsSave();
        _ = RefreshUpdateAvailabilityAsync();
    }

    [RelayCommand]
    private async Task TestKeyInGame()
    {
        _gameWindow.ClearCache();
        var focusMsg = "Keys sent in background — Debra stays on top.";
        await Task.Delay(200);
        _input.ResetDiagnostics();
        _input.PressKeyCombo("Z");
        await Task.Delay(120);
        _input.PressKeyCombo("Z");
        await Task.Delay(120);
        _input.PressKeyCombo("Shift+Z");

        var proc = _gameWindow.IsProcessRunning() ? $"running ({TargetProcessName})" : $"NOT running ({TargetProcessName})";
        var hwnd = _gameWindow.IsGameWindowFound() ? "found" : "NOT found";
        var targets = _gameWindow.GetMessageTargets().Count;
        GameConnectionStatus = $"Test | {proc} | hwnd {hwnd} ({targets} targets) | {_input.LastDeliveryMethod}";

        DebraDialogs.Info(
            "Key test",
            $"{focusMsg}\n\n" +
            $"Process {TargetProcessName}: {proc}\n" +
            $"Window: {hwnd} ({targets} message target(s))\n" +
            $"Keys sent: {_input.KeysSentCount}\n" +
            $"Method: {_input.LastDeliveryMethod}\n\n" +
            "With instrument open, you should hear C3 (Z) even if this app stays on top.\n" +
            "If keys sent > 0 but no sound: open instrument mode in-game, or run as Administrator.");
    }

    /// <summary>PostMessage to game HWND — Debra can stay on top.</summary>
    private async Task<bool> PrepareGameConnectionAsync()
    {
        var probe = await Task.Run(() =>
        {
            var running = _gameWindow.IsProcessRunning();
            var found = running && _gameWindow.IsGameWindowFound();
            var targets = found ? _gameWindow.GetMessageTargets().Count : 0;
            return (running, found, targets);
        }).ConfigureAwait(false);

        if (!probe.running)
        {
            var continueAnyway = false;
            await UiDispatcher.RunAsync(() => continueAnyway = DebraDialogs.Confirm(
                "Game not found",
                $"Process \"{TargetProcessName}\" is not running.\n\n" +
                $"Start {GameProfiles.Current.DisplayName}, open the instrument, then play.\n\nContinue anyway?",
                confirmLabel: "Continue",
                cancelLabel: "Cancel")).ConfigureAwait(true);
            if (!continueAnyway)
                return false;
        }

        if (!probe.found)
        {
            var continueAnyway = false;
            await UiDispatcher.RunAsync(() => continueAnyway = DebraDialogs.Confirm(
                "Window not found",
                $"No window found for {TargetProcessName}.\n\n" +
                "Open the instrument UI in-game.\n\nContinue anyway?",
                confirmLabel: "Continue",
                cancelLabel: "Cancel")).ConfigureAwait(true);
            if (!continueAnyway)
                return false;

            probe.targets = await Task.Run(() => _gameWindow.GetMessageTargets().Count).ConfigureAwait(false);
        }

        var seconds = Math.Clamp(PrePlayCountdownSeconds, 0, 30);
        for (var i = seconds; i > 0; i--)
        {
            var tick = i;
            await UiDispatcher.RunAsync(() => GameConnectionStatus = $"Starting in {tick}s…")
                .ConfigureAwait(true);
            await Task.Delay(1000).ConfigureAwait(false);
        }

        await UiDispatcher.RunAsync(() =>
                GameConnectionStatus =
                    $"Background play to {TargetProcessName} ({probe.targets} hwnd).")
            .ConfigureAwait(true);

        return true;
    }

    private void ScheduleHistoryPersist()
    {
        if (_historyPersistScheduled)
            return;

        _historyPersistScheduled = true;
        _ = Task.Run(async () =>
        {
            try
            {
                await _history.SaveAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                AppPaths.WriteDiagnosticLog("history-save", ex);
            }
            finally
            {
                UiDispatcher.Post(() => _historyPersistScheduled = false);
            }
        });
    }

    private List<Song> GetFilteredLibrarySongs() => _libraryViewSource.View.Cast<Song>().ToList();

    private List<Song> GetFilteredFavoriteSongs() => _favoritesViewSource.View.Cast<Song>().ToList();

    private List<Song> GetFilteredPlaylistSongs() => _playlistViewSource.View.Cast<Song>().ToList();

    private List<CatalogueTrack> GetFilteredCatalogueTracks() => _catalogueViewSource.View.Cast<CatalogueTrack>().ToList();

    private ActivePlaybackList GetPlaybackListForCurrentSection() =>
        SelectedSection switch
        {
            NavigationSection.Library => ActivePlaybackList.Library,
            NavigationSection.Catalogue => ActivePlaybackList.Catalogue,
            NavigationSection.Community => ActivePlaybackList.Community,
            NavigationSection.Favorites => ActivePlaybackList.Favorites,
            NavigationSection.History => ActivePlaybackList.Playlist,
            NavigationSection.Settings when _activePlaybackList != ActivePlaybackList.None
                => _activePlaybackList,
            NavigationSection.Settings => ActivePlaybackList.Playlist,
            _ => ActivePlaybackList.Playlist
        };

    private static ActivePlaybackList MapPrimarySelection(PrimarySelectionSource source) =>
        source switch
        {
            PrimarySelectionSource.Playlist => ActivePlaybackList.Playlist,
            PrimarySelectionSource.Catalogue => ActivePlaybackList.Catalogue,
            PrimarySelectionSource.Community => ActivePlaybackList.Community,
            PrimarySelectionSource.Favorites => ActivePlaybackList.Favorites,
            PrimarySelectionSource.Library => ActivePlaybackList.Library,
            _ => ActivePlaybackList.None
        };

    private void ApplyExclusiveSelection(PrimarySelectionSource source)
    {
        _primarySelection = source;
        if (source != PrimarySelectionSource.None)
            _activePlaybackList = MapPrimarySelection(source);
        if (source != PrimarySelectionSource.Library)
            SelectedLibrarySong = null;
        if (source != PrimarySelectionSource.Playlist)
            SelectedPlaylistSong = null;
        if (source != PrimarySelectionSource.Favorites)
            SelectedFavoriteSong = null;
        if (source != PrimarySelectionSource.Catalogue)
            SelectedCatalogueTrack = null;
        if (source != PrimarySelectionSource.Community)
            SelectedCommunitySong = null;
    }

    private void OnPrimaryListItemSelected(PrimarySelectionSource source)
    {
        if (_suppressExclusiveSelection)
            return;

        _suppressExclusiveSelection = true;
        try
        {
            ApplyExclusiveSelection(source);
        }
        finally
        {
            _suppressExclusiveSelection = false;
        }
    }

    private void SetPrimaryListSelection(PrimarySelectionSource source, Song? song, CatalogueTrack? catalogueTrack = null, CommunitySong? communitySong = null)
    {
        _suppressExclusiveSelection = true;
        try
        {
            ApplyExclusiveSelection(source);
            switch (source)
            {
                case PrimarySelectionSource.Library:
                    SelectedLibrarySong = song;
                    break;
                case PrimarySelectionSource.Playlist:
                    SelectedPlaylistSong = song;
                    break;
                case PrimarySelectionSource.Favorites:
                    SelectedFavoriteSong = song;
                    break;
                case PrimarySelectionSource.Catalogue:
                    SelectedCatalogueTrack = catalogueTrack;
                    break;
                case PrimarySelectionSource.Community:
                    SelectedCommunitySong = communitySong;
                    break;
            }
        }
        finally
        {
            _suppressExclusiveSelection = false;
        }
    }

    private void SyncListSelectionForActivePlayback(ActivePlaybackList list, Song? song = null, CatalogueTrack? catalogueTrack = null, CommunitySong? communitySong = null)
    {
        switch (list)
        {
            case ActivePlaybackList.Library when song is not null:
                SetPrimaryListSelection(PrimarySelectionSource.Library, song);
                break;
            case ActivePlaybackList.Favorites when song is not null:
                SetPrimaryListSelection(PrimarySelectionSource.Favorites, song);
                break;
            case ActivePlaybackList.Playlist when song is not null:
                SetPrimaryListSelection(PrimarySelectionSource.Playlist, song);
                break;
            case ActivePlaybackList.Catalogue when catalogueTrack is not null:
                SetPrimaryListSelection(PrimarySelectionSource.Catalogue, null, catalogueTrack);
                break;
            case ActivePlaybackList.Community when communitySong is not null:
                SetPrimaryListSelection(PrimarySelectionSource.Community, null, communitySong: communitySong);
                break;
        }
    }

    private Song? GetSelectedSongForList(ActivePlaybackList list) =>
        list switch
        {
            ActivePlaybackList.Library => SelectedLibrarySong ?? _lastSelectedLibrarySong,
            ActivePlaybackList.Favorites => SelectedFavoriteSong ?? _lastSelectedFavoriteSong,
            ActivePlaybackList.Playlist => SelectedPlaylistSong ?? _lastSelectedPlaylistSong,
            _ => null
        };

    private CatalogueTrack? GetSelectedCatalogueTrack() =>
        SelectedCatalogueTrack ?? _lastSelectedCatalogueTrack;

    /// <summary>Explicit catalogue click, else the now-playing row highlight, else last remembered.</summary>
    private CatalogueTrack? GetNavigationCatalogueTrack() =>
        SelectedCatalogueTrack ?? _nowPlayingCatalogueTrack ?? _lastSelectedCatalogueTrack;

    private Song? GetNavigationSongForList(ActivePlaybackList list)
    {
        var selected = list switch
        {
            ActivePlaybackList.Library => SelectedLibrarySong,
            ActivePlaybackList.Favorites => SelectedFavoriteSong,
            ActivePlaybackList.Playlist => SelectedPlaylistSong,
            _ => null
        };
        if (selected is not null)
            return selected;
        if (_nowPlaying is not null && SongIsInList(_nowPlaying, list))
            return _nowPlaying;
        return GetSelectedSongForList(list);
    }

    private ActivePlaybackList ResolvePrimaryPlaybackListByPriority()
    {
        if (SelectedPlaylistSong is not null)
            return ActivePlaybackList.Playlist;
        if (SelectedCatalogueTrack is not null)
            return ActivePlaybackList.Catalogue;
        if (SelectedCommunitySong is not null)
            return ActivePlaybackList.Community;
        if (SelectedFavoriteSong is not null)
            return ActivePlaybackList.Favorites;
        if (SelectedLibrarySong is not null)
            return ActivePlaybackList.Library;
        if (_lastSelectedPlaylistSong is not null)
            return ActivePlaybackList.Playlist;
        if (_lastSelectedCatalogueTrack is not null)
            return ActivePlaybackList.Catalogue;
        if (_lastSelectedCommunitySong is not null)
            return ActivePlaybackList.Community;
        if (_lastSelectedFavoriteSong is not null)
            return ActivePlaybackList.Favorites;
        if (_lastSelectedLibrarySong is not null)
            return ActivePlaybackList.Library;
        return ActivePlaybackList.None;
    }

    private bool IsPlaybackHotkeyContextActive()
    {
        if (PlaybackHotkeysGlobal)
            return true;

        if (_gameWindow.IsGameFocused())
            return true;

        return Application.Current?.MainWindow?.IsActive == true;
    }

    private ActivePlaybackList ResolvePlaybackListForSong(Song song)
    {
        if (_activePlaybackList != ActivePlaybackList.None && SongIsInList(song, _activePlaybackList))
            return _activePlaybackList;

        return SelectedSection switch
        {
            NavigationSection.Favorites when SongIsInList(song, ActivePlaybackList.Favorites)
                => ActivePlaybackList.Favorites,
            NavigationSection.Library => ActivePlaybackList.Library,
            _ when SongIsInList(song, ActivePlaybackList.Playlist) => ActivePlaybackList.Playlist,
            _ when SongIsInList(song, ActivePlaybackList.Favorites) => ActivePlaybackList.Favorites,
            _ => ActivePlaybackList.Library
        };
    }

    private bool SongIsInList(Song song, ActivePlaybackList list) =>
        list switch
        {
            ActivePlaybackList.Library => GetFilteredLibrarySongs().Any(s => s.Id == song.Id),
            ActivePlaybackList.Favorites => GetFilteredFavoriteSongs().Any(s => s.Id == song.Id),
            ActivePlaybackList.Playlist => GetFilteredPlaylistSongs().Any(s => s.Id == song.Id),
            _ => false
        };

    private void SetActivePlaybackContext(ActivePlaybackList list, Song song)
    {
        _activePlaybackList = list;
        _activeListIndex = list switch
        {
            ActivePlaybackList.Library => FindSongIndex(GetFilteredLibrarySongs(), song),
            ActivePlaybackList.Favorites => FindSongIndex(GetFilteredFavoriteSongs(), song),
            ActivePlaybackList.Playlist => FindSongIndex(GetFilteredPlaylistSongs(), song),
            _ => -1
        };
    }

    private void SetActivePlaybackContext(ActivePlaybackList list, CatalogueTrack track)
    {
        _activePlaybackList = list;
        _activeListIndex = FindCatalogueIndex(GetFilteredCatalogueTracks(), track);
    }

    private static int FindSongIndex(IReadOnlyList<Song> songs, Song song)
    {
        for (var i = 0; i < songs.Count; i++)
        {
            if (songs[i].Id == song.Id)
                return i;
        }

        return -1;
    }

    private static int FindCatalogueIndex(IReadOnlyList<CatalogueTrack> tracks, CatalogueTrack track)
    {
        for (var i = 0; i < tracks.Count; i++)
        {
            if (tracks[i].Id == track.Id)
                return i;
        }

        return -1;
    }

    private int ResolveSongListIndex(IReadOnlyList<Song> songs, Song? selected)
    {
        if (_activeListIndex >= 0 && _activeListIndex < songs.Count)
            return _activeListIndex;

        if (selected is not null)
        {
            var selectedIndex = FindSongIndex(songs, selected);
            if (selectedIndex >= 0)
                return selectedIndex;
        }

        if (_nowPlaying is not null)
        {
            var playingIndex = FindSongIndex(songs, _nowPlaying);
            if (playingIndex >= 0)
                return playingIndex;
        }

        return songs.Count > 0 ? 0 : -1;
    }

    private int ResolveCatalogueListIndex(IReadOnlyList<CatalogueTrack> tracks, CatalogueTrack? selected)
    {
        selected ??= GetNavigationCatalogueTrack();

        if (_activePlaybackList == ActivePlaybackList.Catalogue &&
            _activeListIndex >= 0 &&
            _activeListIndex < tracks.Count)
        {
            return _activeListIndex;
        }

        if (selected is not null)
        {
            var selectedIndex = FindCatalogueIndex(tracks, selected);
            if (selectedIndex >= 0)
                return selectedIndex;
        }

        if (_nowPlayingCatalogueTrack is not null)
        {
            var playingIndex = FindCatalogueIndex(tracks, _nowPlayingCatalogueTrack);
            if (playingIndex >= 0)
                return playingIndex;
        }

        return tracks.Count > 0 ? 0 : -1;
    }

    private ActivePlaybackList ResolveActivePlaybackList()
    {
        if (_activePlaybackList != ActivePlaybackList.None)
            return _activePlaybackList;

        if (_primarySelection != PrimarySelectionSource.None)
            return MapPrimarySelection(_primarySelection);

        if (SelectedSection == NavigationSection.Catalogue || _nowPlayingCatalogueTrack is not null)
            return ActivePlaybackList.Catalogue;

        if (SelectedSection == NavigationSection.Community || _nowPlayingCommunitySong is not null)
            return ActivePlaybackList.Community;

        if (SelectedSection == NavigationSection.Library)
            return ActivePlaybackList.Library;
        if (SelectedSection == NavigationSection.Favorites)
            return ActivePlaybackList.Favorites;

        var byPriority = ResolvePrimaryPlaybackListByPriority();
        if (byPriority != ActivePlaybackList.None)
            return byPriority;

        return GetPlaybackListForCurrentSection();
    }

    private async Task PlayPrimarySelectionAsync()
    {
        var list = ResolveActivePlaybackList();
        if (list == ActivePlaybackList.Catalogue)
        {
            var track = GetNavigationCatalogueTrack();
            if (track is not null)
            {
                await PlayCatalogueTrack(track);
                return;
            }
        }
        else if (list == ActivePlaybackList.Community)
        {
            var communitySong = GetNavigationCommunitySong();
            if (communitySong is not null)
            {
                await PlayCommunitySong(communitySong);
                return;
            }
        }
        else if (list != ActivePlaybackList.None)
        {
            var song = GetSelectedSongForList(list);
            if (song is not null)
            {
                await PlaySongFromListAsync(song, list);
                return;
            }
        }

        if (_nowPlaying is not null && _playback.TotalDurationMs > 0)
        {
            StartPlaybackFromCurrentPosition();
            IsPlaying = true;
            RefreshPlayPauseUi();
        }
    }

    private void InvalidateShuffleOrder() => _shuffleOrderKey = null;

    private void EnsureShuffleOrder(int count)
    {
        var key = $"{ResolveActivePlaybackList()}:{count}";
        if (_shuffleOrder is not null && _shuffleOrderKey == key && _shuffleOrder.Count == count)
            return;

        _shuffleOrderKey = key;
        _shuffleOrder = Enumerable.Range(0, count).OrderBy(_ => Random.Shared.Next()).ToList();
        _shuffleOrderCursor = -1;
    }

    private int ResolveAdjacentIndex(int count, int current, bool forward)
    {
        if (count <= 0)
            return 0;

        if (current < 0 || current >= count)
            current = 0;

        if (Shuffle && forward && count > 1)
        {
            EnsureShuffleOrder(count);
            var position = _shuffleOrder!.IndexOf(current);
            if (position < 0)
                position = 0;

            _shuffleOrderCursor = (position + 1) % count;
            return _shuffleOrder[_shuffleOrderCursor];
        }

        if (Shuffle && !forward && count > 1)
        {
            EnsureShuffleOrder(count);
            var position = _shuffleOrder!.IndexOf(current);
            if (position < 0)
                position = 0;

            _shuffleOrderCursor = position <= 0 ? count - 1 : position - 1;
            return _shuffleOrder[_shuffleOrderCursor];
        }

        return forward
            ? (current + 1) % count
            : current <= 0 ? count - 1 : current - 1;
    }

    private void CancelAutoAdvanceTimer()
    {
        if (_autoAdvanceTimer is not null)
        {
            _autoAdvanceTimer.Stop();
            _autoAdvanceTimer.Tick -= OnAutoAdvanceTimerTick;
            _autoAdvanceTimer = null;
        }

        if (_gameStatusBeforeAutoAdvance is not null)
        {
            GameConnectionStatus = _gameStatusBeforeAutoAdvance;
            _gameStatusBeforeAutoAdvance = null;
        }
    }

    private void ScheduleAutoAdvanceAfterSongEnd()
    {
        CancelAutoAdvanceTimer();
        if (!AutoPlayEnabled)
            return;

        var seconds = Math.Max(0, AutoPlayNextDelaySeconds);
        if (seconds <= 0)
        {
            _ = AdvanceActiveListAsync(forward: true, autoStart: true);
            return;
        }

        _gameStatusBeforeAutoAdvance ??= GameConnectionStatus;
        _autoAdvanceTicksRemaining = seconds;
        UpdateAutoAdvanceCountdownStatus();

        _autoAdvanceTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _autoAdvanceTimer.Tick += OnAutoAdvanceTimerTick;
        _autoAdvanceTimer.Start();
    }

    private void UpdateAutoAdvanceCountdownStatus() =>
        GameConnectionStatus = L.F(UiText.ChromeAutoPlayCountdown, _autoAdvanceTicksRemaining);

    private void OnAutoAdvanceTimerTick(object? sender, EventArgs e)
    {
        if (!AutoPlayEnabled)
        {
            CancelAutoAdvanceTimer();
            return;
        }

        _autoAdvanceTicksRemaining--;
        if (_autoAdvanceTicksRemaining > 0)
        {
            UpdateAutoAdvanceCountdownStatus();
            return;
        }

        if (_autoAdvanceTimer is not null)
        {
            _autoAdvanceTimer.Stop();
            _autoAdvanceTimer.Tick -= OnAutoAdvanceTimerTick;
            _autoAdvanceTimer = null;
        }

        if (_gameStatusBeforeAutoAdvance is not null)
        {
            GameConnectionStatus = _gameStatusBeforeAutoAdvance;
            _gameStatusBeforeAutoAdvance = null;
        }

        _ = AdvanceActiveListAsync(forward: true, autoStart: true);
    }

    private async Task AdvanceActiveListAsync(bool forward, bool autoStart)
    {
        var listKind = ResolveActivePlaybackList();

        try
        {
            switch (listKind)
            {
                case ActivePlaybackList.Library:
                {
                    var songs = GetFilteredLibrarySongs();
                    if (songs.Count == 0)
                    {
                        StopPlaybackAtEnd();
                        return;
                    }

                    var current = ResolveSongListIndex(songs, GetNavigationSongForList(ActivePlaybackList.Library));
                    _activeListIndex = ResolveAdjacentIndex(songs.Count, current, forward);
                    _activePlaybackList = ActivePlaybackList.Library;
                    var song = songs[_activeListIndex];
                    SyncListSelectionForActivePlayback(ActivePlaybackList.Library, song);
                    SetActivePlaybackContext(ActivePlaybackList.Library, song);
                    if (autoStart)
                        await StartSongAsync(song);
                    return;
                }
                case ActivePlaybackList.Catalogue:
                {
                    var tracks = GetFilteredCatalogueTracks();
                    if (tracks.Count == 0)
                    {
                        StopPlaybackAtEnd();
                        return;
                    }

                    var current = ResolveCatalogueListIndex(tracks, GetNavigationCatalogueTrack());
                    _activeListIndex = ResolveAdjacentIndex(tracks.Count, current, forward);
                    _activePlaybackList = ActivePlaybackList.Catalogue;
                    var track = tracks[_activeListIndex];
                    SyncListSelectionForActivePlayback(ActivePlaybackList.Catalogue, catalogueTrack: track);
                    SetActivePlaybackContext(ActivePlaybackList.Catalogue, track);
                    if (autoStart)
                        await PlayCatalogueTrack(track);
                    return;
                }
                case ActivePlaybackList.Community:
                {
                    var communitySongs = GetFilteredCommunitySongs();
                    if (communitySongs.Count == 0)
                    {
                        StopPlaybackAtEnd();
                        return;
                    }

                    var current = ResolveCommunityListIndex(communitySongs, GetNavigationCommunitySong());
                    _activeListIndex = ResolveAdjacentIndex(communitySongs.Count, current, forward);
                    _activePlaybackList = ActivePlaybackList.Community;
                    var communitySong = communitySongs[_activeListIndex];
                    SyncListSelectionForActivePlayback(ActivePlaybackList.Community, communitySong: communitySong);
                    SetActivePlaybackContext(ActivePlaybackList.Community, communitySong);
                    if (autoStart)
                        await PlayCommunitySong(communitySong);
                    return;
                }
                case ActivePlaybackList.Favorites:
                {
                    var songs = GetFilteredFavoriteSongs();
                    if (songs.Count == 0)
                    {
                        StopPlaybackAtEnd();
                        return;
                    }

                    var current = ResolveSongListIndex(songs, GetNavigationSongForList(ActivePlaybackList.Favorites));
                    _activeListIndex = ResolveAdjacentIndex(songs.Count, current, forward);
                    _activePlaybackList = ActivePlaybackList.Favorites;
                    var song = songs[_activeListIndex];
                    SetPrimaryListSelection(PrimarySelectionSource.Favorites, song);
                    SetActivePlaybackContext(ActivePlaybackList.Favorites, song);
                    if (autoStart)
                        await StartSongAsync(song);
                    return;
                }
                case ActivePlaybackList.Playlist:
                {
                    var songs = GetFilteredPlaylistSongs();
                    if (songs.Count == 0)
                    {
                        StopPlaybackAtEnd();
                        return;
                    }

                    var current = ResolveSongListIndex(songs, GetNavigationSongForList(ActivePlaybackList.Playlist));
                    _activeListIndex = ResolveAdjacentIndex(songs.Count, current, forward);
                    _activePlaybackList = ActivePlaybackList.Playlist;
                    var song = songs[_activeListIndex];
                    SyncListSelectionForActivePlayback(ActivePlaybackList.Playlist, song);
                    SetActivePlaybackContext(ActivePlaybackList.Playlist, song);
                    if (autoStart)
                        await StartSongAsync(song);
                    return;
                }
                default:
                    if (autoStart)
                        StopPlaybackAtEnd();
                    break;
            }
        }
        catch (Exception ex)
        {
            AppPaths.WriteDiagnosticLog("advance-list", ex);
            if (autoStart)
            {
                IsPlaying = false;
                UpdateProgress();
            }
        }

        return;

        void StopPlaybackAtEnd()
        {
            IsPlaying = false;
            UpdateProgress();
        }
    }

    private void OnPlaybackCompleted()
    {
        CancelAutoAdvanceTimer();
        FinalizeHistory(PlaybackStatus.Completed);
        IsPlaying = false;
        UpdateProgress();

        if (Repeat && _nowPlaying is not null)
        {
            _ = StartSongAsync(_nowPlaying);
            return;
        }

        if (!AutoPlayEnabled)
            return;

        ScheduleAutoAdvanceAfterSongEnd();
    }

    private void FinalizeHistory(PlaybackStatus status)
    {
        if (_activeHistoryItem is null)
            return;

        _activeHistoryItem.Status = status;
        _activeHistoryItem.DurationMs = _playback.CurrentPositionMs;
        CommitHistoryEntry(_activeHistoryItem);
        _activeHistoryItem = null;
    }

    private void ReloadHistoryItemsFromStore()
    {
        HistoryItems.ReplaceAll(_history.Items.ToList());
        RefreshHistoryStats();
    }

    private void CommitHistoryEntry(HistoryItem item)
    {
        _history.Record(item);

        var existing = -1;
        for (var i = 0; i < HistoryItems.Count; i++)
        {
            if (HistoryItems[i].Id != item.Id)
                continue;
            existing = i;
            break;
        }

        if (existing >= 0)
            HistoryItems.RemoveAt(existing);

        HistoryItems.Insert(0, item);
        ScheduleHistoryPersist();
        RefreshHistoryStats();
    }

    private void OnUiTimerTick(object? sender, EventArgs e)
    {
        var active = _playback.State is PlaybackState.Playing or PlaybackState.Paused;
        _uiTimer.Interval = active
            ? TimeSpan.FromMilliseconds(100)
            : TimeSpan.FromMilliseconds(250);

        UpdateProgress();
        if (!active)
            return;

        if (DateTime.UtcNow - _lastPlaybackStatusUtc < TimeSpan.FromSeconds(2))
            return;

        _lastPlaybackStatusUtc = DateTime.UtcNow;
        UpdatePlaybackStatus();
    }

    private void UpdateProgress()
    {
        var current = _playback.CurrentPositionMs;
        var total = Math.Max(1, _playback.TotalDurationMs);
        var newProgress = Math.Clamp(current * 100.0 / total, 0, 100);
        if (Math.Abs(newProgress - Progress) > 0.05)
            Progress = newProgress;

        var newCurrent = TimeFormat.FromMilliseconds(current);
        if (!string.Equals(CurrentTimeText, newCurrent, StringComparison.Ordinal))
            CurrentTimeText = newCurrent;

        if (_playback.TotalDurationMs > 0)
        {
            var newTotal = TimeFormat.FromMilliseconds(_playback.TotalDurationMs);
            if (!string.Equals(TotalTimeText, newTotal, StringComparison.Ordinal))
                TotalTimeText = newTotal;
        }

        var isPlaying = _playback.State == PlaybackState.Playing;
        if (IsPlaying != isPlaying)
        {
            IsPlaying = isPlaying;
            RefreshPlayPauseUi();
        }
    }

    private void UpdatePlaybackStatus()
    {
        var game = _gameWindow.IsProcessRunning()
            ? (_gameWindow.IsGameWindowFound() ? "game OK" : "game NOT found")
            : "game NOT running";
        GameConnectionStatus =
            $"Playing | keys sent: {_input.KeysSentCount} | last: {_input.LastKeySent} via {_input.LastDeliveryMethod} | {game}";
    }

    private void ScheduleRefreshLibraryStats()
    {
        if (_libraryStatsRefreshScheduled)
            return;

        _libraryStatsRefreshScheduled = true;
        var dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
        dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            _libraryStatsRefreshScheduled = false;
            RefreshLibraryStats();
        });
    }

    private void RefreshLibraryStats()
    {
        var visible = LibrarySongs.Where(FilterLibrarySong).ToList();
        long bytes = 0;
        foreach (var song in visible)
        {
            try
            {
                if (File.Exists(song.FilePath))
                    bytes += new FileInfo(song.FilePath).Length;
            }
            catch { /* ignore */ }
        }

        LibraryHeaderText = L.F(UiText.AllSongsHeader, visible.Count);
        LibraryStatsText = L.F(UiText.StatsSongsBytes, visible.Count, TimeFormat.FormatBytes(bytes));
    }

    private void RefreshHistoryStats() =>
        HistoryStatsText = L.F(UiText.StatsPlays, HistoryItems.Count);

    private void ScheduleRefreshPlaylistStats()
    {
        if (_playlistStatsRefreshScheduled)
            return;

        _playlistStatsRefreshScheduled = true;
        var dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
        dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            _playlistStatsRefreshScheduled = false;
            _playlistViewSource.View.Refresh();
            RefreshPlaylistStats();
        });
    }

    private void RefreshPlaylistStats()
    {
        int count;
        long totalMs;
        if (PlaylistFavoritesOnly)
        {
            var visible = _playlistViewSource.View.Cast<Song>().ToList();
            count = visible.Count;
            totalMs = visible.Sum(s => s.DurationMs);
        }
        else
        {
            count = PlaylistSongs.Count;
            totalMs = 0;
            foreach (var song in PlaylistSongs)
                totalMs += song.DurationMs;
        }

        var suffix = PlaylistFavoritesOnly ? L.T(UiText.StatsFavorites) : L.T(UiText.StatsSongs);
        PlaylistStatsText = $"{count} {suffix} • {TimeFormat.FromMilliseconds(totalMs)}";
        _currentPlaylist.Name = PlaylistName;
    }

    partial void OnPlaylistNameChanged(string value)
    {
        NotifyPlaylistSectionActiveName();
        RefreshPlaylistStats();

        if (_suppressSavedPlaylistSelection)
            return;

        if (SelectedSavedPlaylist is not null
            && !string.Equals(SelectedSavedPlaylist.Name, value?.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            _suppressSavedPlaylistSelection = true;
            SelectedSavedPlaylist = null;
            _suppressSavedPlaylistSelection = false;
        }
    }

    partial void OnSelectedLibrarySongChanged(Song? value)
    {
        if (value is not null)
        {
            _lastSelectedLibrarySong = value;
            OnPrimaryListItemSelected(PrimarySelectionSource.Library);
        }
        else if (_primarySelection == PrimarySelectionSource.Library)
        {
            _primarySelection = PrimarySelectionSource.None;
        }

        NotifyTrashCommandsCanExecute();
    }

    partial void OnSelectedPlaylistSongChanged(Song? value)
    {
        if (value is not null)
        {
            _lastSelectedPlaylistSong = value;
            OnPrimaryListItemSelected(PrimarySelectionSource.Playlist);
        }
        else if (_primarySelection == PrimarySelectionSource.Playlist)
        {
            _primarySelection = PrimarySelectionSource.None;
        }

        NotifyTrashCommandsCanExecute();
    }

    partial void OnSelectedFavoriteSongChanged(Song? value)
    {
        if (value is not null)
        {
            _lastSelectedFavoriteSong = value;
            OnPrimaryListItemSelected(PrimarySelectionSource.Favorites);
        }
        else if (_primarySelection == PrimarySelectionSource.Favorites)
        {
            _primarySelection = PrimarySelectionSource.None;
        }

        NotifyTrashCommandsCanExecute();
    }

    partial void OnSelectedCatalogueTrackChanged(CatalogueTrack? value)
    {
        if (value is not null)
        {
            _lastSelectedCatalogueTrack = value;
            OnPrimaryListItemSelected(PrimarySelectionSource.Catalogue);
        }
        else if (_primarySelection == PrimarySelectionSource.Catalogue)
        {
            _primarySelection = PrimarySelectionSource.None;
        }
    }

    private bool _suppressLanguageChange;
    private bool _localizationApplyScheduled;

    private void ScheduleApplyLocalization()
    {
        var dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
        if (_localizationApplyScheduled)
            return;

        _localizationApplyScheduled = true;
        dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            _localizationApplyScheduled = false;
            ApplyLocalization();
        });
    }

    private void ApplyUiLanguageFromSettings()
    {
        LocalizationService.Instance.SetLanguage(_settings.Settings.UiLanguage);
        _suppressLanguageChange = true;
        SelectedLanguage = AvailableLanguages.FirstOrDefault(l =>
                               l.Code.Equals(LocalizationService.Instance.CurrentLanguageCode,
                                   StringComparison.OrdinalIgnoreCase))
                           ?? AvailableLanguages[0];
        _suppressLanguageChange = false;
        ApplyLocalization();
    }

    partial void OnSelectedLanguageChanged(LanguageOption? value)
    {
        if (_suppressLanguageChange || value is null)
            return;

        LocalizationService.Instance.SetLanguage(value.Code);
        _settings.Settings.UiLanguage = LocalizationService.Instance.CurrentLanguageCode;
        _settings.Save();
        ApplyLocalization();
    }

    private void RefreshThemeOptions()
    {
        if (AvailableThemes.Count < 2)
            return;

        var selectedId = SelectedTheme?.Id ?? ThemeService.CurrentId;
        AvailableThemes.Clear();
        AvailableThemes.Add(new ThemeOption { Id = ThemeService.Sakura, DisplayName = Ui.ThemeSakura });
        AvailableThemes.Add(new ThemeOption { Id = ThemeService.Wuxia, DisplayName = Ui.ThemeWuxia });
        AvailableThemes.Add(new ThemeOption { Id = ThemeService.Ffxiv, DisplayName = Ui.ThemeFfxiv });
        _suppressThemeChange = true;
        SelectedTheme = AvailableThemes.FirstOrDefault(t =>
                            t.Id.Equals(selectedId, StringComparison.OrdinalIgnoreCase))
                        ?? AvailableThemes[0];
        _suppressThemeChange = false;
    }

    private void ApplyUiThemeFromSettings()
    {
        var settings = _settings.Settings;
        // Settings written before per-game themes only offered the two WWM themes: keep that choice
        // for WWM. The FFXIV theme in the legacy slot only means the last session ran FFXIV —
        // seeding it into the WWM slot would dress WWM in Eorzea colors.
        if (settings.UiThemeByGame.Count == 0 && !string.IsNullOrWhiteSpace(settings.UiTheme))
        {
            var legacy = ThemeService.Normalize(settings.UiTheme);
            if (!legacy.Equals(ThemeService.Ffxiv, StringComparison.OrdinalIgnoreCase))
                settings.UiThemeByGame[GameProfiles.WhereWindsMeet.Id] = legacy;
        }

        // Heal settings written by the earlier migration, which copied the FFXIV theme into WWM's slot.
        if (settings.UiThemeByGame.Count == 1
            && settings.UiThemeByGame.TryGetValue(GameProfiles.WhereWindsMeet.Id, out var wwmTheme)
            && ThemeService.Normalize(wwmTheme).Equals(ThemeService.Ffxiv, StringComparison.OrdinalIgnoreCase))
            settings.UiThemeByGame.Remove(GameProfiles.WhereWindsMeet.Id);

        ApplyThemeForGame(GameProfiles.Current);
    }

    /// <summary>Dresses the app in the theme tied to the selected game (Eorzea night for FFXIV),
    /// unless the player already picked another one for that game.</summary>
    private void ApplyThemeForGame(GameProfile game)
    {
        var themeId = _settings.Settings.UiThemeByGame.TryGetValue(game.Id, out var saved)
                      && !string.IsNullOrWhiteSpace(saved)
            ? saved
            : game.SignatureThemeId;

        ThemeService.Apply(themeId, persist: false);
        _settings.Settings.UiTheme = ThemeService.CurrentId;
        // Remember what each game is wearing, so switching games always restores its last theme.
        _settings.Settings.UiThemeByGame[game.Id] = ThemeService.CurrentId;
        _suppressThemeChange = true;
        SelectedTheme = AvailableThemes.FirstOrDefault(t =>
                              t.Id.Equals(ThemeService.CurrentId, StringComparison.OrdinalIgnoreCase))
                          ?? AvailableThemes[0];
        _suppressThemeChange = false;
    }

    partial void OnSelectedThemeChanged(ThemeOption? value)
    {
        if (_suppressThemeChange || value is null)
            return;

        ThemeService.Apply(value.Id);
        _settings.Settings.UiTheme = ThemeService.CurrentId;
        _settings.Settings.UiThemeByGame[GameProfiles.Current.Id] = ThemeService.CurrentId;
        _settings.Save();
    }

    private void RebuildSongSortOptions()
    {
        var libraryMode = SelectedLibrarySortOption?.Mode ?? ParseSortMode(_settings.Settings.LibrarySortMode);
        var playlistMode = SelectedPlaylistSortOption?.Mode ?? ParseSortMode(_settings.Settings.PlaylistSortMode);
        var catalogueMode = SelectedCatalogueSortOption?.Mode ?? ParseCatalogueSortMode(_settings.Settings.CatalogueSortMode);

        LibrarySortOptions.Clear();
        PlaylistSortOptions.Clear();
        CatalogueSortOptions.Clear();
        foreach (var mode in new[]
                 {
                     SongListSortMode.Manual,
                     SongListSortMode.Name,
                     SongListSortMode.TimeAddedNewest,
                     SongListSortMode.TimeAddedOldest
                 })
        {
            var option = new SongSortOption { Mode = mode, Label = GetSortLabel(mode) };
            LibrarySortOptions.Add(option);
            PlaylistSortOptions.Add(new SongSortOption { Mode = mode, Label = option.Label });
        }

        foreach (var mode in new[] { CatalogueSortMode.PublishingDate, CatalogueSortMode.Alphabetical })
            CatalogueSortOptions.Add(new CatalogueSortOption { Mode = mode, Label = GetCatalogueSortLabel(mode) });

        _suppressSortChange = true;
        SelectedLibrarySortOption = LibrarySortOptions.FirstOrDefault(o => o.Mode == libraryMode)
                                    ?? LibrarySortOptions.FirstOrDefault();
        SelectedPlaylistSortOption = PlaylistSortOptions.FirstOrDefault(o => o.Mode == playlistMode)
                                     ?? PlaylistSortOptions.FirstOrDefault();
        SelectedCatalogueSortOption = CatalogueSortOptions.FirstOrDefault(o => o.Mode == catalogueMode)
                                        ?? CatalogueSortOptions.FirstOrDefault();
        _suppressSortChange = false;
    }

    private void ApplySortSettingsFromSaved()
    {
        SongListSortHelper.Apply(_libraryViewSource, SelectedLibrarySortOption?.Mode ?? SongListSortMode.Manual);
        SongListSortHelper.Apply(_playlistViewSource, SelectedPlaylistSortOption?.Mode ?? SongListSortMode.Manual);
        ApplyCatalogueSort();
        OnPropertyChanged(nameof(IsPlaylistManualSort));
    }

    private void ApplyCatalogueSort() =>
        SongListSortHelper.ApplyCatalogueSort(
            _catalogueViewSource,
            SelectedCatalogueSortOption?.Mode ?? ParseCatalogueSortMode(_settings.Settings.CatalogueSortMode),
            IsAllStylesFilter(CatalogueStyleFilter));

    private static SongListSortMode ParseSortMode(string? value) =>
        Enum.TryParse<SongListSortMode>(value, out var mode) ? mode : SongListSortMode.Manual;

    private static CatalogueSortMode ParseCatalogueSortMode(string? value) =>
        Enum.TryParse<CatalogueSortMode>(value, out var mode) ? mode : CatalogueSortMode.PublishingDate;

    private static string GetCatalogueSortLabel(CatalogueSortMode mode) => mode switch
    {
        CatalogueSortMode.Alphabetical => L.T(UiText.SortCatalogueAlphabetical),
        _ => L.T(UiText.SortCataloguePublishingDate)
    };

    private static string GetSortLabel(SongListSortMode mode) => mode switch
    {
        SongListSortMode.Manual => L.T(UiText.SortManual),
        SongListSortMode.Name => L.T(UiText.SortName),
        SongListSortMode.TimeAddedNewest => L.T(UiText.SortTimeAddedNewest),
        SongListSortMode.TimeAddedOldest => L.T(UiText.SortTimeAddedOldest),
        _ => L.T(UiText.SortManual)
    };

    private void ApplyLocalization()
    {
        RefreshNavLabels();
        RefreshThemeOptions();
        RebuildSongSortOptions();
        RebuildNoteMappingModes();
        ApplySortSettingsFromSaved();
        Ui.Refresh();
        OnPropertyChanged(nameof(Ui));
        OnPropertyChanged(nameof(UiFlowDirection));
        OnPropertyChanged(nameof(ChromeAutoPlayNextText));
        OnPropertyChanged(nameof(SmartTransposeStateLabel));
        OnPropertyChanged(nameof(PlaybackOctaveShiftLabel));
        OnPropertyChanged(nameof(PlayerChromeOpacityLabel));
        OnPropertyChanged(nameof(SelectedNoteMappingModeDescription));
        OnPropertyChanged(nameof(TrackMixerSummary));
        RefreshPlayPauseUi();
        NotifyPlaybackHotkeyLabels();
        RefreshIdleUiStrings();
        UpdateAllStylesLabel();

        RefreshLibraryStats();
        RefreshHistoryStats();
        RefreshPlaylistStats();
        RefreshFavoritesStats();
        RefreshCatalogueStats();
        RefreshCommunityLocalization();
        RefreshKeyLayouts();
        RefreshKeyboardLayoutPresets();
    }

    private void RefreshIdleUiStrings()
    {
        if (_nowPlaying is null)
        {
            NowPlayingTitle = L.T(UiText.NowPlayingNoSong);
            NowPlayingSubtitle = string.Empty;
        }
        else
            NowPlayingSubtitle = L.T(UiText.NowPlayingMidiFile);

        if (_playback.State is PlaybackState.Stopped)
            GameConnectionStatus = string.Empty;
    }

    private void UpdateAllStylesLabel()
    {
        if (CatalogueStyles.Count == 0)
        {
            CatalogueStyles.Add(AllStylesLabel);
            CatalogueStyleFilter = AllStylesLabel;
            return;
        }

        var wasAll = IsAllStylesFilter(CatalogueStyleFilter);
        var previous = CatalogueStyleFilter;
        CatalogueStyles[0] = AllStylesLabel;

        if (wasAll)
            CatalogueStyleFilter = AllStylesLabel;
        else if (!string.IsNullOrWhiteSpace(previous) && CatalogueStyles.Contains(previous))
            CatalogueStyleFilter = previous;
        else
            CatalogueStyleFilter = AllStylesLabel;
    }

    private void RefreshNavLabels()
    {
        if (NavItems.Count < 7)
            return;

        NavItems[0].Label = L.T(UiText.NavLibrary);
        NavItems[1].Label = L.T(UiText.NavCatalogue);
        NavItems[2].Label = L.T(UiText.NavCommunity);
        NavItems[3].Label = L.T(UiText.NavPractice);
        NavItems[4].Label = L.T(UiText.NavFavorites);
        NavItems[5].Label = L.T(UiText.NavHistory);
        NavItems[6].Label = L.T(UiText.NavSettings);
    }

    private void NotifyTrashCommandsCanExecute()
    {
        RemoveFromLibraryCommand.NotifyCanExecuteChanged();
        RemoveFromPracticeLibraryCommand.NotifyCanExecuteChanged();
        RemoveFromPlaylistCommand.NotifyCanExecuteChanged();
        RemoveFromFavoritesCommand.NotifyCanExecuteChanged();
    }

    partial void OnSmartTransposeChanged(bool value)
    {
        _settings.Settings.SmartTranspose = value;
        ScheduleSettingsSave();
        OnPropertyChanged(nameof(SmartTransposeStateLabel));
    }

    partial void OnStrictNoteRangeChanged(bool value)
    {
        _settings.Settings.StrictNoteRange = value;
        ScheduleSettingsSave();
    }

    private bool _playbackTimingReloadScheduled;

    private void SchedulePlaybackTimingReload()
    {
        if (_nowPlaying is null)
            return;

        if (_playbackTimingReloadScheduled)
            return;

        _playbackTimingReloadScheduled = true;
        UiDispatcher.Post(async () =>
        {
            _playbackTimingReloadScheduled = false;
            if (_nowPlaying is null)
                return;

            if (_playback.State is PlaybackState.Playing or PlaybackState.Paused)
                await ReprepareCurrentSongScheduleAsync();
        });
    }

    partial void OnNoteDelayMsChanged(int value)
    {
        var clamped = Math.Clamp(value, 0, 50);
        if (clamped != value)
            NoteDelayMs = clamped;

        _settings.Settings.NoteDelayMs = clamped;
        ApplyInputAndWindowSettings();
        ScheduleSettingsSave();
        SchedulePlaybackTimingReload();
    }

    partial void OnChordRollDelayMsChanged(int value)
    {
        var clamped = Math.Max(0, value);
        if (clamped != value)
            ChordRollDelayMs = clamped;

        _settings.Settings.ChordRollDelayMs = clamped;
        ScheduleSettingsSave();
        SchedulePlaybackTimingReload();
    }

    partial void OnModifierDelayMsChanged(int value)
    {
        var clamped = Math.Clamp(value, 0, 50);
        if (clamped != value)
            ModifierDelayMs = clamped;

        _settings.Settings.ModifierDelayMs = clamped;
        ScheduleSettingsSave();
        _input.ConfigureModifierDelay(() => ModifierDelayMs);
    }

    partial void OnPlayerChromeOpacityPercentChanged(int value)
    {
        var clamped = Math.Clamp(value, 15, 100);
        if (clamped != value)
            PlayerChromeOpacityPercent = clamped;

        OnPropertyChanged(nameof(PlayerChromeOpacity));
        OnPropertyChanged(nameof(PlayerChromeTextOpacity));
        OnPropertyChanged(nameof(PlayerChromeOpacityLabel));
    }

    private bool _suppressPlaybackCalibrationChange;

    partial void OnPlaybackOctaveShiftChanged(int value)
    {
        var clamped = Math.Clamp(value, SongPlaybackCalibration.MinOctaveShift, SongPlaybackCalibration.MaxOctaveShift);
        if (clamped != value)
            PlaybackOctaveShift = clamped;

        OnPropertyChanged(nameof(PlaybackOctaveShiftLabel));

        if (_suppressPlaybackCalibrationChange)
            return;

        SavePlaybackCalibration();
        SchedulePlaybackCalibrationReload();
    }

    private int[] _mutedTrackSnapshot = [];

    private int[] GetMutedTrackIndexes() => _mutedTrackSnapshot;

    public string TrackMixerSummary
    {
        get
        {
            var total = PlaybackTrackMixItems.Count;
            var muted = _mutedTrackSnapshot.Length;
            return muted == 0
                ? L.T(UiText.MidiTrackAll)
                : $"{total - muted}/{total} 🔇";
        }
    }

    [RelayCommand]
    private void ToggleTrackMute(PlaybackTrackMixItem? item)
    {
        if (item is null)
            return;

        item.IsMuted = !item.IsMuted;
        OnTrackMixChanged();
    }

    [RelayCommand]
    private void SoloTrack(PlaybackTrackMixItem? item)
    {
        if (item is null)
            return;

        // Solo again on an already-solo track brings everything back.
        var alreadySolo = !item.IsMuted && PlaybackTrackMixItems.All(t => t == item || t.IsMuted);
        foreach (var track in PlaybackTrackMixItems)
            track.IsMuted = !alreadySolo && track != item;
        OnTrackMixChanged();
    }

    [RelayCommand]
    private void UnmuteAllTracks()
    {
        foreach (var track in PlaybackTrackMixItems)
            track.IsMuted = false;
        OnTrackMixChanged();
    }

    private void OnTrackMixChanged()
    {
        _mutedTrackSnapshot = PlaybackTrackMixItems
            .Where(t => t.IsMuted)
            .Select(t => t.TrackIndex)
            .ToArray();
        OnPropertyChanged(nameof(TrackMixerSummary));

        if (_suppressPlaybackCalibrationChange)
            return;

        SavePlaybackCalibration();
        SchedulePlaybackCalibrationReload();
    }

    partial void OnPlaybackPhraseFoldChanged(bool value)
    {
        if (_suppressPlaybackCalibrationChange)
            return;

        SavePlaybackCalibration();
        SchedulePlaybackCalibrationReload();
    }

    partial void OnSelectedNoteMappingModeChanged(NoteMappingModeOption? value)
    {
        if (value is not null)
        {
            _settings.Settings.DefaultNoteMappingMode = value.Mode;
            ScheduleSettingsSave();
        }

        OnPropertyChanged(nameof(SelectedNoteMappingModeDescription));

        if (_suppressPlaybackCalibrationChange)
            return;

        SavePlaybackCalibration();
        SchedulePlaybackCalibrationReload();
    }

    [RelayCommand]
    private void OctaveShiftDown()
    {
        if (PlaybackOctaveShift > SongPlaybackCalibration.MinOctaveShift)
            PlaybackOctaveShift--;
    }

    [RelayCommand]
    private void OctaveShiftUp()
    {
        if (PlaybackOctaveShift < SongPlaybackCalibration.MaxOctaveShift)
            PlaybackOctaveShift++;
    }

    private void RebuildNoteMappingModes()
    {
        var selected = SelectedNoteMappingMode?.Mode ?? _settings.Settings.DefaultNoteMappingMode;
        var ffxiv = GameProfiles.Current == GameProfiles.FinalFantasyXiv;

        // Per-game chromatic default: WWM keeps its Chromatic 36, FFXIV gets Chromatic FFXIV (37).
        // Switching games swaps the entry and remaps the selection to the current game's chromatic.
        if (ffxiv && selected == NoteMappingMode.Chromatic36)
            selected = NoteMappingMode.ChromaticFfxiv37;
        else if (!ffxiv && selected == NoteMappingMode.ChromaticFfxiv37)
            selected = NoteMappingMode.Chromatic36;

        NoteMappingModes.Clear();
        NoteMappingModes.Add(ffxiv
            ? new NoteMappingModeOption
            {
                Mode = NoteMappingMode.ChromaticFfxiv37,
                DisplayName = L.T(UiText.NoteMappingChromatic37),
                Description = L.T(UiText.NoteMappingChromatic37Hint)
            }
            : new NoteMappingModeOption
            {
                Mode = NoteMappingMode.Chromatic36,
                DisplayName = L.T(UiText.NoteMappingChromatic36),
                Description = L.T(UiText.NoteMappingChromatic36Hint)
            });
        NoteMappingModes.Add(new NoteMappingModeOption
        {
            Mode = NoteMappingMode.TransposeOnly,
            DisplayName = L.T(UiText.NoteMappingTransposeOnly),
            Description = L.T(UiText.NoteMappingTransposeOnlyHint)
        });
        NoteMappingModes.Add(new NoteMappingModeOption
        {
            Mode = NoteMappingMode.ClosestNatural,
            DisplayName = L.T(UiText.NoteMappingClosestNatural),
            Description = L.T(UiText.NoteMappingClosestNaturalHint)
        });

        _suppressPlaybackCalibrationChange = true;
        SelectedNoteMappingMode = NoteMappingModes.FirstOrDefault(m => m.Mode == selected)
            ?? NoteMappingModes.FirstOrDefault();
        _suppressPlaybackCalibrationChange = false;
        OnPropertyChanged(nameof(SelectedNoteMappingModeDescription));
    }

    private void ApplyPlaybackCalibrationOnLoad(string filePath)
    {
        var calibration = _songPlayback.GetOrDefault(filePath);
        if (calibration.IsDefault)
            calibration.MappingMode = _settings.Settings.DefaultNoteMappingMode;

        // Suppress before RebuildPlaybackTrackMix — it mutates mute state and would
        // otherwise schedule a reprepare / save against the previous now-playing song.
        _suppressPlaybackCalibrationChange = true;
        try
        {
            var tracks = _midiParser.GetTracks(filePath);

            // Migration: single-track selection predates the mixer — becomes a solo.
            var muted = calibration.MutedTracks.ToHashSet();
            if (muted.Count == 0 && calibration.TrackIndex >= 0)
                muted = tracks.Where(t => t.Index != calibration.TrackIndex).Select(t => t.Index).ToHashSet();

            RebuildPlaybackTrackMix(tracks, muted);

            PlaybackOctaveShift = Math.Clamp(
                calibration.OctaveShift,
                SongPlaybackCalibration.MinOctaveShift,
                SongPlaybackCalibration.MaxOctaveShift);

            // Migration: Phrase Fold briefly shipped as a mapping mode; it is additive now.
            if (calibration.MappingMode == NoteMappingMode.PhraseFold)
            {
                calibration.MappingMode = _settings.Settings.DefaultNoteMappingMode;
                calibration.PhraseFold = true;
            }

            PlaybackPhraseFold = calibration.PhraseFold;
            SelectedNoteMappingMode = NoteMappingModes.FirstOrDefault(m => m.Mode == calibration.MappingMode)
                ?? NoteMappingModes.FirstOrDefault();
            ShowMidiTrackSelector = PlaybackTrackMixItems.Count > 1;
            OnPropertyChanged(nameof(PlaybackOctaveShiftLabel));
        }
        finally
        {
            _suppressPlaybackCalibrationChange = false;
        }
    }

    private void RebuildPlaybackTrackMix(IReadOnlyList<MidiTrackInfo> tracks, IReadOnlySet<int> mutedTracks)
    {
        IsTrackMixerOpen = false;
        PlaybackTrackMixItems.Clear();
        foreach (var track in tracks)
        {
            PlaybackTrackMixItems.Add(new PlaybackTrackMixItem
            {
                TrackIndex = track.Index,
                DisplayName = string.IsNullOrWhiteSpace(track.Name) ? $"Track {track.Index + 1}" : track.Name,
                NoteCountDisplay = $"{track.NoteCount:N0} ♪",
                IsMuted = mutedTracks.Contains(track.Index)
            });
        }

        _mutedTrackSnapshot = PlaybackTrackMixItems
            .Where(t => t.IsMuted)
            .Select(t => t.TrackIndex)
            .ToArray();
        OnPropertyChanged(nameof(TrackMixerSummary));
    }

    private void SavePlaybackCalibration()
    {
        if (_nowPlaying is null)
            return;

        _songPlayback.Set(_nowPlaying.FilePath, new SongPlaybackCalibration
        {
            OctaveShift = PlaybackOctaveShift,
            TrackIndex = -1,
            MutedTracks = [.. _mutedTrackSnapshot],
            MappingMode = SelectedNoteMappingMode?.Mode ?? NoteMappingMode.Chromatic36,
            PhraseFold = PlaybackPhraseFold
        });
        _songPlayback.Save();
    }

    private bool _playbackCalibrationReloadScheduled;

    private void SchedulePlaybackCalibrationReload()
    {
        if (_nowPlaying is null)
            return;

        if (_playbackCalibrationReloadScheduled)
            return;

        _playbackCalibrationReloadScheduled = true;
        UiDispatcher.Post(async () =>
        {
            _playbackCalibrationReloadScheduled = false;
            if (_nowPlaying is null)
                return;

            if (_playback.State is PlaybackState.Playing or PlaybackState.Paused)
                await ReprepareCurrentSongScheduleAsync();
        });
    }

    public string PlaybackOctaveShiftLabel =>
        L.F(UiText.ChromeOctaveShift, PlaybackOctaveShift);

    public double PlayerChromeOpacity =>
        Math.Clamp(PlayerChromeOpacityPercent, 15, 100) / 100.0;

    public double PlayerChromeTextOpacity => PlayerChromeOpacity;

    public string PlayerChromeOpacityLabel =>
        L.F(UiText.ChromePlayerOpacity, PlayerChromeOpacityPercent);


    partial void OnPlaybackHotkeysGlobalChanged(bool value)
    {
        _settings.Settings.PlaybackHotkeysGlobal = value;
        ScheduleSettingsSave();
    }

    partial void OnAutoPlayEnabledChanged(bool value)
    {
        _settings.Settings.AutoPlayEnabled = value;
        ScheduleSettingsSave();
        OnPropertyChanged(nameof(ChromeAutoPlayNextText));

        if (!value)
            CancelAutoAdvanceTimer();
    }

    partial void OnAutoPlayNextDelaySecondsChanged(int value)
    {
        var clamped = Math.Clamp(value, 0, 3600);
        if (clamped != value)
        {
            AutoPlayNextDelaySeconds = clamped;
            return;
        }

        _settings.Settings.AutoPlayNextDelaySeconds = clamped;
        ScheduleSettingsSave();
        OnPropertyChanged(nameof(ChromeAutoPlayNextText));
    }

    partial void OnShuffleChanged(bool value)
    {
        _settings.Settings.Shuffle = value;
        ScheduleSettingsSave();
        InvalidateShuffleOrder();
    }

    partial void OnRepeatChanged(bool value)
    {
        _settings.Settings.Repeat = value;
        ScheduleSettingsSave();
    }

    private void ResetPlaybackTempoForSong(double beatsPerMinute)
    {
        SongTempoBpm = Math.Clamp(beatsPerMinute, 40, 320);
        _suppressTempoChange = true;
        PlaybackTempoPercent = 100;
        _suppressTempoChange = false;
        _playback.SetTempoMultiplier(1.0);
    }

    private void ApplySongTempoOnLoad(string filePath)
    {
        var percent = 100;
        if (_songTempo.TryGetPercent(filePath, out var saved))
            percent = saved;

        _suppressTempoChange = true;
        PlaybackTempoPercent = percent;
        _suppressTempoChange = false;
        _playback.SetTempoMultiplier(percent / 100.0);
        _sessionTempoPercent = percent;
        NotifyPlaybackTempoUi();
    }

    private void NotifyPlaybackTempoUi()
    {
        OnPropertyChanged(nameof(EffectiveTempoBpm));
        OnPropertyChanged(nameof(PlaybackTempoDisplay));
        OnPropertyChanged(nameof(IsTempoSliderEnabled));
        OnPropertyChanged(nameof(CanResetPlaybackTempo));
        OnPropertyChanged(nameof(CanSaveSongTempo));
        SaveSongTempoCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanSaveSongTempo))]
    private void SaveSongTempo()
    {
        if (_nowPlaying is null)
            return;

        _songTempo.SetPercent(_nowPlaying.FilePath, PlaybackTempoPercent);
        try
        {
            _songTempo.Save();
        }
        catch (Exception ex)
        {
            AppPaths.WriteDiagnosticLog("song-tempo-save", ex);
            DebraDialogs.Error("Tempo", "Could not save tempo for this song.");
            return;
        }

        _sessionTempoPercent = PlaybackTempoPercent;
        NotifyPlaybackTempoUi();
    }

    [RelayCommand]
    private void ResetPlaybackTempo()
    {
        if (!CanResetPlaybackTempo)
            return;

        _suppressTempoChange = true;
        PlaybackTempoPercent = 100;
        _suppressTempoChange = false;
        _playback.SetTempoMultiplier(1.0);
        NotifyPlaybackTempoUi();
    }

    partial void OnPlaybackTempoPercentChanged(int value)
    {
        if (_suppressTempoChange)
            return;

        var clamped = Math.Clamp(value, 50, 200);
        if (clamped != value)
        {
            _suppressTempoChange = true;
            PlaybackTempoPercent = clamped;
            _suppressTempoChange = false;
        }

        _playback.SetTempoMultiplier(clamped / 100.0);
        NotifyPlaybackTempoUi();
    }

    partial void OnPracticeTempoPercentChanged(int value)
    {
        if (_suppressPracticeTempoChange)
            return;

        var clamped = Math.Clamp(value, 50, 200);
        if (clamped != value)
        {
            _suppressPracticeTempoChange = true;
            PracticeTempoPercent = clamped;
            _suppressPracticeTempoChange = false;
        }

        _practiceSession.SetTempoPercent(clamped);
        OnPropertyChanged(nameof(PracticeTempoDisplay));
    }

    partial void OnVolumeChanged(int value)
    {
        var clamped = Math.Clamp(value, 0, 100);
        if (clamped != value)
        {
            _suppressVolumeSync = true;
            Volume = clamped;
            _suppressVolumeSync = false;
        }

        _settings.Settings.Volume = Volume;
        ScheduleSettingsSave();

        if (!_suppressVolumeSync)
            ApplyVolumeToSystem();
    }

    private void ApplyVolumeToSystem()
    {
        _systemVolume.SetMasterVolumePercent(Volume);
        ApplySynthVolume();
    }

    private void ApplySynthVolume() =>
        _practiceSound.SetMasterVolume(Volume / 100f);

    partial void OnGameWindowTitleContainsChanged(string value)
    {
        _settings.Settings.GameWindowTitleContains = value;
        ScheduleSettingsSave();
    }

    partial void OnFocusGameBeforePlayChanged(bool value)
    {
        _settings.Settings.FocusGameBeforePlay = value;
        ScheduleSettingsSave();
    }

    partial void OnPracticeAcademyCountdownSecondsChanged(int value)
    {
        var clamped = Math.Clamp(value, 0, 30);
        if (clamped != value)
            PracticeAcademyCountdownSeconds = clamped;

        _settings.Settings.AcademyPracticeCountdownSeconds = clamped;
        ScheduleSettingsSave();
    }

    partial void OnIsPracticeLessonArmedChanged(bool value) =>
        OnPropertyChanged(nameof(ShowPracticeCenterPlay));

    partial void OnIsPracticeCountdownActiveChanged(bool value) =>
        OnPropertyChanged(nameof(ShowPracticeCenterPlay));

    partial void OnIsPracticeAcademyOverlayOpenChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowAcademyTourOnPiano));
        if (value)
            _ = ActivatePracticeAcademyOverlayAsync();
        else
            EndAcademyTour();
    }

    partial void OnPrePlayCountdownSecondsChanged(int value)
    {
        _settings.Settings.PrePlayCountdownSeconds = value;
        ScheduleSettingsSave();
    }

    partial void OnSelectedLayoutChanged(KeyLayoutOption? value)
    {
        if (_suppressLayoutChange || value is null)
            return;
        LoadKeyMapping(value.FileName);
    }

    partial void OnSelectedSectionChanged(NavigationSection value)
    {
        if (value != NavigationSection.Practice)
        {
            StopPracticeSession();
            DisableLiveMidiAfterPractice();
            _input.ClearLiveQueue();
        }

        if (value == NavigationSection.Practice)
        {
            IsPracticeAcademyOverlayOpen = false;
            EndAcademyTour();
            AcademyPanel.EnsureLoaded();
            SyncPracticeSoundState();
            _ = SyncPracticeLiveInputAsync();
            RequestPracticeTour();
        }

        if (value == NavigationSection.Community)
            EnsureCommunityLoaded();

        _activePlaybackList = value switch
        {
            NavigationSection.Catalogue => ActivePlaybackList.Catalogue,
            NavigationSection.Community => ActivePlaybackList.Community,
            NavigationSection.Library => ActivePlaybackList.Library,
            NavigationSection.Favorites => ActivePlaybackList.Favorites,
            NavigationSection.History => ActivePlaybackList.Playlist,
            _ => _activePlaybackList
        };

        UpdateNavActive();
        OnPropertyChanged(nameof(ShowMainPanels));
        OnPropertyChanged(nameof(ShowSettingsPanel));
        OnPropertyChanged(nameof(ShowPracticePanel));
        OnPropertyChanged(nameof(ShowLibraryPanel));
        OnPropertyChanged(nameof(ShowHistoryPanel));
        OnPropertyChanged(nameof(ShowCataloguePanel));
        OnPropertyChanged(nameof(ShowCommunityPanel));
        OnPropertyChanged(nameof(ShowFavoritesPanel));
        OnPropertyChanged(nameof(ShowPlaylistPanel));
        OnPropertyChanged(nameof(ShowDebraPlayerChrome));
        OnPropertyChanged(nameof(IsFfxivChatVisible));
    }

    public void RequestPracticeTour(bool force = false)
    {
        if (!force && _settings.Settings.PracticeTourDismissed)
            return;

        PracticeTourRequested?.Invoke();
    }

    public void CompletePracticeTour(bool dontShowAgain)
    {
        if (dontShowAgain)
        {
            _settings.Settings.PracticeTourDismissed = true;
            ScheduleSettingsSave();
        }
    }

    public double GetMainPanelLeftRatio() =>
        Math.Clamp(_settings.Settings.MainPanelLeftRatio, 0.06, 0.94);

    public void SaveMainPanelLeftRatio(double ratio)
    {
        _settings.Settings.MainPanelLeftRatio = Math.Clamp(ratio, 0.06, 0.94);
        ScheduleSettingsSave();
    }

    public void SaveWindowState(Window window)
    {
        _settings.Settings.WindowLeft = SafeCoord(window.Left);
        _settings.Settings.WindowTop = SafeCoord(window.Top);
        _settings.Settings.WindowWidth = window.Width;
        _settings.Settings.WindowHeight = window.Height;
        _settingsSaveDebounce?.Cancel();
        _settings.Save();
    }

    public void ApplyWindowState(Window window)
    {
        const double defaultWidth = 1024;
        const double defaultHeight = 682;
        const double minWindowWidth = 940;
        const double minWindowHeight = 640;
        const double maxWindowWidth = 3840;
        const double maxWindowHeight = 2160;

        var width = _settings.Settings.WindowWidth;
        var height = _settings.Settings.WindowHeight;
        if (width <= 0 || width > maxWindowWidth || double.IsNaN(width))
            width = defaultWidth;
        if (height <= 0 || height > maxWindowHeight || double.IsNaN(height))
            height = defaultHeight;

        window.Width = Math.Clamp(width, minWindowWidth, maxWindowWidth);
        window.Height = Math.Clamp(height, minWindowHeight, maxWindowHeight);
        window.Topmost = WindowAlwaysOnTop;

        WindowPlacementHelper.CenterOnLaunchAnchor(window, _gameWindow);
    }

    partial void OnWindowAlwaysOnTopChanged(bool value)
    {
        _settings.Settings.WindowAlwaysOnTop = value;
        ScheduleSettingsSave();
    }

    private static double? SafeCoord(double value) =>
        double.IsFinite(value) ? value : null;

    public void Dispose()
    {
        _uiTimer.Stop();
        _globalHotkey.Dispose();
        _liveMidi.Dispose();
        _practiceSession.Dispose();
        _playback.Dispose();
        _practiceSound.Dispose();
        _midiSoundEngine.Dispose();
        _hypnotoadRetryTimer?.Stop();
        _hypnotoad.Dispose();
        _systemVolume.Dispose();
        _settings.Save();
        _history.Save();
        _songMetadataCache.Flush();
    }
}
