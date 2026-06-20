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
                MappingMode = entry.MappingMode
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
  /// <summary>-1 = all tracks; otherwise MIDI track index.</summary>
  public int TrackIndex { get; set; } = -1;
  public NoteMappingMode MappingMode { get; set; } = NoteMappingMode.Chromatic36;

  public bool IsDefault =>
      OctaveShift == 0
      && TrackIndex == -1
      && MappingMode == NoteMappingMode.Chromatic36;
}
