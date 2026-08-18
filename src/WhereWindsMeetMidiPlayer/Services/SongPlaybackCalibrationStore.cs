using WhereWindsMeetMidiPlayer.Infrastructure;
using WhereWindsMeetMidiPlayer.Models;

namespace WhereWindsMeetMidiPlayer.Services;

/// <summary>Per-song playback calibration (octave shift, track filter, mapping mode).</summary>
public sealed class SongPlaybackCalibrationStore
{
    private Dictionary<string, SongPlaybackCalibration> _entries = new(StringComparer.OrdinalIgnoreCase);

    public void Load()
    {
        AppPaths.EnsureCreated();
        _entries = JsonFileStore.Read<Dictionary<string, SongPlaybackCalibration>>(AppPaths.SongPlaybackFile)
                    ?? new Dictionary<string, SongPlaybackCalibration>(StringComparer.OrdinalIgnoreCase);
    }

    public void Save() => JsonFileStore.Write(AppPaths.SongPlaybackFile, _entries);

    public SongPlaybackCalibration GetOrDefault(string filePath)
    {
        var key = Normalize(filePath);
        return _entries.TryGetValue(key, out var entry)
            ? new SongPlaybackCalibration
            {
                OctaveShift = entry.OctaveShift,
                TrackIndex = entry.TrackIndex,
                MappingMode = entry.MappingMode,
                PhraseFold = entry.PhraseFold,
                MutedTracks = [.. entry.MutedTracks],
                FfxivInstrumentId = entry.FfxivInstrumentId
            }
            : new SongPlaybackCalibration();
    }

    public void Set(string filePath, SongPlaybackCalibration calibration)
    {
        var key = Normalize(filePath);
        if (calibration.IsDefault)
            _entries.Remove(key);
        else
            _entries[key] = calibration;
    }

    private static string Normalize(string filePath) =>
        Path.GetFullPath(filePath.Trim());
}

public sealed class SongPlaybackCalibration
{
  public const int MinOctaveShift = -2;
  public const int MaxOctaveShift = 2;

  public int OctaveShift { get; set; }
  /// <summary>Legacy single-track filter (-1 = all tracks); migrated to MutedTracks on load.</summary>
  public int TrackIndex { get; set; } = -1;
  public NoteMappingMode MappingMode { get; set; } = NoteMappingMode.Chromatic36;
  /// <summary>Additive on top of the mapping mode: out-of-range passages shift as whole phrases.</summary>
  public bool PhraseFold { get; set; }
  /// <summary>MIDI track indexes muted in the player's track mixer.</summary>
  public List<int> MutedTracks { get; set; } = [];
  /// <summary>FFXIV instrument chosen for this song (Perform-sheet id); 0 = auto-detect from track names.</summary>
  public int FfxivInstrumentId { get; set; }

  public bool IsDefault =>
      OctaveShift == 0
      && TrackIndex == -1
      && MappingMode == NoteMappingMode.Chromatic36
      && !PhraseFold
      && MutedTracks.Count == 0
      && FfxivInstrumentId == 0;
}
