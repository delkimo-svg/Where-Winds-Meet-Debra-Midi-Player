using WhereWindsMeetMidiPlayer.Infrastructure;

namespace WhereWindsMeetMidiPlayer.Services;

/// <summary>Per-song playback tempo overrides (percent of MIDI tempo). Does not modify MIDI files.</summary>
public sealed class SongTempoStore
{
    private Dictionary<string, int> _percents = new(StringComparer.OrdinalIgnoreCase);

    public void Load()
    {
        AppPaths.EnsureCreated();
        _percents = JsonFileStore.Read<Dictionary<string, int>>(AppPaths.SongTempoFile)
                    ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    }

    public void Save() => JsonFileStore.Write(AppPaths.SongTempoFile, _percents);

    public bool TryGetPercent(string filePath, out int percent) =>
        _percents.TryGetValue(Normalize(filePath), out percent);

    public void SetPercent(string filePath, int percent)
    {
        var key = Normalize(filePath);
        percent = Math.Clamp(percent, 50, 200);
        if (percent == 100)
            _percents.Remove(key);
        else
            _percents[key] = percent;
    }

    private static string Normalize(string filePath) =>
        Path.GetFullPath(filePath.Trim());
}
