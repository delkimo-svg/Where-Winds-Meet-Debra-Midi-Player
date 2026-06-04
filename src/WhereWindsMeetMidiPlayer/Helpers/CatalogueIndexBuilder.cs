using WhereWindsMeetMidiPlayer.Models;

namespace WhereWindsMeetMidiPlayer.Helpers;

internal static class CatalogueIndexBuilder
{
    public static List<string> BuildStyleNames(IReadOnlyList<CatalogueTrack> tracks) =>
        tracks
            .GroupBy(t => t.StyleName)
            .OrderBy(g => g.Min(t => t.StyleSortOrder))
            .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.Key)
            .ToList();

    public static Dictionary<string, CatalogueTrack> BuildPathIndex(IReadOnlyList<CatalogueTrack> tracks)
    {
        var map = new Dictionary<string, CatalogueTrack>(StringComparer.OrdinalIgnoreCase);
        foreach (var track in tracks)
        {
            if (string.IsNullOrWhiteSpace(track.CachedFilePath))
                continue;

            map.TryAdd(track.CachedFilePath, track);
        }

        return map;
    }
}
