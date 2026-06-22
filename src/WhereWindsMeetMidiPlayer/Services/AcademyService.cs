using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using WhereWindsMeetMidiPlayer.Infrastructure;
using WhereWindsMeetMidiPlayer.Models;

namespace WhereWindsMeetMidiPlayer.Services;

public sealed class AcademyService
{
    public const string BundledManifestFileName = "academy-manifest.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public AcademyManifest? Current { get; private set; }

    public void LoadBundled()
    {
        foreach (var path in BundledSearchPaths())
        {
            if (!File.Exists(path))
                continue;

            try
            {
                var json = File.ReadAllText(path, Encoding.UTF8);
                Current = Deserialize(json);
                EnrichFromBundled();
                return;
            }
            catch (Exception ex)
            {
                AppPaths.WriteDiagnosticLog("academy-bundled", ex);
            }
        }
    }

    public void ApplyRemote(AcademyManifest manifest)
    {
        Current = manifest;
        EnrichFromBundled();
    }

    public void EnrichFromBundled()
    {
        if (Current is null)
            return;

        var bundled = TryReadBundledManifest();
        if (bundled is null)
            return;

        foreach (var module in Current.Modules)
        {
            var bundledModule = bundled.Modules.FirstOrDefault(m =>
                m.Id.Equals(module.Id, StringComparison.OrdinalIgnoreCase));
            if (bundledModule is null)
                continue;

            foreach (var lesson in module.Lessons)
            {
                var bundledLesson = bundledModule.Lessons.FirstOrDefault(l =>
                    l.Id.Equals(lesson.Id, StringComparison.OrdinalIgnoreCase));
                if (bundledLesson is null)
                    continue;

                if (string.IsNullOrWhiteSpace(lesson.BundledMidiPath) &&
                    !string.IsNullOrWhiteSpace(bundledLesson.BundledMidiPath))
                    lesson.BundledMidiPath = bundledLesson.BundledMidiPath;

                if (ResolveBundledMidiPath(lesson) is not null)
                    lesson.ComingSoon = false;

                if (lesson.TourSteps is null or { Count: 0 } && bundledLesson.TourSteps is { Count: > 0 })
                    lesson.TourSteps = bundledLesson.TourSteps;

                if (string.IsNullOrWhiteSpace(lesson.Guide) && !string.IsNullOrWhiteSpace(bundledLesson.Guide))
                    lesson.Guide = bundledLesson.Guide;

                if (lesson.Hand == AcademyHand.Any && bundledLesson.Hand != AcademyHand.Any)
                    lesson.Hand = bundledLesson.Hand;

                if (lesson.EnabledTracks is null or { Length: 0 } &&
                    bundledLesson.EnabledTracks is { Length: > 0 })
                    lesson.EnabledTracks = bundledLesson.EnabledTracks;
            }
        }
    }

    private static AcademyManifest? TryReadBundledManifest()
    {
        foreach (var path in BundledSearchPaths())
        {
            if (!File.Exists(path))
                continue;

            try
            {
                var json = File.ReadAllText(path, Encoding.UTF8);
                return Deserialize(json);
            }
            catch (Exception ex)
            {
                AppPaths.WriteDiagnosticLog("academy-bundled-read", ex);
            }
        }

        return null;
    }

    public AcademyModule? FindModule(string id) =>
        Current?.Modules.FirstOrDefault(m => m.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    public AcademyLesson? FindLesson(string lessonId)
    {
        if (Current is null)
            return null;

        foreach (var module in Current.Modules)
        {
            var lesson = module.Lessons.FirstOrDefault(l =>
                l.Id.Equals(lessonId, StringComparison.OrdinalIgnoreCase));
            if (lesson is not null)
                return lesson;
        }

        return null;
    }

    public static string? ResolveBundledMidiPath(AcademyLesson lesson)
    {
        if (string.IsNullOrWhiteSpace(lesson.BundledMidiPath))
            return null;

        var relative = lesson.BundledMidiPath.Replace('/', Path.DirectorySeparatorChar);
        foreach (var root in BundledRoots())
        {
            var combined = Path.Combine(root, relative);
            if (File.Exists(combined))
                return Path.GetFullPath(combined);
        }

        return null;
    }

    public static IEnumerable<string> BundledSearchPaths()
    {
        yield return Path.Combine(AppContext.BaseDirectory, BundledManifestFileName);
        yield return Path.Combine(AppContext.BaseDirectory, "Assets", BundledManifestFileName);
    }

    public static IEnumerable<string> BundledRoots()
    {
        yield return AppContext.BaseDirectory;
        yield return Path.Combine(AppContext.BaseDirectory, "Assets");
    }

    public static AcademyManifest Deserialize(string json) =>
        JsonSerializer.Deserialize<AcademyManifest>(json, JsonOptions)
        ?? throw new InvalidOperationException("Academy manifest is empty.");

    public void SaveCache(AcademyManifest manifest)
    {
        AppPaths.EnsureCreated();
        var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(AppPaths.AcademyManifestCacheFile, json, Encoding.UTF8);
    }

    public bool TryLoadCache()
    {
        if (!File.Exists(AppPaths.AcademyManifestCacheFile))
            return false;

        try
        {
            var json = File.ReadAllText(AppPaths.AcademyManifestCacheFile, Encoding.UTF8);
            var manifest = Deserialize(json);
            if (manifest.Modules.Count == 0)
                return false;

            Current = manifest;
            EnrichFromBundled();
            return true;
        }
        catch (Exception ex)
        {
            AppPaths.WriteDiagnosticLog("academy-cache", ex);
            return false;
        }
    }
}
