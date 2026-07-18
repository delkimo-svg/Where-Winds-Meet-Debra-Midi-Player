using WhereWindsMeetMidiPlayer.Helpers;
using WhereWindsMeetMidiPlayer.Infrastructure;
using WhereWindsMeetMidiPlayer.Models;

namespace WhereWindsMeetMidiPlayer.Services;

public sealed class PlaylistService
{
    private readonly MidiParserService _midiParser;
    private readonly NoteRangeService _noteRange;
    private readonly SongMetadataCacheService? _metadataCache;

    public PlaylistService(MidiParserService midiParser, NoteRangeService noteRange,
        SongMetadataCacheService? metadataCache = null)
    {
        _midiParser = midiParser;
        _noteRange = noteRange;
        _metadataCache = metadataCache;
    }

    public Playlist CreatePlaylist(string name) => new()
    {
        Name = name,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    public void AddSong(Playlist playlist, Song song)
    {
        if (playlist.Songs.Any(s => s.FilePath.Equals(song.FilePath, StringComparison.OrdinalIgnoreCase)))
            return;

        EnsureAddedAt(song);
        playlist.Songs.Add(song);
        playlist.UpdatedAt = DateTime.UtcNow;
    }

    public void RemoveSong(Playlist playlist, string songId)
    {
        playlist.Songs.RemoveAll(s => s.Id == songId);
        playlist.UpdatedAt = DateTime.UtcNow;
    }

    public void MoveSong(Playlist playlist, int oldIndex, int newIndex)
    {
        if (oldIndex < 0 || oldIndex >= playlist.Songs.Count)
            return;

        newIndex = Math.Clamp(newIndex, 0, playlist.Songs.Count - 1);
        var song = playlist.Songs[oldIndex];
        playlist.Songs.RemoveAt(oldIndex);
        playlist.Songs.Insert(newIndex, song);
        playlist.UpdatedAt = DateTime.UtcNow;
    }

    public void InsertSong(Playlist playlist, Song song, int index)
    {
        if (playlist.Songs.Any(s => s.FilePath.Equals(song.FilePath, StringComparison.OrdinalIgnoreCase)))
            return;

        index = Math.Clamp(index, 0, playlist.Songs.Count);
        EnsureAddedAt(song);
        playlist.Songs.Insert(index, song);
        playlist.UpdatedAt = DateTime.UtcNow;
    }

    public void MoveSongToIndex(Playlist playlist, int fromIndex, int toIndex)
    {
        if (fromIndex < 0 || fromIndex >= playlist.Songs.Count)
            return;

        if (fromIndex == toIndex)
            return;

        var song = playlist.Songs[fromIndex];
        playlist.Songs.RemoveAt(fromIndex);
        toIndex = Math.Clamp(toIndex, 0, playlist.Songs.Count);
        if (toIndex > fromIndex)
            toIndex--;
        playlist.Songs.Insert(toIndex, song);
        playlist.UpdatedAt = DateTime.UtcNow;
    }

    public async Task SaveAsync(Playlist playlist, string path, CancellationToken cancellationToken = default)
    {
        playlist.UpdatedAt = DateTime.UtcNow;
        await JsonFileStore.WriteAsync(path, playlist, cancellationToken);
    }

    public async Task<Playlist> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        var playlist = await JsonFileStore.ReadAsync<Playlist>(path, cancellationToken)
            ?? throw new InvalidOperationException("Playlist file is empty or invalid.");
        ValidateMissingFiles(playlist);
        return playlist;
    }

    public Playlist Load(string path)
    {
        var playlist = JsonFileStore.Read<Playlist>(path)
            ?? throw new InvalidOperationException("Playlist file is empty or invalid.");
        ValidateMissingFiles(playlist);
        return playlist;
    }

    public void ValidateMissingFiles(Playlist playlist)
    {
        foreach (var song in playlist.Songs)
            NormalizeSongForPlaylistDisplay(song);
    }

    /// <summary>Fast path for playlist open: no full MIDI parse (titles/durations come from JSON or file name).</summary>
    public void NormalizeSongForPlaylistDisplay(Song song)
    {
        if (string.IsNullOrWhiteSpace(song.FilePath))
            return;

        if (song.AddedAt == default && File.Exists(song.FilePath))
            song.AddedAt = File.GetCreationTimeUtc(song.FilePath);

        if (!File.Exists(song.FilePath))
        {
            var baseTitle = string.IsNullOrWhiteSpace(song.Title)
                ? Path.GetFileName(song.FilePath)
                : song.Title;
            song.Title = baseTitle.Contains("(missing)", StringComparison.OrdinalIgnoreCase)
                ? baseTitle
                : $"{baseTitle} (missing)";
            return;
        }

        if (MidiFileNameTitleHelper.IsInformative(song.Title))
            return;

        var fromFileName = MidiFileNameTitleHelper.FromFilePath(song.FilePath);
        if (MidiFileNameTitleHelper.IsInformative(fromFileName) &&
            !MidiFileNameTitleHelper.LooksTruncatedFileName(fromFileName))
        {
            song.Title = CatalogueTitleHelper.GetDisplayTitle(fromFileName, song.FilePath);
            return;
        }

        if (!string.IsNullOrWhiteSpace(song.Title))
            return;

        song.Title = CatalogueTitleHelper.GetDisplayTitle(
            MidiTitleHelper.GetTitleFromFilePath(song.FilePath),
            song.FilePath);
    }

    /// <summary>Full title refresh including MIDI parse — use when importing/building songs, not when opening playlists.</summary>
    public void RefreshSongTitleFromFile(Song song)
    {
        if (!File.Exists(song.FilePath))
            return;

        var fromFileName = MidiFileNameTitleHelper.FromFilePath(song.FilePath);
        if (MidiFileNameTitleHelper.IsInformative(fromFileName) &&
            !MidiFileNameTitleHelper.LooksTruncatedFileName(fromFileName))
        {
            song.Title = fromFileName;
            return;
        }

        if (_metadataCache is not null && _metadataCache.TryGetTitle(song.FilePath, out var cachedTitle))
        {
            song.Title = cachedTitle;
            return;
        }

        try
        {
            var parsed = _midiParser.Parse(song.FilePath);
            if (!string.IsNullOrWhiteSpace(parsed.Title))
            {
                song.Title = parsed.Title;
                _metadataCache?.UpdateTitle(song.FilePath, parsed.Title);
            }
        }
        catch
        {
            song.Title = MidiTitleHelper.GetTitleFromFilePath(song.FilePath);
        }
    }

    public List<Song> ImportFolder(string folderPath, bool smartTranspose, bool strictMode,
        SearchOption searchOption = SearchOption.AllDirectories)
    {
        if (!Directory.Exists(folderPath))
            throw new DirectoryNotFoundException(folderPath);

        var files = Directory.EnumerateFiles(folderPath, "*.*", searchOption)
            .Where(f => f.EndsWith(".mid", StringComparison.OrdinalIgnoreCase)
                        || f.EndsWith(".midi", StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase);

        var songs = new List<Song>();
        foreach (var file in files)
        {
            try
            {
                songs.Add(BuildSongFromFile(file, smartTranspose, strictMode));
            }
            catch
            {
                // Skip unreadable files during bulk import.
            }
        }

        return songs;
    }

    public Song BuildSongFromFile(string filePath, bool smartTranspose, bool strictMode)
    {
        if (_metadataCache is not null && _metadataCache.TryGetSong(filePath, smartTranspose, strictMode, out var cached))
            return cached;

        var parsed = _midiParser.Parse(filePath);
        var notes = smartTranspose
            ? MidiTransposeService.ApplyTranspose(parsed.Notes, MidiTransposeService.DetectBestTranspose(parsed.Notes))
            : parsed.Notes.ToList();
        var ranged = _noteRange.ApplyRange(notes, smartTranspose, strictMode);

        var song = new Song
        {
            Title = parsed.Title,
            FilePath = filePath,
            AddedAt = DateTime.UtcNow,
            DurationMs = parsed.DurationMs,
            NoteCount = ranged.Notes.Count,
            OutOfRangeNoteCount = ranged.OutOfRangeNoteCount
        };
        _metadataCache?.StoreSong(filePath, smartTranspose, strictMode, song);
        return song;
    }

    private static void EnsureAddedAt(Song song)
    {
        if (song.AddedAt == default)
            song.AddedAt = DateTime.UtcNow;
    }

    public static string DefaultPlaylistPath(string name)
    {
        AppPaths.EnsureCreated();
        var safeName = string.Join("_", name.Split(Path.GetInvalidFileNameChars()));
        return Path.Combine(AppPaths.PlaylistsFolder, $"{safeName}.json");
    }
}
