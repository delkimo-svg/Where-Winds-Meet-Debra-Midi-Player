using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;

namespace WhereWindsMeetMidiPlayer.Localization;

public sealed class LocalizationService
{
    public static LocalizationService Instance { get; } = new();

    private readonly Dictionary<string, Dictionary<string, string>> _languages = new(StringComparer.OrdinalIgnoreCase);
    private string _currentCode = "en";
    private Dictionary<string, string> _current = new(StringComparer.Ordinal);

    public event EventHandler? LanguageChanged;

    public string CurrentLanguageCode => _currentCode;

    public bool IsRightToLeft => _currentCode.Equals("ar", StringComparison.OrdinalIgnoreCase);

    public FlowDirection FlowDirection =>
        IsRightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

    private LocalizationService()
    {
        LoadLanguageFile("en");
        _current = _languages["en"];
    }

    public void SetLanguage(string? code)
    {
        code = NormalizeCode(code);
        LoadLanguageFile(code);

        if (!_languages.TryGetValue(code, out var dict))
        {
            code = "en";
            dict = _languages["en"];
        }

        _currentCode = code;
        _current = dict;

        TryApplyCulture(code);

        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    public string Get(string key)
    {
        if (_current.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value))
            return value;

        if (_languages.TryGetValue("en", out var en) && en.TryGetValue(key, out var fallback))
            return fallback;

        return key;
    }

    public string Format(string key, params object[] args) =>
        string.Format(CultureInfo.CurrentCulture, Get(key), args);

    /// <summary>
    /// True when <paramref name="value"/> is empty or matches a localized string for <paramref name="key"/>
    /// in any bundled language file (e.g. catalogue "All styles" filter after UI language change).
    /// </summary>
    public bool MatchesAnyTranslation(string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return true;

        return GetAllTranslations(key).Contains(value);
    }

    private IReadOnlySet<string> GetAllTranslations(string key)
    {
        if (_allTranslationsCache is not null && _allTranslationsCache.TryGetValue(key, out var cached))
            return cached;

        _allTranslationsCache ??= new Dictionary<string, IReadOnlySet<string>>(StringComparer.OrdinalIgnoreCase);
        var labels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var dir = Path.Combine(AppContext.BaseDirectory, "Assets", "Localization");
        if (Directory.Exists(dir))
        {
            foreach (var file in Directory.EnumerateFiles(dir, "*.json"))
            {
                try
                {
                    var json = File.ReadAllText(file, Encoding.UTF8);
                    var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                    if (dict is not null
                        && dict.TryGetValue(key, out var label)
                        && !string.IsNullOrWhiteSpace(label))
                    {
                        labels.Add(label);
                    }
                }
                catch
                {
                    // Ignore malformed localization files.
                }
            }
        }

        if (labels.Count == 0 && _current.TryGetValue(key, out var current) && !string.IsNullOrWhiteSpace(current))
            labels.Add(current);

        cached = labels;
        _allTranslationsCache[key] = cached;
        return cached;
    }

    private Dictionary<string, IReadOnlySet<string>>? _allTranslationsCache;

    private static void TryApplyCulture(string code)
    {
        try
        {
            var culture = code switch
            {
                "zh" => new CultureInfo("zh-Hans"),
                "pt" => new CultureInfo("pt-BR"),
                _ => CultureInfo.GetCultureInfo(code)
            };

            CultureInfo.CurrentUICulture = culture;
            CultureInfo.CurrentCulture = culture;
        }
        catch (CultureNotFoundException)
        {
            CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        }
    }

    private static string NormalizeCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return "en";

        code = code.Trim().ToLowerInvariant();
        return code switch
        {
            "zh-cn" or "zh-hans" or "cn" => "zh",
            "pt-br" or "pt-pt" => "pt",
            _ => code.Split('-')[0]
        };
    }

    private void LoadLanguageFile(string code)
    {
        code = NormalizeCode(code);
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "Localization", $"{code}.json");
        if (!File.Exists(path) && !code.Equals("en", StringComparison.OrdinalIgnoreCase))
        {
            _languages[code] = _languages.TryGetValue("en", out var en)
                ? new Dictionary<string, string>(en, StringComparer.Ordinal)
                : [];
            return;
        }

        if (!File.Exists(path))
            path = Path.Combine(AppContext.BaseDirectory, "Assets", "Localization", "en.json");

        try
        {
            var json = File.ReadAllText(path, Encoding.UTF8);
            var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                       ?? new Dictionary<string, string>(StringComparer.Ordinal);
            _languages[code] = new Dictionary<string, string>(dict, StringComparer.Ordinal);
        }
        catch
        {
            _languages[code] = _languages.TryGetValue("en", out var en)
                ? new Dictionary<string, string>(en, StringComparer.Ordinal)
                : [];
        }
    }
}

public static class L
{
    public static string T(string key) => LocalizationService.Instance.Get(key);

    public static string F(string key, params object[] args) =>
        LocalizationService.Instance.Format(key, args);
}
