using System.Net.Http;
using System.Text.Json;
using WhereWindsMeetMidiPlayer.Infrastructure;
using WhereWindsMeetMidiPlayer.Models;

namespace WhereWindsMeetMidiPlayer.Services;

/// <summary>
/// Solo MIDIs from the bardmusicplayer.com repository. The index is cached locally and only
/// refreshed on explicit request; MIDI files are downloaded one by one when played.
/// </summary>
public sealed class CommunityCatalogueService
{
    private const string ApiBase = "https://bardmusicplayer.com/api/midis";
    private const int PageSize = 200;

    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        // Honest, allowlistable identity — agreed with the site owner (no browser spoofing).
        var version = typeof(CommunityCatalogueService).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"DebraMidiPlayer/{version}");
        client.DefaultRequestHeaders.UserAgent.ParseAdd("(+https://github.com/delkimo-svg)");
        return client;
    }

    public IReadOnlyList<CommunitySong> LoadIndex()
    {
        try
        {
            return JsonFileStore.Read<List<CommunitySong>>(AppPaths.CommunityIndexFile) ?? [];
        }
        catch (Exception ex)
        {
            AppPaths.WriteDiagnosticLog("community-index-load", ex);
            return [];
        }
    }

    public void SaveIndex(IReadOnlyList<CommunitySong> songs)
    {
        try
        {
            JsonFileStore.Write(AppPaths.CommunityIndexFile, songs.Where(s => s.Origin == CommunityOrigin.Bmp).ToList());
        }
        catch (Exception ex)
        {
            AppPaths.WriteDiagnosticLog("community-index-save", ex);
        }
    }

    public async Task<IReadOnlyList<CommunitySong>> FetchBmpSoloCatalogueAsync(
        IProgress<(int Page, int TotalPages)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var songs = new List<CommunitySong>();
        var first = await FetchPageAsync(1, cancellationToken).ConfigureAwait(false);
        var totalPages = first.TotalPages;
        songs.AddRange(first.Songs);
        progress?.Report((1, totalPages));

        // Modest parallelism: fast enough for ~40 pages without hammering the site.
        var pages = Enumerable.Range(2, Math.Max(0, totalPages - 1)).ToList();
        var done = 1;
        var gate = new SemaphoreSlim(4);
        var results = await Task.WhenAll(pages.Select(async page =>
        {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var result = await FetchPageAsync(page, cancellationToken).ConfigureAwait(false);
                progress?.Report((Interlocked.Increment(ref done), totalPages));
                return result.Songs;
            }
            finally
            {
                gate.Release();
            }
        })).ConfigureAwait(false);

        foreach (var pageSongs in results)
            songs.AddRange(pageSongs);

        // The API pages by newest first; keep a stable de-dup in case entries shift between pages.
        return songs
            .GroupBy(s => s.Key)
            .Select(g => g.First())
            .ToList();
    }

    private async Task<(IReadOnlyList<CommunitySong> Songs, int TotalPages)> FetchPageAsync(
        int page,
        CancellationToken cancellationToken)
    {
        var url = $"{ApiBase}?limit={PageSize}&page={page}";
        using var response = await Http.GetAsync(url, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);

        var root = doc.RootElement;
        var totalPages = ReadInt(root, "totalPages");
        var songs = new List<CommunitySong>();
        if (root.TryGetProperty("docs", out var docs) && docs.ValueKind == JsonValueKind.Array)
        {
            foreach (var entry in docs.EnumerateArray())
            {
                var song = ParseSong(entry);
                if (song is not null)
                    songs.Add(song);
            }
        }

        return (songs, Math.Max(1, totalPages));
    }

    private static CommunitySong? ParseSong(JsonElement entry)
    {
        if (!string.Equals(ReadString(entry, "ensembleSize"), "solo", StringComparison.OrdinalIgnoreCase))
            return null;

        var id = ReadInt(entry, "id");
        var url = ReadString(entry, "url");
        if (id <= 0 || string.IsNullOrWhiteSpace(url))
            return null;

        var artist = ReadString(entry, "artist").Trim();
        var sourceWork = ReadString(entry, "source").Trim();
        DateTime? createdAt = null;
        if (DateTime.TryParse(ReadString(entry, "createdAt"), out var parsed))
            createdAt = parsed.ToUniversalTime();

        return new CommunitySong
        {
            Key = $"bmp:{id}",
            Origin = CommunityOrigin.Bmp,
            Title = ReadString(entry, "title").Trim(),
            Artist = artist,
            Creator = ReadString(entry, "arranger").Trim(),
            SourceWork = sourceWork,
            Genre = CommunityGenreMap.Resolve(artist, sourceWork),
            DurationMs = ReadLong(entry, "songDurationMs"),
            Downloads = ReadInt(entry, "downloads"),
            Filename = ReadString(entry, "filename"),
            DownloadUrl = url,
            CreatedAt = createdAt
        };
    }

    /// <summary>Fetch the song's MIDI into the local community cache (no-op if already there).</summary>
    public async Task<string> DownloadToCacheAsync(CommunitySong song, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(AppPaths.CommunityCacheFolder);
        var baseName = SanitizeFileName(Path.GetFileNameWithoutExtension(
            string.IsNullOrWhiteSpace(song.Filename) ? song.Title : song.Filename));
        var targetPath = Path.Combine(AppPaths.CommunityCacheFolder, baseName + ".mid");
        if (File.Exists(targetPath) && new FileInfo(targetPath).Length > 0)
            return targetPath;

        var tempPath = targetPath + ".part";
        await using (var response = await Http.GetStreamAsync(song.DownloadUrl, cancellationToken).ConfigureAwait(false))
        await using (var file = File.Create(tempPath))
        {
            await response.CopyToAsync(file, cancellationToken).ConfigureAwait(false);
        }

        File.Move(tempPath, targetPath, overwrite: true);
        return targetPath;
    }

    private static string ReadString(JsonElement entry, string name) =>
        entry.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static int ReadInt(JsonElement entry, string name) => (int)ReadLong(entry, name);

    private static long ReadLong(JsonElement entry, string name)
    {
        if (!entry.TryGetProperty(name, out var value))
            return 0;

        // The API mixes numbers and numeric strings ("downloads":"66").
        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt64(out var n) => n,
            JsonValueKind.String when long.TryParse(value.GetString(), out var n) => n,
            _ => 0
        };
    }

    private static string SanitizeFileName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return string.IsNullOrWhiteSpace(name) ? "song" : name.Trim();
    }
}

/// <summary>Bundled artist/source → genre map for BMP songs (Assets\community-genres.json).</summary>
public static class CommunityGenreMap
{
    public const string UnknownGenre = "Other";

    private static readonly Lazy<(Dictionary<string, string> Artists, Dictionary<string, string> Sources)> Map =
        new(Load, LazyThreadSafetyMode.ExecutionAndPublication);

    private static (Dictionary<string, string>, Dictionary<string, string>) Load()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Assets", "community-genres.json");
            if (!File.Exists(path))
                return (new(StringComparer.OrdinalIgnoreCase), new(StringComparer.OrdinalIgnoreCase));

            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            return (ReadMap(doc.RootElement, "artists"), ReadMap(doc.RootElement, "sources"));
        }
        catch (Exception ex)
        {
            AppPaths.WriteDiagnosticLog("community-genres-load", ex);
            return (new(StringComparer.OrdinalIgnoreCase), new(StringComparer.OrdinalIgnoreCase));
        }
    }

    private static Dictionary<string, string> ReadMap(JsonElement root, string section)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (root.TryGetProperty(section, out var element) && element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.String)
                    map[property.Name.Trim()] = property.Value.GetString() ?? UnknownGenre;
            }
        }

        return map;
    }

    /// <summary>Artist wins; a song by an unmapped artist falls back to its source work.</summary>
    public static string Resolve(string artist, string sourceWork)
    {
        var (artists, sources) = Map.Value;
        if (!string.IsNullOrWhiteSpace(artist)
            && artists.TryGetValue(artist.Trim(), out var byArtist)
            && byArtist != UnknownGenre)
        {
            return byArtist;
        }

        if (!string.IsNullOrWhiteSpace(sourceWork) && sources.TryGetValue(sourceWork.Trim(), out var bySource))
            return bySource;

        return UnknownGenre;
    }
}
