using System.Globalization;
using System.Windows.Data;
using WhereWindsMeetMidiPlayer.Models;

namespace WhereWindsMeetMidiPlayer.Converters;

public sealed class IsNowPlayingHighlightConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        var nowPlayingPath = values.Length > 1 ? values[1] as string : null;
        var nowPlayingCatalogueId = values.Length > 2 ? values[2] as string : null;
        var item = values.Length > 0 ? values[0] : null;

        return item switch
        {
            Song song => PathsMatch(song.FilePath, nowPlayingPath),
            HistoryItem history => PathsMatch(history.FilePath, nowPlayingPath),
            CatalogueTrack track => PathsMatch(track.CachedFilePath, nowPlayingPath)
                                    || CatalogueIdsMatch(track.Id, nowPlayingCatalogueId),
            _ => false
        };
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static bool PathsMatch(string? itemPath, string? nowPlayingPath) =>
        !string.IsNullOrWhiteSpace(itemPath)
        && !string.IsNullOrWhiteSpace(nowPlayingPath)
        && itemPath.Equals(nowPlayingPath, StringComparison.OrdinalIgnoreCase);

    private static bool CatalogueIdsMatch(string? trackId, string? nowPlayingCatalogueId) =>
        !string.IsNullOrWhiteSpace(trackId)
        && !string.IsNullOrWhiteSpace(nowPlayingCatalogueId)
        && trackId.Equals(nowPlayingCatalogueId, StringComparison.OrdinalIgnoreCase);
}