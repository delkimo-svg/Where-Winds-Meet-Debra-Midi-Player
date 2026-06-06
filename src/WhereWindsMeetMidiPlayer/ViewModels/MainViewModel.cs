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
using WhereWindsMeetMidiPlayer.Services.Discord;

namespace WhereWindsMeetMidiPlayer.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly MidiParserService _midiParser = new();
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
    private readonly CollectionViewSource _favoritesViewSource = new();
    private readonly CollectionViewSource _catalogueViewSource = new();
    private readonly CollectionViewSource _playlistViewSource = new();
    private readonly DiscordCatalogueService _discordCatalogue = new();
    private readonly SharedCatalogueService _sharedCatalogue = new();
    private readonly AppUpdateService _appUpdate = new();
    private readonly GlobalPlaybackHotkeyService _globalHotkey;
    private ReleaseManifest? _pendingUpdateManifest;
    private readonly SystemVolumeService _systemVolume = new();

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
    [ObservableProperty] private int _noteDelayMs = 2;
    [ObservableProperty] private int _chordRollDelayMs = 12;
    [ObservableProperty] private int _autoPlayNextDelaySeconds;
    [ObservableProperty] private bool _autoPlayEnabled;
    [ObservableProperty] private bool _shuffle;
    [ObservableProperty] private bool _repeat;
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
    [ObservableProperty] private bool _focusGameBeforePlay;
    [ObservableProperty] private int _prePlayCountdownSeconds = 1;
    [ObservableProperty] private string _gameConnectionStatus = string.Empty;

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

    private DiscordCredentials? _discordCredentials;

    public ObservableCollection<Song> LibrarySongs { get; } = [];
    public ObservableCollection<Song> FavoriteSongs { get; } = [];
    public BulkObservableCollection<CatalogueTrack> CatalogueTracks { get; } = [];
    public BulkObservableCollection<string> CatalogueStyles { get; } = [];
    public BulkObservableCollection<Song> PlaylistSongs { get; } = [];
    public BulkObservableCollection<HistoryItem> HistoryItems { get; } = [];
    public ObservableCollection<KeyLayoutOption> KeyLayouts { get; } = [];
    public ObservableCollection<NavItemViewModel> NavItems { get; } = [];
    public BulkObservableCollection<SavedPlaylistEntry> SavedPlaylists { get; } = [];
    public ObservableCollection<LanguageOption> AvailableLanguages { get; } = [];
    public ObservableCollection<ThemeOption> AvailableThemes { get; } = [];
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

    public bool IsPlaylistManualSort => SelectedPlaylistSortOption?.Mode == SongListSortMode.Manual;

    private enum PrimarySelectionSource
    {
        None,
        Playlist,
        Catalogue,
        Favorites,
        Library
    }

    public ICollectionView FilteredLibrarySongs => _libraryViewSource.View;
    public ICollectionView FilteredCatalogueTracks => _catalogueViewSource.View;
    public ICollectionView FilteredPlaylistSongs => _playlistViewSource.View;
    public ICollectionView FilteredFavoriteSongs => _favoritesViewSource.View;

    public bool ShowMainPanels => SelectedSection != NavigationSection.Settings;
    public bool ShowSettingsPanel => SelectedSection == NavigationSection.Settings;
    public bool ShowLibraryPanel => SelectedSection == NavigationSection.Library;
    public bool ShowFavoritesPanel => SelectedSection == NavigationSection.Favorites;
    public bool ShowPlaylistPanel =>
        ShowMainPanels && SelectedSection is not NavigationSection.Settings;
    public bool ShowHistoryPanel => SelectedSection == NavigationSection.History;
    public bool ShowCataloguePanel => SelectedSection == NavigationSection.Catalogue;

    public FlowDirection UiFlowDirection => LocalizationService.Instance.FlowDirection;

    public string ChromeAutoPlayNextText =>
        !AutoPlayEnabled
            ? L.T(UiText.ChromeAutoPlayNextOff)
            : AutoPlayNextDelaySeconds > 0
                ? L.F(UiText.ChromeAutoPlayNext, AutoPlayNextDelaySeconds)
                : L.T(UiText.ChromeAutoPlayNextImmediate);

    public string SmartTransposeStateLabel =>
        SmartTranspose ? L.T(UiText.ChromeOn) : L.T(UiText.ChromeOff);

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

    public string PlaybackTempoDisplay =>
        _nowPlaying is null ? "—" : $"{EffectiveTempoBpm}";

    public int EffectiveTempoBpm =>
        (int)Math.Round(SongTempoBpm * PlaybackTempoPercent / 100.0, MidpointRounding.AwayFromZero);

    private static string AllStylesLabel => L.T(UiText.AllStyles);

    public MainViewModel()
    {
        var uiDispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

        _playlistService = new PlaylistService(_midiParser, _noteRange);
        _library = new LibraryService(_playlistService);
        _input = new InputService(_gameWindow);
        _playback = new PlaybackEngine(_input);

        _libraryViewSource.Source = LibrarySongs;
        _libraryViewSource.View.Filter = FilterLibrarySong;
        LibrarySongs.CollectionChanged += (_, _) =>
        {
            ScheduleRefreshLibraryStats();
            RefreshFavoriteSongs();
            NotifyTrashCommandsCanExecute();
            ClearLibraryCommand.NotifyCanExecuteChanged();
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
            uiDispatcher);

        NavItems.Add(new NavItemViewModel { Section = NavigationSection.Library, Icon = "📚" });
        NavItems.Add(new NavItemViewModel { Section = NavigationSection.Catalogue, Icon = "☁" });
        NavItems.Add(new NavItemViewModel { Section = NavigationSection.Favorites, Icon = "♥" });
        NavItems.Add(new NavItemViewModel { Section = NavigationSection.History, Icon = "🕐" });
        NavItems.Add(new NavItemViewModel { Section = NavigationSection.Settings, Icon = "⚙" });

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

        try
        {
            _history.Load();
        }
        catch (Exception ex)
        {
            AppPaths.WriteDiagnosticLog("history-load", ex);
        }

        SmartTranspose = _settings.Settings.SmartTranspose;
        StrictNoteRange = _settings.Settings.StrictNoteRange;
        NoteDelayMs = _settings.Settings.NoteDelayMs;
        ChordRollDelayMs = _settings.Settings.ChordRollDelayMs;
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
        TargetProcessName = _settings.Settings.TargetProcessName;
        GameWindowTitleContains = _settings.Settings.GameWindowTitleContains;
        ReleaseManifestUrl = _settings.Settings.ReleaseManifestUrl ?? string.Empty;
        FocusGameBeforePlay = false;
        _settings.Settings.FocusGameBeforePlay = false;
        PrePlayCountdownSeconds = 1;
        _settings.Settings.PrePlayCountdownSeconds = 1;
        DiscordCredentialStore.MigrateFromSettings(_settings);
        _discordCredentials = DiscordCredentialStore.Load();

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
        RefreshKeyLayouts();
        LoadKeyMapping(_settings.Settings.KeyMappingFile);

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

    private void EnsureKeyMaps()
    {
        // Refresh from bundled assets so in-game layout updates apply after app updates.
        _keyMapping.EnsureDefaultKeyMap("default-keymap.json", updateFromBundle: true);
        _keyMapping.EnsureDefaultKeyMap("debra-36-keys.json", updateFromBundle: true);
    }

    private void RefreshKeyLayouts()
    {
        KeyLayouts.Clear();
        foreach (var file in Directory.GetFiles(AppPaths.KeyMapsFolder, "*.json").OrderBy(f => f))
        {
            var name = Path.GetFileName(file);
            var display = name switch
            {
                "debra-36-keys.json" => "Debra 36 Keys",
                "default-keymap.json" => "Default 36 Keys",
                _ => Path.GetFileNameWithoutExtension(name)
            };
            KeyLayouts.Add(new KeyLayoutOption { FileName = name, DisplayName = display });
        }

        var pick = KeyLayouts.FirstOrDefault(k => k.FileName == _settings.Settings.KeyMappingFile)
                   ?? KeyLayouts.FirstOrDefault();
        SelectedLayout = pick;
        if (pick is not null)
            LoadKeyMapping(pick.FileName);
    }

    private void LoadKeyMapping(string fileName)
    {
        var path = Path.Combine(AppPaths.KeyMapsFolder, fileName);
        if (!File.Exists(path))
            path = _keyMapping.EnsureDefaultKeyMap(fileName);
        _keyMapping.LoadFromFile(path);
        _settings.Settings.KeyMappingFile = fileName;
        _settings.Save();
    }

    public void ShowLibrarySection()
    {
        if (SelectedSection != NavigationSection.Library)
            Navigate(NavigationSection.Library);
    }

    public event Action? TourRequested;

    [RelayCommand]
    private void ShowHelp() => TourRequested?.Invoke();

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
        OnPropertyChanged(nameof(ShowLibraryPanel));
        OnPropertyChanged(nameof(ShowHistoryPanel));
        OnPropertyChanged(nameof(ShowCataloguePanel));
        OnPropertyChanged(nameof(ShowFavoritesPanel));
        OnPropertyChanged(nameof(ShowPlaylistPanel));
    }

    private void UpdateNavActive()
    {
        foreach (var item in NavItems)
            item.IsActive = item.Section == SelectedSection;
    }

    partial void OnLibrarySearchTextChanged(string value) => ScheduleRefreshLibraryView();

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
                    song = _library.AddFile(path, SmartTranspose, StrictNoteRange);
                    if (!LibrarySongs.Any(s => s.FilePath.Equals(song.FilePath, StringComparison.OrdinalIgnoreCase)))
                        LibrarySongs.Add(song);
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
               || track.Title.Contains(CatalogueSearchText, StringComparison.OrdinalIgnoreCase);
    }

    private void RefreshCatalogueStats()
    {
        var count = IsAllStylesFilter(CatalogueStyleFilter) && string.IsNullOrWhiteSpace(CatalogueSearchText)
            ? CatalogueTracks.Count
            : _catalogueViewSource.View.Cast<object>().Count();
        UpdateCatalogueStatsText(count);
    }

    private static bool IsAllStylesFilter(string? value, string allStylesLabel) =>
        string.IsNullOrWhiteSpace(value)
        || value.Equals("All styles", StringComparison.OrdinalIgnoreCase)
        || value.Equals(allStylesLabel, StringComparison.OrdinalIgnoreCase);

    private bool IsAllStylesFilter(string? value) => IsAllStylesFilter(value, AllStylesLabel);

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
                        CatalogueTrackMetadata.EnrichDuration(tracks[j], _midiParser);
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
            if (!LibrarySongs.Any(s => s.FilePath.Equals(song.FilePath, StringComparison.OrdinalIgnoreCase)))
                LibrarySongs.Add(song);
            SetPrimaryListSelection(PrimarySelectionSource.Catalogue, null, track);
            SetActivePlaybackContext(ActivePlaybackList.Catalogue, track);
            await StartSongAsync(song);
            CatalogueStatusText = "Ready.";
        }
        catch (Exception ex)
        {
            DebraDialogs.Error("Catalogue play", ex.Message);
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
            DebraDialogs.Error("Catalogue", ex.Message);
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

            if (!LibrarySongs.Any(s => s.FilePath.Equals(song.FilePath, StringComparison.OrdinalIgnoreCase)))
                LibrarySongs.Add(song);

            AddToPlaylistAt(song, insertIndex);
            CatalogueStatusText = "Added to playlist.";
        }
        catch (Exception ex)
        {
            DebraDialogs.Error("Catalogue", ex.Message);
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
        var song = _library.AddFile(path, SmartTranspose, StrictNoteRange, track.Title);
        if (!LibrarySongs.Any(s => s.FilePath.Equals(song.FilePath, StringComparison.OrdinalIgnoreCase)))
            LibrarySongs.Add(song);
        return song;
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
        SelectedLibrarySong = song;
        RefreshLibraryStats();
    }

    /// <summary>Import MIDI files or folders dropped onto the library (Explorer drag-and-drop).</summary>
    public int ImportDroppedPaths(IEnumerable<string> paths)
    {
        var added = 0;
        Song? lastAdded = null;

        foreach (var path in paths)
        {
            if (string.IsNullOrWhiteSpace(path))
                continue;

            try
            {
                if (File.Exists(path))
                {
                    if (!IsMidiFile(path))
                        continue;

                    var before = LibrarySongs.Count;
                    AddSongToLibrary(path);
                    if (LibrarySongs.Count > before)
                    {
                        added++;
                        lastAdded = SelectedLibrarySong;
                    }
                }
                else if (Directory.Exists(path))
                {
                    _settings.Settings.LastImportFolder = path;
                    var imported = _library.ImportFolder(path, SmartTranspose, StrictNoteRange);
                    foreach (var song in imported)
                    {
                        if (!LibrarySongs.Any(s => s.FilePath.Equals(song.FilePath, StringComparison.OrdinalIgnoreCase)))
                        {
                            LibrarySongs.Add(song);
                            added++;
                            lastAdded = song;
                        }
                    }
                }
            }
            catch
            {
                // Skip unreadable paths.
            }
        }

        if (added > 0)
        {
            if (lastAdded is not null)
                SelectedLibrarySong = lastAdded;
            _settings.Save();
            RefreshLibraryStats();
        }

        return added;
    }

    private static bool IsMidiFile(string path) =>
        path.EndsWith(".mid", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(".midi", StringComparison.OrdinalIgnoreCase);

    [RelayCommand]
    private void ImportLibrary()
    {
        var choice = DebraDialogs.Choose(
            "Import MIDI",
            "Import individual MIDI files or all MIDI files in a folder.",
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
                Multiselect = true
            };
            if (dialog.ShowDialog() != true || dialog.FileNames.Length == 0)
                return;

            var added = ImportDroppedPaths(dialog.FileNames);
            if (added > 0)
                ShowLibrarySection();
            return;
        }

        var folderDialog = new OpenFolderDialog { Title = "Import MIDI folder" };
        if (folderDialog.ShowDialog() != true)
            return;

        var folderAdded = ImportDroppedPaths(new[] { folderDialog.FolderName });
        if (folderAdded > 0)
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

        _settings.Settings.FavoritePaths.RemoveAll(
            p => p.Equals(song.FilePath, StringComparison.OrdinalIgnoreCase));
        RebuildFavoritePathSet();
        _settings.Save();

        if (SelectedLibrarySong?.Id == song.Id)
            SelectedLibrarySong = null;

        RefreshLibraryStats();
        SyncAllSongFavoriteFlags();
        RefreshFavoriteSongs();
    }

    private bool CanRemoveFromLibrary(Song? song) =>
        ResolveLibrarySong(song ?? SelectedLibrarySong ?? _lastSelectedLibrarySong) is not null;

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
        TargetProcessName = "wwm.exe";
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

    private void StartPlaybackFromCurrentPosition() =>
        _playback.PlayFromCurrentPosition(
            _settings.Settings.NoteDelayMs,
            _settings.Settings.ChordRollDelayMs,
            _settings.Settings.MinKeyPressDurationMs,
            _settings.Settings.IdenticalKeyGapMs);

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
            fileName =>
            {
                RefreshKeyLayouts();
                LoadKeyMapping(fileName);
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
            _suppressPlaybackUi = true;
            _playback.Stop();
            FinalizeHistory(PlaybackStatus.Stopped);

            NowPlayingTitle = CatalogueTitleHelper.GetDisplayTitle(song.Title, song.FilePath);

            var smartTranspose = SmartTranspose;
            var strictNoteRange = StrictNoteRange;
            var chordRollDelayMs = _settings.Settings.ChordRollDelayMs;
            var noteDelayMs = _settings.Settings.NoteDelayMs;
            var filePath = song.FilePath;

            var prepared = await Task.Run(() =>
            {
                var parsed = _midiParser.Parse(filePath);
                var transposed = smartTranspose
                    ? MidiTransposeService.ApplyTranspose(parsed.Notes,
                        MidiTransposeService.DetectBestTranspose(parsed.Notes))
                    : parsed.Notes.ToList();
                var ranged = _noteRange.ApplyRange(transposed, smartTranspose: true, strictMode: strictNoteRange);
                var schedule = PlaybackEngine.BuildSchedule(
                    ranged.Notes,
                    _keyMapping,
                    chordRollDelayMs,
                    noteDelayMs);
                return (parsed, ranged, schedule);
            }).ConfigureAwait(false);

            var parsed = prepared.parsed;
            var ranged = prepared.ranged;
            var schedule = prepared.schedule;

            if (schedule.Count == 0)
            {
                _suppressPlaybackUi = false;
                await UiDispatcher.RunAsync(() => DebraDialogs.Warning(
                    "Cannot play",
                    $"No playable notes ({_keyMapping.MappedNoteCount} keys in layout).\n\n" +
                    "• Settings → pick layout \"Debra 36 Keys\"\n" +
                    "• Enable Smart Transpose if the MIDI is outside C3–B5")).ConfigureAwait(true);
                return;
            }

            if (!await PrepareGameConnectionAsync().ConfigureAwait(false))
            {
                _suppressPlaybackUi = false;
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

                    if (playbackList == ActivePlaybackList.Catalogue && catalogueTrack is not null)
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
                    _playback.LoadSchedule(schedule, parsed.DurationMs);
                    TotalTimeText = TimeFormat.FromMilliseconds(parsed.DurationMs);
                    _input.ResetDiagnostics();

                    CancelAutoAdvanceTimer();
                    StartPlaybackFromCurrentPosition();

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
            }).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _suppressPlaybackUi = false;
            var failedItem = new HistoryItem
            {
                SongTitle = CatalogueTitleHelper.GetDisplayTitle(song.Title, song.FilePath),
                FilePath = song.FilePath,
                PlayedAt = DateTime.UtcNow,
                Status = PlaybackStatus.Error
            };
            UiDispatcher.Run(() => CommitHistoryEntry(failedItem));
            DebraDialogs.Error("Playback error", $"Failed to play: {ex.Message}");
        }
    }

    private void ApplyInputAndWindowSettings()
    {
        _gameWindow.SetTargetProcessName(TargetProcessName);
        _gameWindow.SetCustomKeywords(_settings.Settings.CustomWindowKeywords);
        _input.ConfigureMode(() => InputDeliveryMode.LocalPostMessage);
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
                "Start Where Winds Meet, open the instrument, then play.\n\nContinue anyway?",
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

    private void SetPrimaryListSelection(PrimarySelectionSource source, Song? song, CatalogueTrack? catalogueTrack = null)
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
            }
        }
        finally
        {
            _suppressExclusiveSelection = false;
        }
    }

    private void SyncListSelectionForActivePlayback(ActivePlaybackList list, Song? song = null, CatalogueTrack? catalogueTrack = null)
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
        if (SelectedFavoriteSong is not null)
            return ActivePlaybackList.Favorites;
        if (SelectedLibrarySong is not null)
            return ActivePlaybackList.Library;
        if (_lastSelectedPlaylistSong is not null)
            return ActivePlaybackList.Playlist;
        if (_lastSelectedCatalogueTrack is not null)
            return ActivePlaybackList.Catalogue;
        if (_lastSelectedFavoriteSong is not null)
            return ActivePlaybackList.Favorites;
        if (_lastSelectedLibrarySong is not null)
            return ActivePlaybackList.Library;
        return ActivePlaybackList.None;
    }

    private bool IsPlaybackHotkeyContextActive()
    {
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
        _suppressThemeChange = true;
        SelectedTheme = AvailableThemes.FirstOrDefault(t =>
                            t.Id.Equals(selectedId, StringComparison.OrdinalIgnoreCase))
                        ?? AvailableThemes[0];
        _suppressThemeChange = false;
    }

    private void ApplyUiThemeFromSettings()
    {
        ThemeService.Apply(_settings.Settings.UiTheme, persist: false);
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
        ApplySortSettingsFromSaved();
        Ui.Refresh();
        OnPropertyChanged(nameof(Ui));
        OnPropertyChanged(nameof(UiFlowDirection));
        OnPropertyChanged(nameof(ChromeAutoPlayNextText));
        OnPropertyChanged(nameof(SmartTransposeStateLabel));
        RefreshPlayPauseUi();
        NotifyPlaybackHotkeyLabels();
        RefreshIdleUiStrings();
        UpdateAllStylesLabel();

        RefreshLibraryStats();
        RefreshHistoryStats();
        RefreshPlaylistStats();
        RefreshFavoritesStats();
        RefreshCatalogueStats();
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
            return;
        }

        var wasAll = IsAllStylesFilter(CatalogueStyleFilter);
        CatalogueStyles[0] = AllStylesLabel;
        if (wasAll)
            CatalogueStyleFilter = AllStylesLabel;
    }

    private void RefreshNavLabels()
    {
        if (NavItems.Count < 5)
            return;

        NavItems[0].Label = L.T(UiText.NavLibrary);
        NavItems[1].Label = L.T(UiText.NavCatalogue);
        NavItems[2].Label = L.T(UiText.NavFavorites);
        NavItems[3].Label = L.T(UiText.NavHistory);
        NavItems[4].Label = L.T(UiText.NavSettings);
    }

    private void NotifyTrashCommandsCanExecute()
    {
        RemoveFromLibraryCommand.NotifyCanExecuteChanged();
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

    partial void OnNoteDelayMsChanged(int value)
    {
        _settings.Settings.NoteDelayMs = value;
        ScheduleSettingsSave();
    }

    partial void OnChordRollDelayMsChanged(int value)
    {
        _settings.Settings.ChordRollDelayMs = value;
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
        NotifyPlaybackTempoUi();
    }

    private void NotifyPlaybackTempoUi()
    {
        OnPropertyChanged(nameof(EffectiveTempoBpm));
        OnPropertyChanged(nameof(PlaybackTempoDisplay));
        OnPropertyChanged(nameof(IsTempoSliderEnabled));
        OnPropertyChanged(nameof(CanResetPlaybackTempo));
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
    }

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

    partial void OnPrePlayCountdownSecondsChanged(int value)
    {
        _settings.Settings.PrePlayCountdownSeconds = value;
        ScheduleSettingsSave();
    }

    partial void OnSelectedLayoutChanged(KeyLayoutOption? value)
    {
        if (value is null)
            return;
        LoadKeyMapping(value.FileName);
    }

    partial void OnSelectedSectionChanged(NavigationSection value)
    {
        _activePlaybackList = value switch
        {
            NavigationSection.Catalogue => ActivePlaybackList.Catalogue,
            NavigationSection.Library => ActivePlaybackList.Library,
            NavigationSection.Favorites => ActivePlaybackList.Favorites,
            NavigationSection.History => ActivePlaybackList.Playlist,
            _ => _activePlaybackList
        };

        UpdateNavActive();
        OnPropertyChanged(nameof(ShowMainPanels));
        OnPropertyChanged(nameof(ShowSettingsPanel));
        OnPropertyChanged(nameof(ShowLibraryPanel));
        OnPropertyChanged(nameof(ShowHistoryPanel));
        OnPropertyChanged(nameof(ShowCataloguePanel));
        OnPropertyChanged(nameof(ShowFavoritesPanel));
        OnPropertyChanged(nameof(ShowPlaylistPanel));
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
        if (_settings.Settings.WindowLeft is { } left)
            window.Left = left;
        if (_settings.Settings.WindowTop is { } top)
            window.Top = top;
        const double defaultWidth = 1024;
        const double defaultHeight = 682;
        const double designAspect = defaultWidth / defaultHeight;
        const double minWindowHeight = 640;

        var savedWidth = _settings.Settings.WindowWidth;
        var savedHeight = _settings.Settings.WindowHeight;
        if (savedWidth is > 0 and <= 1150 && savedHeight is > 0 and <= 760)
        {
            window.Width = savedWidth;
            window.Height = savedHeight;
        }
        else
        {
            window.Width = defaultWidth;
            window.Height = defaultHeight;
        }

        // Snap saved sizes to design aspect so Viewbox does not leave bottom/right bands
        var aspect = window.Width / window.Height;
        if (Math.Abs(aspect - designAspect) > 0.008)
        {
            window.Height = Math.Round(window.Width / designAspect);
            if (window.Height < minWindowHeight)
            {
                window.Height = minWindowHeight;
                window.Width = Math.Round(window.Height * designAspect);
            }
        }
    }

    private static double? SafeCoord(double value) =>
        double.IsFinite(value) ? value : null;

    public void Dispose()
    {
        _uiTimer.Stop();
        _globalHotkey.Dispose();
        _playback.Dispose();
        _systemVolume.Dispose();
        _settings.Save();
        _history.Save();
    }
}
