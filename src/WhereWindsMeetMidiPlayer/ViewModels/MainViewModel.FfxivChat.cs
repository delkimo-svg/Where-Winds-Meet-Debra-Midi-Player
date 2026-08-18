using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WhereWindsMeetMidiPlayer.Helpers;
using WhereWindsMeetMidiPlayer.Infrastructure;
using WhereWindsMeetMidiPlayer.Localization;
using WhereWindsMeetMidiPlayer.Services;

namespace WhereWindsMeetMidiPlayer.ViewModels;

/// <summary>A chat channel choice in the FFXIV Chat panel (labels are game terms, not localized).</summary>
public sealed record FfxivChatChannelOption(string Label, int Code);

/// <summary>An entry of the FFXIV instrument picker; Id 0 = auto-detect from track names.</summary>
public sealed record FfxivInstrumentOption(uint Id, string Label);

/// <summary>
/// FFXIV Chat panel: sends in-game chat through the Hypnotoad Dalamud plugin, so messages
/// land without opening the game's input box — playback never pauses or drops notes.
/// </summary>
public partial class MainViewModel
{
    // FFXIV chat is capped at 500 bytes; stay far below so channel shortcuts always fit.
    private const int FfxivChatMaxLength = 450;
    private const string HypnotoadPluginUrl = "https://github.com/GiR-Zippo/Hypnotoad-Plugin";

    private readonly HypnotoadService _hypnotoad = new();
    private DispatcherTimer? _hypnotoadRetryTimer;
    private bool _suppressFfxivChatPersist;

    [ObservableProperty] private bool _isFfxivChatExpanded;
    [ObservableProperty] private string _ffxivChatMessageText = string.Empty;
    [ObservableProperty] private string _ffxivChatAnnounceTemplate = string.Empty;
    [ObservableProperty] private bool _ffxivChatAutoAnnounce;
    [ObservableProperty] private FfxivChatChannelOption? _selectedFfxivChatChannel;
    [ObservableProperty] private FfxivChatChannelOption? _selectedFfxivAnnounceChannel;
    [ObservableProperty] private string _ffxivChatStatusText = string.Empty;
    [ObservableProperty] private bool _isHypnotoadConnected;
    [ObservableProperty] private bool _isHypnotoadMissing = true;
    [ObservableProperty] private bool _ffxivNotesDirect = true;
    [ObservableProperty] private string _ffxivInstrumentName = string.Empty;
    [ObservableProperty] private bool _ffxivAutoOpenInstrument = true;
    [ObservableProperty] private FfxivInstrumentOption? _selectedFfxivInstrument;
    [ObservableProperty] private FfxivInstrumentOption? _selectedFfxivDefaultInstrument;
    [ObservableProperty] private string _ffxivGuitarToneKeys = "1,2,3,4,5";

    private uint _ffxivInstrumentId;
    private uint _lastOpenedInstrumentId;
    private bool _suppressFfxivInstrumentPersist;

    public ObservableCollection<FfxivInstrumentOption> FfxivInstrumentOptions { get; } = [];
    /// <summary>Settings picker for what "Auto" equips (no Auto entry).</summary>
    public ObservableCollection<FfxivInstrumentOption> FfxivDefaultInstrumentOptions { get; } = [];

    public ObservableCollection<FfxivChatChannelOption> FfxivChatChannels { get; } = [];
    public ObservableCollection<FfxivChatChannelOption> FfxivAnnounceChannels { get; } = [];

    public bool IsFfxivChatAvailable => SelectedGameProfile == GameProfiles.FinalFantasyXiv;

    /// <summary>Panel shows only in FFXIV mode and only alongside the player chrome.</summary>
    public bool IsFfxivChatVisible => IsFfxivChatAvailable && ShowDebraPlayerChrome;

    private void InitializeFfxivChat()
    {
        _suppressFfxivInstrumentPersist = true;
        try
        {
            FfxivInstrumentOptions.Add(new FfxivInstrumentOption(0, L.T(UiText.FfxivInstrumentAuto)));
            foreach (var instrument in FfxivInstrumentResolver.All)
            {
                FfxivInstrumentOptions.Add(new FfxivInstrumentOption(instrument.Id, instrument.Name));
                FfxivDefaultInstrumentOptions.Add(new FfxivInstrumentOption(instrument.Id, instrument.Name));
            }

            SelectedFfxivInstrument = FfxivInstrumentOptions[0];
            var defaultId = (uint)_settings.Settings.FfxivDefaultInstrumentId;
            SelectedFfxivDefaultInstrument =
                FfxivDefaultInstrumentOptions.FirstOrDefault(o => o.Id == defaultId)
                ?? FfxivDefaultInstrumentOptions[0];

            FfxivGuitarToneKeys = string.IsNullOrWhiteSpace(_settings.Settings.FfxivGuitarToneKeys)
                ? "1,2,3,4,5"
                : _settings.Settings.FfxivGuitarToneKeys;
            ApplyGuitarToneKeys(FfxivGuitarToneKeys);
        }
        finally
        {
            _suppressFfxivInstrumentPersist = false;
        }

        foreach (var option in new FfxivChatChannelOption[]
        {
            new("Say", HypnotoadChatChannel.Say),
            new("Yell", HypnotoadChatChannel.Yell),
            new("Shout", HypnotoadChatChannel.Shout),
            new("Party", HypnotoadChatChannel.Party),
            new("FC", HypnotoadChatChannel.FreeCompany)
        })
        {
            FfxivChatChannels.Add(option);
            FfxivAnnounceChannels.Add(option);
        }

        _suppressFfxivChatPersist = true;
        try
        {
            var s = _settings.Settings;
            IsFfxivChatExpanded = s.FfxivChatPanelExpanded;
            FfxivChatAnnounceTemplate = string.IsNullOrWhiteSpace(s.FfxivChatAnnounceTemplate)
                ? "♪ Now playing: {title} ♪"
                : s.FfxivChatAnnounceTemplate;
            FfxivChatAutoAnnounce = s.FfxivChatAutoAnnounce;
            FfxivNotesDirect = s.FfxivNotesViaHypnotoad;
            FfxivAutoOpenInstrument = s.FfxivAutoOpenInstrument;
            SelectedFfxivChatChannel = FfxivChatChannels.FirstOrDefault(c => c.Code == s.FfxivChatChannelCode)
                ?? FfxivChatChannels[0];
            SelectedFfxivAnnounceChannel = FfxivAnnounceChannels.FirstOrDefault(c => c.Code == s.FfxivChatAnnounceChannelCode)
                ?? FfxivAnnounceChannels[1];
        }
        finally
        {
            _suppressFfxivChatPersist = false;
        }

        _hypnotoad.StateChanged += (_, _) => UiDispatcher.Post(RefreshHypnotoadStatus);

        // Re-acquire the pipe if it was busy at startup (e.g. LightAmp closed since).
        _hypnotoadRetryTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _hypnotoadRetryTimer.Tick += (_, _) =>
        {
            if (IsFfxivChatAvailable && !_hypnotoad.IsRunning)
                _ = _hypnotoad.StartAsync();
        };
        _hypnotoadRetryTimer.Start();

        UpdateFfxivChatForGame();
    }

    /// <summary>Shows/hides the panel and owns/releases the "Hypnotoad" pipe as the game switches.</summary>
    private void UpdateFfxivChatForGame()
    {
        OnPropertyChanged(nameof(IsFfxivChatAvailable));
        OnPropertyChanged(nameof(IsFfxivChatVisible));
        if (IsFfxivChatAvailable)
            _ = _hypnotoad.StartAsync();
        else
            _ = _hypnotoad.StopAsync();

        _lastOpenedInstrumentId = 0;
        UpdateFfxivInstrumentSuggestion();
        RefreshHypnotoadStatus();
    }

    /// <summary>
    /// Recomputes the effective FFXIV instrument. A manual pick (saved per song) wins;
    /// otherwise a recognizable unmuted track name decides ("Auto — <recommended>"), and
    /// with nothing recognizable "Auto" falls back to the default instrument from Settings —
    /// slot 0 then shows just its name (e.g. "Harp"). Empty outside FFXIV mode or with
    /// nothing loaded — that hides the picker.
    /// </summary>
    private void UpdateFfxivInstrumentSuggestion()
    {
        if (!IsFfxivChatAvailable || PlaybackTrackMixItems.Count == 0)
        {
            _ffxivInstrumentId = 0;
            FfxivInstrumentName = string.Empty;
            return;
        }

        var recommended = FfxivInstrumentResolver.ResolveOrNull(
            PlaybackTrackMixItems.Where(t => !t.IsMuted).Select(t => t.DisplayName));
        var fallback = recommended
            ?? FfxivInstrumentResolver.FromId((uint)_settings.Settings.FfxivDefaultInstrumentId);

        var wasAuto = SelectedFfxivInstrument is null or { Id: 0 };
        if (FfxivInstrumentOptions.Count > 0)
        {
            var autoOption = new FfxivInstrumentOption(0, recommended is not null
                ? $"{L.T(UiText.FfxivInstrumentAuto)} — {recommended.Name}"
                : fallback.Name);
            _suppressFfxivInstrumentPersist = true;
            try
            {
                FfxivInstrumentOptions[0] = autoOption;
                if (wasAuto)
                    SelectedFfxivInstrument = autoOption;
            }
            finally
            {
                _suppressFfxivInstrumentPersist = false;
            }
        }

        var effective = wasAuto ? fallback : FfxivInstrumentResolver.FromId(SelectedFfxivInstrument!.Id);
        _ffxivInstrumentId = effective.Id;
        FfxivInstrumentName = effective.Name;
    }

    private void ApplyGuitarToneKeys(string value) =>
        _playback.GuitarToneKeyCombos = (value ?? string.Empty)
            .Split(',')
            .Select(s => s.Trim())
            .ToArray();

    partial void OnFfxivGuitarToneKeysChanged(string value)
    {
        ApplyGuitarToneKeys(value);
        if (_suppressFfxivInstrumentPersist)
            return;

        _settings.Settings.FfxivGuitarToneKeys = value;
        ScheduleSettingsSave();
    }

    partial void OnSelectedFfxivDefaultInstrumentChanged(FfxivInstrumentOption? value)
    {
        if (value is null || _suppressFfxivInstrumentPersist)
            return;

        _settings.Settings.FfxivDefaultInstrumentId = (int)value.Id;
        ScheduleSettingsSave();
        UpdateFfxivInstrumentSuggestion();
    }

    /// <summary>Restores the per-song instrument pick when a song loads (0 = auto).</summary>
    private void SelectFfxivInstrumentFromCalibration(int instrumentId)
    {
        if (FfxivInstrumentOptions.Count == 0)
            return;

        _suppressFfxivInstrumentPersist = true;
        try
        {
            SelectedFfxivInstrument = instrumentId > 0
                ? FfxivInstrumentOptions.FirstOrDefault(o => o.Id == (uint)instrumentId) ?? FfxivInstrumentOptions[0]
                : FfxivInstrumentOptions[0];
        }
        finally
        {
            _suppressFfxivInstrumentPersist = false;
        }
    }

    partial void OnSelectedFfxivInstrumentChanged(FfxivInstrumentOption? value)
    {
        if (value is null || _suppressFfxivInstrumentPersist)
            return;

        UpdateFfxivInstrumentSuggestion();

        if (_suppressPlaybackCalibrationChange)
            return;

        // Browsing the list only selects and saves — the instrument is equipped when Play
        // starts the song. The one exception: switching mid-performance pauses, re-equips
        // and resumes so the new pick is heard right away.
        SavePlaybackCalibration();
        if (_playback.State == PlaybackState.Playing)
            _ = SwitchInstrumentDuringPlaybackAsync();
    }

    private bool _ffxivInstrumentSwitchInFlight;

    /// <summary>Mid-song instrument change: pause, equip the new pick, resume where we were.</summary>
    private async Task SwitchInstrumentDuringPlaybackAsync()
    {
        if (_ffxivInstrumentSwitchInFlight || !FfxivAutoOpenInstrument
            || !_hypnotoad.IsClientConnected || _ffxivInstrumentId == 0)
            return;

        _ffxivInstrumentSwitchInFlight = true;
        try
        {
            _playback.Pause();
            IsPlaying = false;
            RefreshPlayPauseUi();

            // Quits the current performance, equips the new pick, then lets the UI settle.
            if (await EquipInstrumentAsync(_ffxivInstrumentId))
                await Task.Delay(2000);

            ResumePlayback();
            IsPlaying = true;
            UpdatePlaybackStatus();
        }
        finally
        {
            _ffxivInstrumentSwitchInFlight = false;
        }
    }

    /// <summary>
    /// Equips the detected instrument through Hypnotoad before the first notes fly.
    /// Waits for the game's performance UI to open — a longer settle when the instrument
    /// changed, a short one when re-sending the same id (cheap no-op if already equipped,
    /// reopens the UI if the player closed it). Without the plugin this is a no-op; the
    /// chip in the FFXIV panel still tells the player which instrument to pick manually.
    /// </summary>
    private async Task MaybeOpenFfxivInstrumentAsync()
    {
        if (!IsFfxivChatAvailable || !FfxivAutoOpenInstrument
            || _ffxivInstrumentId == 0 || !_hypnotoad.IsClientConnected)
            return;

        var settleMs = _ffxivInstrumentId == _lastOpenedInstrumentId ? 800 : 2000;
        if (!await EquipInstrumentAsync(_ffxivInstrumentId).ConfigureAwait(false))
            return;

        await Task.Delay(settleMs).ConfigureAwait(false);
    }

    /// <summary>
    /// Equips an instrument through Hypnotoad. The game ignores an instrument change while
    /// already performing, so when we know another instrument is open we quit performance
    /// first (instrument 0) and give the UI a beat to close before opening the new one.
    /// </summary>
    private async Task<bool> EquipInstrumentAsync(uint instrumentId)
    {
        if (_lastOpenedInstrumentId != 0 && _lastOpenedInstrumentId != instrumentId)
        {
            if (!await _hypnotoad.OpenInstrumentAsync(0).ConfigureAwait(false))
                return false;

            await Task.Delay(900).ConfigureAwait(false);
        }

        if (!await _hypnotoad.OpenInstrumentAsync(instrumentId).ConfigureAwait(false))
            return false;

        _lastOpenedInstrumentId = instrumentId;
        return true;
    }

    /// <summary>Opens the Hypnotoad plugin page (install instructions + custom Dalamud repo URL).</summary>
    [RelayCommand]
    private void OpenHypnotoadPage()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(HypnotoadPluginUrl)
            {
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            AppPaths.WriteDiagnosticLog("hypnotoad-open-url", ex);
        }
    }

    /// <summary>Routes playback and live-MIDI notes through Hypnotoad when connected and enabled.</summary>
    private void UpdateDirectNoteRouting()
    {
        var active = IsFfxivChatAvailable && FfxivNotesDirect && _hypnotoad.IsClientConnected;
        Func<int, bool, bool>? sink = active ? (note, on) => _hypnotoad.SendNote(note, on) : null;
        _playback.DirectNoteSink = sink;
        _liveMidi.DirectNoteSink = sink;
        // Guitar tone changes ride the same route; without the plugin the engine falls back
        // to the configured Tone keybinds.
        _playback.GuitarToneSink = active ? tone => _hypnotoad.SendProgramChange(tone) : null;
    }

    private void RefreshHypnotoadStatus()
    {
        IsHypnotoadConnected = _hypnotoad.IsClientConnected;
        IsHypnotoadMissing = !_hypnotoad.IsClientConnected;
        if (!_hypnotoad.IsClientConnected)
            _lastOpenedInstrumentId = 0;
        UpdateDirectNoteRouting();
        if (_hypnotoad.IsClientConnected)
        {
            FfxivChatStatusText = string.IsNullOrEmpty(_hypnotoad.CharacterName)
                ? L.T(UiText.FfxivChatStatusConnected)
                : $"{L.T(UiText.FfxivChatStatusConnected)} — {_hypnotoad.CharacterName}";
        }
        else if (!_hypnotoad.IsRunning && !string.IsNullOrEmpty(_hypnotoad.LastError))
        {
            FfxivChatStatusText = L.T(UiText.FfxivChatStatusPipeBusy);
        }
        else
        {
            FfxivChatStatusText = L.T(UiText.FfxivChatStatusWaiting);
        }
    }

    [RelayCommand]
    private async Task SendFfxivChat()
    {
        var text = FfxivChatMessageText?.Trim() ?? string.Empty;
        if (text.Length == 0)
            return;

        // A leading slash is a game command (/r, /sit, /em …) — deliver raw so the game parses it.
        var channel = text.StartsWith('/')
            ? HypnotoadChatChannel.Command
            : SelectedFfxivChatChannel?.Code ?? HypnotoadChatChannel.Say;

        if (await SendFfxivChatTextAsync(channel, text).ConfigureAwait(true))
            FfxivChatMessageText = string.Empty;
    }

    /// <summary>Sends the box content as a reply to the last received /tell (the game's /r).</summary>
    [RelayCommand]
    private async Task SendFfxivReply()
    {
        var text = FfxivChatMessageText?.Trim() ?? string.Empty;
        if (text.Length == 0)
            return;

        if (!text.StartsWith('/'))
            text = "/r " + text;

        if (await SendFfxivChatTextAsync(HypnotoadChatChannel.Command, text).ConfigureAwait(true))
            FfxivChatMessageText = string.Empty;
    }

    [RelayCommand]
    private async Task AnnounceNowPlaying()
    {
        if (_nowPlaying is null || string.IsNullOrWhiteSpace(NowPlayingTitle))
        {
            FfxivChatStatusText = L.T(UiText.FfxivChatNoSong);
            return;
        }

        var text = (FfxivChatAnnounceTemplate ?? string.Empty)
            .Replace("{title}", NowPlayingTitle)
            .Replace("{duration}", NowPlayingDurationDisplay)
            .Trim();
        if (text.Length == 0)
            return;

        await SendFfxivChatTextAsync(
            SelectedFfxivAnnounceChannel?.Code ?? HypnotoadChatChannel.Yell, text).ConfigureAwait(true);
    }

    /// <summary>Called when playback starts — announces the song if auto-announce is on.</summary>
    private void AutoAnnounceNowPlayingIfEnabled()
    {
        if (IsFfxivChatAvailable && FfxivChatAutoAnnounce && _hypnotoad.IsClientConnected)
            _ = AnnounceNowPlaying();
    }

    private async Task<bool> SendFfxivChatTextAsync(int channel, string text)
    {
        if (text.Length > FfxivChatMaxLength)
            text = text[..FfxivChatMaxLength];

        var sent = await _hypnotoad.SendChatAsync(channel, text).ConfigureAwait(true);
        RefreshHypnotoadStatus();
        return sent;
    }

    partial void OnIsFfxivChatExpandedChanged(bool value)
    {
        if (_suppressFfxivChatPersist)
            return;

        _settings.Settings.FfxivChatPanelExpanded = value;
        ScheduleSettingsSave();
    }

    partial void OnFfxivNotesDirectChanged(bool value)
    {
        UpdateDirectNoteRouting();
        if (_suppressFfxivChatPersist)
            return;

        _settings.Settings.FfxivNotesViaHypnotoad = value;
        ScheduleSettingsSave();
    }

    partial void OnFfxivAutoOpenInstrumentChanged(bool value)
    {
        if (_suppressFfxivChatPersist)
            return;

        _settings.Settings.FfxivAutoOpenInstrument = value;
        ScheduleSettingsSave();
    }

    partial void OnFfxivChatAutoAnnounceChanged(bool value)
    {
        if (_suppressFfxivChatPersist)
            return;

        _settings.Settings.FfxivChatAutoAnnounce = value;
        ScheduleSettingsSave();
    }

    partial void OnFfxivChatAnnounceTemplateChanged(string value)
    {
        if (_suppressFfxivChatPersist)
            return;

        _settings.Settings.FfxivChatAnnounceTemplate = value;
        ScheduleSettingsSave();
    }

    partial void OnSelectedFfxivChatChannelChanged(FfxivChatChannelOption? value)
    {
        if (_suppressFfxivChatPersist || value is null)
            return;

        _settings.Settings.FfxivChatChannelCode = value.Code;
        ScheduleSettingsSave();
    }

    partial void OnSelectedFfxivAnnounceChannelChanged(FfxivChatChannelOption? value)
    {
        if (_suppressFfxivChatPersist || value is null)
            return;

        _settings.Settings.FfxivChatAnnounceChannelCode = value.Code;
        ScheduleSettingsSave();
    }
}
