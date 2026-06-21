using System.Text;
using WhereWindsMeetMidiPlayer.Infrastructure;

namespace WhereWindsMeetMidiPlayer.Services;

public sealed class KeyMappingService
{
    private Dictionary<int, string> _mapping = new();

    public IReadOnlyDictionary<int, string> Mapping => _mapping;
    public int MappedNoteCount => _mapping.Count;

    public void LoadFromFile(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("Key mapping file not found.", path);

        var json = File.ReadAllText(path, Encoding.UTF8);
        var raw = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json)
            ?? throw new InvalidOperationException("Key mapping file is empty or invalid.");

        var map = new Dictionary<int, string>();
        foreach (var (key, value) in raw)
        {
            if (!int.TryParse(key, out var noteNumber))
                continue;

            map[noteNumber] = value;
        }

        _mapping = map;
    }

    public string? GetKeyCombo(int noteNumber) =>
        _mapping.TryGetValue(noteNumber, out var combo) ? combo : null;

    public int? TryGetNoteForCombo(string combo)
    {
        if (string.IsNullOrWhiteSpace(combo))
            return null;

        foreach (var (midi, mapped) in _mapping)
            if (string.Equals(mapped, combo, StringComparison.OrdinalIgnoreCase))
                return midi;

        return null;
    }

    public void SetKeyCombo(int noteNumber, string combo) => _mapping[noteNumber] = combo;

    public IReadOnlyDictionary<int, string> CloneMapping() =>
        new Dictionary<int, string>(_mapping);

    public void ReplaceMapping(IReadOnlyDictionary<int, string> mapping) =>
        _mapping = new Dictionary<int, string>(mapping);

    public Dictionary<string, string> ExportToFileDictionary() =>
        _mapping.ToDictionary(static kv => kv.Key.ToString(), static kv => kv.Value);

    public void SaveToFile(string path)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(ExportToFileDictionary(), JsonFileStore.Options);
        File.WriteAllText(path, json, Encoding.UTF8);
    }

    public string EnsureDefaultKeyMap(string fileName = "default-keymap.json", bool updateFromBundle = false)
    {
        AppPaths.EnsureCreated();
        var targetPath = Path.Combine(AppPaths.KeyMapsFolder, fileName);
        var bundled = Path.Combine(AppContext.BaseDirectory, "Assets", fileName);

        if (File.Exists(bundled) && (!File.Exists(targetPath) || updateFromBundle))
        {
            File.Copy(bundled, targetPath, overwrite: true);
            return targetPath;
        }

        if (File.Exists(targetPath))
            return targetPath;

        var map = CreateDefaultMapping();
        File.WriteAllText(targetPath, System.Text.Json.JsonSerializer.Serialize(map, JsonFileStore.Options));
        return targetPath;
    }

    public static Dictionary<string, string> CreateDefaultMapping() =>
        GameKeyLayout.BuildWhereWindsMeetMap();
}
