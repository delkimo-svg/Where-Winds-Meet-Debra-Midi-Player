namespace WhereWindsMeetMidiPlayer.Infrastructure;

public static class AppPaths
{
    public static string AppDataRoot =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WhereWindsMeetMidiPlayer");

    public static string SettingsFile => Path.Combine(AppDataRoot, "settings.json");
    public static string HistoryFile => Path.Combine(AppDataRoot, "history.json");
    public static string PlaylistsFolder => Path.Combine(AppDataRoot, "playlists");
    public static string KeyMapsFolder => Path.Combine(AppDataRoot, "keymaps");
    public static string CatalogueCacheFolder => Path.Combine(AppDataRoot, "catalogue-cache");
    public static string CatalogueIndexFile => Path.Combine(AppDataRoot, "catalogue-index.json");
    public static string SongTempoFile => Path.Combine(AppDataRoot, "song-tempo.json");
    public static string DiscordCredentialsFile => Path.Combine(AppDataRoot, "discord-credentials.dat");

    public static void EnsureCreated()
    {
        Directory.CreateDirectory(AppDataRoot);
        Directory.CreateDirectory(PlaylistsFolder);
        Directory.CreateDirectory(KeyMapsFolder);
        Directory.CreateDirectory(CatalogueCacheFolder);
    }

    public static void WriteDiagnosticLog(string tag, Exception ex)
    {
        try
        {
            EnsureCreated();
            var path = Path.Combine(AppDataRoot, "crash.log");
            File.AppendAllText(path, $"[{DateTime.UtcNow:u}] {tag}\n{ex}\n\n");
        }
        catch
        {
            // ignored
        }
    }
}
