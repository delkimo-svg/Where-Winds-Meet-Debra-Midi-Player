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

    public ObservableCollection<FfxivChatChannelOption> FfxivChatChannels { get; } = [];
    public ObservableCollection<FfxivChatChannelOption> FfxivAnnounceChannels { get; } = [];

    public bool IsFfxivChatAvailable => SelectedGameProfile == GameProfiles.FinalFantasyXiv;

    /// <summary>Panel shows only in FFXIV mode and only alongside the player chrome.</summary>
    public bool IsFfxivChatVisible => IsFfxivChatAvailable && ShowDebraPlayerChrome;

    private void InitializeFfxivChat()
    {
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

        RefreshHypnotoadStatus();
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

    private void RefreshHypnotoadStatus()
    {
        IsHypnotoadConnected = _hypnotoad.IsClientConnected;
        IsHypnotoadMissing = !_hypnotoad.IsClientConnected;
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
