using WhereWindsMeetMidiPlayer.Models;

namespace WhereWindsMeetMidiPlayer.Services;

public sealed class LibraryService
{
    private readonly PlaylistService _playlistService;

    public List<Song> Songs { get; } = [];

    public LibraryService(PlaylistService playlistService)
    {
        _playlistService = playlistService;
    }

    public Song AddFile(string filePath, bool smartTranspose, bool strictMode, string? preferredTitle = null)
    {
        var song = _playlistService.BuildSongFromFile(filePath, smartTranspose, strictMode);
        if (!string.IsNullOrWhiteSpace(preferredTitle))
            song.Title = preferredTitle.Trim();
        else
            _playlistService.RefreshSongTitleFromFile(song);

        if (!Songs.Any(s => s.FilePath.Equals(song.FilePath, StringComparison.OrdinalIgnoreCase)))
            Songs.Add(song);
        return song;
    }

    public List<Song> ImportFolder(string folderPath, bool smartTranspose, bool strictMode)
    {
        var imported = _playlistService.ImportFolder(folderPath, smartTranspose, strictMode);
        foreach (var song in imported)
        {
            if (!Songs.Any(s => s.FilePath.Equals(song.FilePath, StringComparison.OrdinalIgnoreCase)))
                Songs.Add(song);
        }

        return imported;
    }

    public bool RemoveSong(Song song) =>
        Songs.RemoveAll(s => s.Id == song.Id) > 0;

    public void Clear() => Songs.Clear();
}
