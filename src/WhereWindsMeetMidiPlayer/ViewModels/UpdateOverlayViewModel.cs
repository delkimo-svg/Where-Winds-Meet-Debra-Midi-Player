using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WhereWindsMeetMidiPlayer.Infrastructure;
using WhereWindsMeetMidiPlayer.Localization;
using WhereWindsMeetMidiPlayer.Models;
using WhereWindsMeetMidiPlayer.Services;

namespace WhereWindsMeetMidiPlayer.ViewModels;

public partial class UpdateOverlayViewModel : ObservableObject
{
    private readonly AppUpdateService _updates;
    private readonly Action<string?> _onDismissed;
    private readonly Action _onClose;

    public ReleaseManifest Manifest { get; }

    [ObservableProperty] private string _statusText = string.Empty;
    [ObservableProperty] private double _downloadProgress;
    [ObservableProperty] private bool _isDownloading;
    [ObservableProperty] private bool _downloadComplete;

    public bool CanDownload => !IsDownloading;

    public string TitleText => L.F(UiText.UpdateOverlayTitle, Manifest.Version);
    public string SubtitleText => L.T(UiText.UpdateOverlaySubtitle);
    public string ReleaseNotes => Manifest.ReleaseNotes ?? string.Empty;
    public string ExtractHintText => L.T(UiText.UpdateOverlayExtractHint);
    public string DownloadLabel => L.T(UiText.UpdateOverlayDownload);
    public string LaterLabel => L.T(UiText.UpdateOverlayLater);
    public string OpenFolderLabel => L.T(UiText.UpdateOverlayOpenFolder);
    public string CloseLabel => L.T(UiText.UpdateOverlayClose);

    public UpdateOverlayViewModel(
        ReleaseManifest manifest,
        AppUpdateService updates,
        Action<string?> onDismissed,
        Action onClose)
    {
        Manifest = manifest;
        _updates = updates;
        _onDismissed = onDismissed;
        _onClose = onClose;
        StatusText = L.T(UiText.UpdateOverlayReady);
    }

    [RelayCommand]
    private async Task Download()
    {
        if (IsDownloading)
            return;

        try
        {
            IsDownloading = true;
            DownloadComplete = false;
            StatusText = L.T(UiText.UpdateOverlayDownloading);

            var path = await _updates.DownloadPortableArchiveAsync(
                Manifest,
                new Progress<double>(p => DownloadProgress = p * 100));

            DownloadComplete = true;
            StatusText = L.F(UiText.UpdateOverlaySaved, Path.GetFileName(path));
        }
        catch (Exception ex)
        {
            StatusText = L.F(UiText.UpdateOverlayFailed, ex.Message);
        }
        finally
        {
            IsDownloading = false;
            OnPropertyChanged(nameof(CanDownload));
        }
    }

    partial void OnIsDownloadingChanged(bool value) => OnPropertyChanged(nameof(CanDownload));

    [RelayCommand]
    private void OpenFolder()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = _updates.InstallDirectory,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
        }
    }

    [RelayCommand]
    private void RemindLater()
    {
        _onDismissed(Manifest.Version);
        _onClose();
    }

    [RelayCommand]
    private void Close() => _onClose();
}
