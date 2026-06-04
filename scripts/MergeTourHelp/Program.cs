using System.Text.Encodings.Web;
using System.Text.Json;

var locDir = args.Length > 0
    ? args[0]
    : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "src", "WhereWindsMeetMidiPlayer", "Assets", "Localization"));

var opts = new JsonSerializerOptions
{
    WriteIndented = true,
    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
};

var langs = new[] { "fr", "es", "pt", "de", "it", "ja", "zh", "ar" };
var enTourPath = Path.Combine(locDir, "tour-help-en-extra.json");
var enKeybindPath = Path.Combine(locDir, "keybind-ui-en-extra.json");
var enTour = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(enTourPath))
             ?? throw new InvalidOperationException("Missing tour-help-en-extra.json");
var enKeybind = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(enKeybindPath))
                ?? throw new InvalidOperationException("Missing keybind-ui-en-extra.json");

foreach (var lang in langs)
{
    var basePath = Path.Combine(locDir, $"{lang}.json");
    var tourPath = Path.Combine(locDir, $"tour-help-{lang}-extra.json");
    var keybindPath = Path.Combine(locDir, $"keybind-ui-{lang}-extra.json");

    var tour = File.Exists(tourPath)
        ? JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(tourPath)) ?? enTour
        : enTour;
    var keybind = File.Exists(keybindPath)
        ? JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(keybindPath)) ?? enKeybind
        : enKeybind;

    var merged = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(basePath))
                 ?? new Dictionary<string, string>(StringComparer.Ordinal);

    foreach (var kv in tour)
        merged[kv.Key] = kv.Value;
    foreach (var kv in keybind)
        merged[kv.Key] = kv.Value;

    File.WriteAllText(basePath, JsonSerializer.Serialize(merged, opts));
    var tourNote = File.Exists(tourPath) ? "tour translated" : "tour en";
    var keyNote = File.Exists(keybindPath) ? "keybind translated" : "keybind en";
    Console.WriteLine($"Merged {lang}.json ({tourNote}, {keyNote})");
}
