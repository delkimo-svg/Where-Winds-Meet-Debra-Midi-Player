using System.Text.Json;
using WhereWindsMeetMidiPlayer.Infrastructure;
using WhereWindsMeetMidiPlayer.Models;

namespace WhereWindsMeetMidiPlayer.Services;

/// <summary>
/// Persistent MIDI metadata cache (title, duration, note counts) keyed by file path + size + mtime.
/// Avoids re-parsing every library/catalogue file on each launch.
/// </summary>
public sealed class SongMetadataCacheService
{
    private sealed class Entry
    {
        public long Size { get; set; }
        public long MTimeTicks { get; set; }
        public string Title { get; set; } = string.Empty;
        public long DurationMs { get; set; }

        // Note counts depend on transpose/range flags; null flags mean counts not computed yet.
        public bool? SmartTranspose { get; set; }
        public bool? StrictNoteRange { get; set; }
        public int NoteCount { get; set; }
        public int OutOfRangeNoteCount { get; set; }
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    private readonly object _gate = new();
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.OrdinalIgnoreCase);
    private bool _dirty;
    private bool _saveScheduled;

    public void Load()
    {
        try
        {
            var path = AppPaths.SongMetadataCacheFile;
            if (!File.Exists(path))
                return;

            var loaded = JsonSerializer.Deserialize<Dictionary<string, Entry>>(File.ReadAllText(path), JsonOptions);
            if (loaded is null)
                return;

            lock (_gate)
            {
                foreach (var (key, value) in loaded)
                    _entries[key] = value;
            }
        }
        catch (Exception ex)
        {
            AppPaths.WriteDiagnosticLog("song-metadata-cache-load", ex);
        }
    }

    /// <summary>Full song info for import paths — hit only when file and flags match.</summary>
    public bool TryGetSong(string filePath, bool smartTranspose, bool strictMode, out Song song)
    {
        song = null!;
        if (!TryGetFreshEntry(filePath, out var entry))
            return false;

        if (entry.SmartTranspose != smartTranspose || entry.StrictNoteRange != strictMode)
            return false;

        song = new Song
        {
            Title = entry.Title,
            FilePath = filePath,
            AddedAt = DateTime.UtcNow,
            DurationMs = entry.DurationMs,
            NoteCount = entry.NoteCount,
            OutOfRangeNoteCount = entry.OutOfRangeNoteCount
        };
        return true;
    }

    /// <summary>Duration is flag-independent — used by the catalogue duration enrichment.</summary>
    public bool TryGetDuration(string filePath, out long durationMs)
    {
        durationMs = 0;
        if (!TryGetFreshEntry(filePath, out var entry) || entry.DurationMs <= 0)
            return false;

        durationMs = entry.DurationMs;
        return true;
    }

    public bool TryGetTitle(string filePath, out string title)
    {
        title = string.Empty;
        if (!TryGetFreshEntry(filePath, out var entry) || string.IsNullOrWhiteSpace(entry.Title))
            return false;

        title = entry.Title;
        return true;
    }

    public void StoreSong(string filePath, bool smartTranspose, bool strictMode, Song song)
    {
        if (!TryGetFileStamp(filePath, out var size, out var mtime))
            return;

        lock (_gate)
        {
            _entries[filePath] = new Entry
            {
                Size = size,
                MTimeTicks = mtime,
                Title = song.Title,
                DurationMs = song.DurationMs,
                SmartTranspose = smartTranspose,
                StrictNoteRange = strictMode,
                NoteCount = song.NoteCount,
                OutOfRangeNoteCount = song.OutOfRangeNoteCount
            };
            MarkDirtyLocked();
        }
    }

    public void StoreDuration(string filePath, long durationMs)
    {
        if (durationMs <= 0 || !TryGetFileStamp(filePath, out var size, out var mtime))
            return;

        lock (_gate)
        {
            if (_entries.TryGetValue(filePath, out var existing) &&
                existing.Size == size && existing.MTimeTicks == mtime)
            {
                existing.DurationMs = durationMs;
            }
            else
            {
                _entries[filePath] = new Entry
                {
                    Size = size,
                    MTimeTicks = mtime,
                    DurationMs = durationMs
                };
            }

            MarkDirtyLocked();
        }
    }

    public void UpdateTitle(string filePath, string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return;

        lock (_gate)
        {
            if (!_entries.TryGetValue(filePath, out var entry))
                return;

            if (string.Equals(entry.Title, title, StringComparison.Ordinal))
                return;

            entry.Title = title;
            MarkDirtyLocked();
        }
    }

    public void Flush()
    {
        Dictionary<string, Entry> snapshot;
        lock (_gate)
        {
            if (!_dirty)
                return;
            _dirty = false;
            snapshot = new Dictionary<string, Entry>(_entries, StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            AppPaths.EnsureCreated();
            File.WriteAllText(AppPaths.SongMetadataCacheFile, JsonSerializer.Serialize(snapshot, JsonOptions));
        }
        catch (Exception ex)
        {
            AppPaths.WriteDiagnosticLog("song-metadata-cache-save", ex);
        }
    }

    private void MarkDirtyLocked()
    {
        _dirty = true;
        if (_saveScheduled)
            return;

        _saveScheduled = true;
        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
            lock (_gate)
            {
                _saveScheduled = false;
            }
            Flush();
        });
    }

    private bool TryGetFreshEntry(string filePath, out Entry entry)
    {
        entry = null!;
        if (string.IsNullOrWhiteSpace(filePath))
            return false;

        if (!TryGetFileStamp(filePath, out var size, out var mtime))
            return false;

        lock (_gate)
        {
            if (!_entries.TryGetValue(filePath, out var found))
                return false;

            if (found.Size != size || found.MTimeTicks != mtime)
            {
                _entries.Remove(filePath);
                return false;
            }

            entry = found;
            return true;
        }
    }

    private static bool TryGetFileStamp(string filePath, out long size, out long mtimeTicks)
    {
        size = 0;
        mtimeTicks = 0;
        try
        {
            var info = new FileInfo(filePath);
            if (!info.Exists)
                return false;

            size = info.Length;
            mtimeTicks = info.LastWriteTimeUtc.Ticks;
            return true;
        }
        catch
        {
            return false;
        }
    }
}
