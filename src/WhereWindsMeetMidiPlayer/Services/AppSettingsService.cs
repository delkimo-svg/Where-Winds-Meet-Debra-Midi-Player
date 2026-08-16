using WhereWindsMeetMidiPlayer.Infrastructure;
using WhereWindsMeetMidiPlayer.Models;

namespace WhereWindsMeetMidiPlayer.Services;

public sealed class AppSettingsService
{
    public AppSettings Settings { get; private set; } = new();

    public void Load()
    {
        AppPaths.EnsureCreated();
        try
        {
            Settings = JsonFileStore.Read<AppSettings>(AppPaths.SettingsFile) ?? new AppSettings();
        }
        catch (Exception ex)
        {
            // An unreadable settings file must never keep the app from starting: keep the bad
            // file for inspection and boot with defaults.
            AppPaths.WriteDiagnosticLog("settings-load", ex);
            TryBackupCorruptSettings();
            Settings = new AppSettings();
        }

        Sanitize(Settings);
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        AppPaths.EnsureCreated();
        try
        {
            Settings = await JsonFileStore.ReadAsync<AppSettings>(AppPaths.SettingsFile, cancellationToken) ?? new AppSettings();
        }
        catch (Exception ex)
        {
            AppPaths.WriteDiagnosticLog("settings-load", ex);
            TryBackupCorruptSettings();
            Settings = new AppSettings();
        }

        Sanitize(Settings);
    }

    private static void TryBackupCorruptSettings()
    {
        try
        {
            if (File.Exists(AppPaths.SettingsFile))
                File.Copy(AppPaths.SettingsFile, AppPaths.SettingsFile + ".bad", overwrite: true);
        }
        catch
        {
            // Best effort — defaults still load.
        }
    }

    public void Save()
    {
        AppPaths.EnsureCreated();
        StripDiscordFieldsFromSettings();
        Sanitize(Settings);
        JsonFileStore.Write(AppPaths.SettingsFile, Settings);
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        AppPaths.EnsureCreated();
        StripDiscordFieldsFromSettings();
        Sanitize(Settings);
        await JsonFileStore.WriteAsync(AppPaths.SettingsFile, Settings, cancellationToken);
    }

    private void StripDiscordFieldsFromSettings()
    {
        Settings.DiscordBotToken = null;
        Settings.DiscordGuildId = null;
        Settings.DiscordCategoryChannelId = null;
        Settings.DiscordCategoryName = null;
    }

    internal static void Sanitize(AppSettings settings)
    {
        settings.WindowLeft = SanitizeWindowCoord(settings.WindowLeft);
        settings.WindowTop = SanitizeWindowCoord(settings.WindowTop);
        settings.WindowWidth = SanitizeWindowSize(settings.WindowWidth, 1024);
        settings.WindowHeight = SanitizeWindowSize(settings.WindowHeight, 682);
    }

    private static double? SanitizeWindowCoord(double? value) =>
        value is null or double.NaN or double.PositiveInfinity or double.NegativeInfinity ? null : value;

    private static double SanitizeWindowSize(double value, double fallback) =>
        value is > 0 and < 10000 and not double.NaN and not double.PositiveInfinity and not double.NegativeInfinity
            ? value
            : fallback;
}
