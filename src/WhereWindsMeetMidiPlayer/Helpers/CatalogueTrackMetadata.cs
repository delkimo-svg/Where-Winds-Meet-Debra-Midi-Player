using WhereWindsMeetMidiPlayer.Models;
using WhereWindsMeetMidiPlayer.Services;

namespace WhereWindsMeetMidiPlayer.Helpers;

public static class CatalogueTrackMetadata
{
    public static void EnrichDuration(CatalogueTrack track, MidiParserService parser,
        SongMetadataCacheService? metadataCache = null)
    {
        if (track.DurationMs > 0)
            return;

        var path = track.CachedFilePath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return;

        if (metadataCache is not null && metadataCache.TryGetDuration(path, out var cachedDuration))
        {
            track.DurationMs = cachedDuration;
            return;
        }

        try
        {
            track.DurationMs = parser.Parse(path).DurationMs;
            metadataCache?.StoreDuration(path, track.DurationMs);
        }
        catch
        {
            // ignore corrupt cache files
        }
    }

    public static void EnrichDurations(IEnumerable<CatalogueTrack> tracks, MidiParserService parser,
        SongMetadataCacheService? metadataCache = null)
    {
        foreach (var track in tracks)
            EnrichDuration(track, parser, metadataCache);
    }
}
