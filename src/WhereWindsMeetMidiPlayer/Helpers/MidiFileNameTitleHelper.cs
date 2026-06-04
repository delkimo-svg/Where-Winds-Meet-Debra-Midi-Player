using System.Net;
using WhereWindsMeetMidiPlayer.Models;

namespace WhereWindsMeetMidiPlayer.Helpers;

/// <summary>
/// Builds display titles from MIDI file names (Discord attachment name, URL path, or cached path on disk).
/// </summary>
public static class MidiFileNameTitleHelper
{
    private static readonly string[] GenericNames =
    [
        "untitled", "track", "midi", "sequence", "export", "song", "new", "download"
    ];

    /// <summary>Catalogue: prefer attachment / URL file name; message text is only a fallback.</summary>
    public static string ResolveCatalogueTitle(string? attachmentFileName, string? downloadUrl, string? messageLine)
    {
        foreach (var candidate in new[]
                 {
                     FromFileName(attachmentFileName),
                     FromUrlPath(downloadUrl),
                     Normalize(messageLine)
                 })
        {
            if (IsInformative(candidate) && !LooksTruncatedFileName(candidate))
                return candidate!;
        }

        return FirstNonEmpty(FromFileName(attachmentFileName), Normalize(messageLine)) ?? "Untitled";
    }

    public static void RefreshCatalogueTrackTitle(CatalogueTrack track)
    {
        var fromSource = FromFileName(track.SourceFileName);
        var fromUrl = FromUrlPath(track.DownloadUrl);
        var fromCache = string.IsNullOrWhiteSpace(track.CachedFilePath)
            ? string.Empty
            : FromFilePath(track.CachedFilePath);

        string? best = null;
        foreach (var candidate in new[] { fromCache, fromSource, fromUrl })
        {
            if (!IsInformative(candidate) || LooksTruncatedFileName(candidate))
                continue;

            if (best is null || IsMoreInformative(candidate, best))
                best = candidate;
        }

        if (best is not null && IsMoreInformative(best, track.Title))
            track.Title = best;
    }

    public static string FromFilePath(string filePath) => FromFileName(Path.GetFileName(filePath));

    public static string FromFileName(string? fileNameOrPath)
    {
        if (string.IsNullOrWhiteSpace(fileNameOrPath))
            return string.Empty;

        var name = fileNameOrPath.Trim();
        if (name.Contains('/') || name.Contains('\\'))
            name = Path.GetFileName(name);

        name = Path.GetFileNameWithoutExtension(name);
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        name = WebUtility.UrlDecode(name);
        name = Uri.UnescapeDataString(name);
        return name.Replace('_', ' ').Trim();
    }

    public static string FromUrlPath(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return string.Empty;

        try
        {
            var path = new Uri(url, UriKind.Absolute).AbsolutePath;
            return FromFileName(path);
        }
        catch
        {
            return FromFileName(url);
        }
    }

    public static bool IsInformative(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return false;

        var normalized = title.Trim();
        if (normalized.Length <= 2)
            return false;

        if (GenericNames.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            return false;

        if (normalized.Length <= 8 && normalized.All(c => char.IsDigit(c) || c is '_' or '-' or ' ' or '.'))
            return false;

        return true;
    }

    /// <summary>Discord sometimes stores ASCII-only stubs like "Eric -" while the real .mid name is complete.</summary>
    public static bool LooksTruncatedFileName(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return false;

        var t = title.Trim();
        if (t.EndsWith('-') || t.EndsWith('–') || t.EndsWith('—'))
            return true;

        return t.EndsWith(" -", StringComparison.Ordinal);
    }

    public static bool IsMoreInformative(string candidate, string? current)
    {
        if (!IsInformative(candidate))
            return false;

        if (!IsInformative(current))
            return true;

        if (CjkTextHelper.ContainsEastAsianScript(candidate) && !CjkTextHelper.ContainsEastAsianScript(current))
            return true;

        return candidate.Length > current.Length + 2;
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }
}
