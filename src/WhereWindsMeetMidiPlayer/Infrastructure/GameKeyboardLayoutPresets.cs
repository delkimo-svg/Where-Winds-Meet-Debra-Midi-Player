namespace WhereWindsMeetMidiPlayer.Infrastructure;

/// <summary>
/// Keyboard layout presets aligned with SnowiyQ Where-Winds-Meet-Midi-Player (QWERTY / QWERTZ / AZERTY).
/// Each preset only changes the seven natural keys per octave row; sharps/flats use Shift/Ctrl on those keys.
/// </summary>
public sealed class GameKeyboardLayoutPreset
{
    public required string Id { get; init; }
    public required string FileName { get; init; }
    public required string NameKey { get; init; }
    public required string DescriptionKey { get; init; }
    public required string[] Low { get; init; }
    public required string[] Mid { get; init; }
    public required string[] High { get; init; }

    public Dictionary<string, string> BuildMap() =>
        GameKeyLayout.BuildMapFromNaturalRows(Low, Mid, High);
}

public static class GameKeyboardLayoutPresets
{
    public const string QwertyId = "qwerty";
    public const string QwertzId = "qwertz";
    public const string AzertyId = "azerty";

    public static IReadOnlyList<GameKeyboardLayoutPreset> All { get; } = [
        new GameKeyboardLayoutPreset
        {
            Id = QwertyId,
            FileName = "preset-qwerty.json",
            NameKey = "Settings_NoteKeysPresetQwerty",
            DescriptionKey = "Settings_NoteKeysPresetQwertyDesc",
            Low = ["Z", "X", "C", "V", "B", "N", "M"],
            Mid = ["A", "S", "D", "F", "G", "H", "J"],
            High = ["Q", "W", "E", "R", "T", "Y", "U"]
        },
        new GameKeyboardLayoutPreset
        {
            Id = QwertzId,
            FileName = "preset-qwertz.json",
            NameKey = "Settings_NoteKeysPresetQwertz",
            DescriptionKey = "Settings_NoteKeysPresetQwertzDesc",
            Low = ["Y", "X", "C", "V", "B", "N", "M"],
            Mid = ["A", "S", "D", "F", "G", "H", "J"],
            High = ["Q", "W", "E", "R", "T", "Z", "U"]
        },
        new GameKeyboardLayoutPreset
        {
            Id = AzertyId,
            FileName = "preset-azerty.json",
            NameKey = "Settings_NoteKeysPresetAzerty",
            DescriptionKey = "Settings_NoteKeysPresetAzertyDesc",
            Low = ["W", "X", "C", "V", "B", "N", ","],
            Mid = ["Q", "S", "D", "F", "G", "H", "J"],
            High = ["A", "Z", "E", "R", "T", "Y", "U"]
        }
    ];

    public static GameKeyboardLayoutPreset? Find(string id) =>
        All.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));

    public static GameKeyboardLayoutPreset? FindByFileName(string fileName) =>
        All.FirstOrDefault(p => string.Equals(p.FileName, fileName, StringComparison.OrdinalIgnoreCase));
}
