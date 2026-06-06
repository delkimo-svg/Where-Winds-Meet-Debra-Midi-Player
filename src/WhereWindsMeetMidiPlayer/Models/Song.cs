using System.ComponentModel;
using System.Runtime.CompilerServices;
using WhereWindsMeetMidiPlayer.Helpers;

namespace WhereWindsMeetMidiPlayer.Models;

public sealed class Song : INotifyPropertyChanged
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = string.Empty;

    /// <summary>Title without Debra / Debra Yume / DebraYume prefix for display.</summary>
    public string DisplayTitle => CatalogueTitleHelper.GetDisplayTitle(Title, FilePath);
    public string FilePath { get; set; } = string.Empty;
    /// <summary>UTC time when the song was added to the library or playlist.</summary>
    public DateTime AddedAt { get; set; }
    public long DurationMs { get; set; }
    public string? DetectedKey { get; set; }
    public int NoteCount { get; set; }
    public int OutOfRangeNoteCount { get; set; }

    private bool _isFavorite;
    public bool IsFavorite
    {
        get => _isFavorite;
        set
        {
            if (_isFavorite == value)
                return;
            _isFavorite = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Independent copy for playlist storage (decoupled from library entries).</summary>
    public Song CloneForPlaylist() => new()
    {
        Title = Title,
        FilePath = FilePath,
        AddedAt = AddedAt == default ? DateTime.UtcNow : AddedAt,
        DurationMs = DurationMs,
        DetectedKey = DetectedKey,
        NoteCount = NoteCount,
        OutOfRangeNoteCount = OutOfRangeNoteCount,
        IsFavorite = IsFavorite
    };

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
