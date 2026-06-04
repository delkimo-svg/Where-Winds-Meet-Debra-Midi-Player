using WhereWindsMeetMidiPlayer.Models;
using WhereWindsMeetMidiPlayer.Services;

namespace WhereWindsMeetMidiPlayer.Helpers;

public static class CatalogueTrackMetadata
{
    public static void EnrichDuration(CatalogueTrack track, MidiParserService parser)
    {
        if (track.DurationMs > 0)
            return;

        var path = track.CachedFilePath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return;

        try
        {
            track.DurationMs = parser.Parse(path).DurationMs;
        }
        catch
        {
            // ignore corrupt cache files
        }
    }

    public static void EnrichDurations(IEnumerable<CatalogueTrack> tracks, MidiParserService parser)
    {
        foreach (var track in tracks)
            EnrichDuration(track, parser);
    }
}
