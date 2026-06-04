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

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
