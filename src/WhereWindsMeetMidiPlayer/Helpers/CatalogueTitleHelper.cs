using System.Text.RegularExpressions;

namespace WhereWindsMeetMidiPlayer.Helpers;

public static class CatalogueTitleHelper
{
    private static readonly Regex DebraPrefix = new(
        @"^debra(?:\s*yume|yume)?\s*[-–—:|_\s]*",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>Appends _1, _2, … when copying duplicate files — hide on display.</summary>
    private static readonly Regex DuplicateIndexSuffix = new(
        @"_\d+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>After underscores become spaces: "Heroes 1" from Heroes_1.mid.</summary>
    private static readonly Regex TrailingSpaceIndexSuffix = new(
        @"\s+\d{1,4}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Prefer the MIDI file name when it is more complete than stored metadata; strip Debra prefix and duplicate _1/_2 suffixes.
    /// </summary>
    public static string GetDisplayTitle(string? title, string? filePath = null)
    {
        var fromFile = string.IsNullOrWhiteSpace(filePath)
            ? string.Empty
            : MidiFileNameTitleHelper.FromFilePath(filePath);

        var stored = title?.Trim() ?? string.Empty;
        string baseTitle;
        if (MidiFileNameTitleHelper.IsInformative(fromFile)
            && (string.IsNullOrWhiteSpace(stored)
                || MidiFileNameTitleHelper.IsMoreInformative(fromFile, stored)
                || fromFile.Length > stored.Length + 1))
            baseTitle = fromFile;
        else
            baseTitle = stored;

        return GetDisplayTitle(baseTitle);
    }

    public static string GetDisplayTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return string.Empty;

        var trimmed = title.Trim();
        var cleaned = DebraPrefix.Replace(trimmed, string.Empty).Trim();
        if (cleaned.StartsWith('-') || cleaned.StartsWith('–') || cleaned.StartsWith('—'))
            cleaned = cleaned[1..].Trim();

        cleaned = StripDuplicateIndexSuffix(cleaned);

        if (string.IsNullOrWhiteSpace(cleaned))
        {
            cleaned = StripDuplicateIndexSuffix(trimmed);
            return string.IsNullOrWhiteSpace(cleaned) ? trimmed : cleaned;
        }

        return cleaned;
    }

    private static string StripDuplicateIndexSuffix(string value)
    {
        var cleaned = DuplicateIndexSuffix.Replace(value, string.Empty).Trim();
        return TrailingSpaceIndexSuffix.Replace(cleaned, string.Empty).Trim();
    }
}