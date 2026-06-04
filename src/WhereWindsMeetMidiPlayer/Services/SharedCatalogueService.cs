using System.Net.Http;
using System.Text;
using System.Text.Json;
using WhereWindsMeetMidiPlayer.Helpers;
using WhereWindsMeetMidiPlayer.Infrastructure;
using WhereWindsMeetMidiPlayer.Models;
using WhereWindsMeetMidiPlayer.Services.Discord;

namespace WhereWindsMeetMidiPlayer.Services;

public sealed class SharedCatalogueService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _http = new();

    public static string SharedManifestFileName => "shared-catalogue.json";
    public static string CataloguePackFolderName => "catalogue-pack";
    public static string CatalogueConfigFileName => "catalogue-config.json";

    public string AppBaseDirectory => AppContext.BaseDirectory;

    public string BundledManifestPath => Path.Combine(AppBaseDirectory, SharedManifestFileName);

    public string BundledManifestAssetsPath => Path.Combine(AppBaseDirectory, "Assets", SharedManifestFileName);

    public string CataloguePackRoot => Path.Combine(AppBaseDirectory, CataloguePackFolderName);

    public string CachedRemoteManifestPath => Path.Combine(AppPaths.AppDataRoot, "shared-catalogue-remote.json");

    public CatalogueConfig? LoadConfig()
    {
        var path = Path.Combine(AppBaseDirectory, "Assets", CatalogueConfigFileName);
        if (!File.Exists(path))
            return null;

        try
        {
            var json = File.ReadAllText(path, Encoding.UTF8);
            return JsonSerializer.Deserialize<CatalogueConfig>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public IReadOnlyList<CatalogueTrack> LoadBundledManifest()
    {
        foreach (var path in new[] { BundledManifestPath, BundledManifestAssetsPath })
        {
            var tracks = TryLoadManifestFile(path);
            if (tracks.Count > 0)
                return tracks;
        }

        return [];
    }

    public IReadOnlyList<CatalogueTrack> LoadCachedRemoteManifest()
    {
        if (!File.Exists(CachedRemoteManifestPath))
            return [];

        return TryLoadManifestFile(CachedRemoteManifestPath);
    }

    public async Task<IReadOnlyList<CatalogueTrack>> FetchManifestFromUrlAsync(string url, CancellationToken cancellationToken = default)
    {
        var json = await _http.GetStringAsync(url, cancellationToken).ConfigureAwait(false);
        var manifest = JsonSerializer.Deserialize<SharedCatalogueManifest>(json, JsonOptions)
                       ?? throw new InvalidOperationException("Invalid shared catalogue manifest.");

        var tracks = PrepareTracks(manifest.Tracks);
        AppPaths.EnsureCreated();
        File.WriteAllText(CachedRemoteManifestPath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return tracks;
    }

    public void SaveBundledManifest(SharedCatalogueManifest manifest, string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        manifest.UpdatedAt = DateTime.UtcNow;
        var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(Path.Combine(outputDirectory, SharedManifestFileName), json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    public static string? ResolveLocalMidiPath(CatalogueTrack track, string appBaseDirectory)
    {
        if (!string.IsNullOrWhiteSpace(track.CachedFilePath) && File.Exists(track.CachedFilePath))
            return track.CachedFilePath;

        if (string.IsNullOrWhiteSpace(track.BundledMidiPath))
            return null;

        var relative = track.BundledMidiPath.Replace('/', Path.DirectorySeparatorChar);
        var combined = Path.Combine(appBaseDirectory, relative);
        return File.Exists(combined) ? Path.GetFullPath(combined) : null;
    }

    private List<CatalogueTrack> TryLoadManifestFile(string path)
    {
        if (!File.Exists(path))
            return [];

        try
        {
            var json = File.ReadAllText(path, Encoding.UTF8);
            var manifest = JsonSerializer.Deserialize<SharedCatalogueManifest>(json, JsonOptions);
            if (manifest?.Tracks is { Count: > 0 })
                return PrepareTracks(manifest.Tracks);

            var tracksOnly = JsonSerializer.Deserialize<List<CatalogueTrack>>(json, JsonOptions);
            return tracksOnly is null ? [] : PrepareTracks(tracksOnly);
        }
        catch
        {
            return [];
        }
    }

    private List<CatalogueTrack> PrepareTracks(IEnumerable<CatalogueTrack> tracks)
    {
        return tracks
            .Select(t =>
            {
                var local = ResolveLocalMidiPath(t, AppBaseDirectory);
                if (local is not null)
                    t.CachedFilePath = local;

                MidiFileNameTitleHelper.RefreshCatalogueTrackTitle(t);
                if (t.StyleSortOrder == 0)
                    t.StyleSortOrder = DiscordCatalogueOrder.GetSortIndex(t.StyleName);
                return t;
            })
            .OrderBy(t => t.StyleSortOrder)
            .ThenBy(t => t.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
