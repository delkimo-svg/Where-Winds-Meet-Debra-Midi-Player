using System.ComponentModel;
using System.Windows.Data;
using WhereWindsMeetMidiPlayer.Models;

namespace WhereWindsMeetMidiPlayer.Helpers;

internal static class SongListSortHelper
{
    public static void Apply(CollectionViewSource source, SongListSortMode mode)
    {
        var view = source.View;
        view.SortDescriptions.Clear();

        switch (mode)
        {
            case SongListSortMode.Name:
                view.SortDescriptions.Add(new SortDescription(nameof(Song.DisplayTitle), ListSortDirection.Ascending));
                break;
            case SongListSortMode.TimeAddedNewest:
                view.SortDescriptions.Add(new SortDescription(nameof(Song.AddedAt), ListSortDirection.Descending));
                break;
            case SongListSortMode.TimeAddedOldest:
                view.SortDescriptions.Add(new SortDescription(nameof(Song.AddedAt), ListSortDirection.Ascending));
                break;
        }
    }

    public static void ApplyCatalogueSort(CollectionViewSource source, CatalogueSortMode mode, bool allStyles)
    {
        var view = source.View;
        view.SortDescriptions.Clear();

        switch (mode)
        {
            case CatalogueSortMode.Alphabetical:
                if (allStyles)
                    view.SortDescriptions.Add(new SortDescription(nameof(CatalogueTrack.StyleSortOrder), ListSortDirection.Ascending));
                view.SortDescriptions.Add(new SortDescription(nameof(CatalogueTrack.DisplayTitle), ListSortDirection.Ascending));
                break;
            default:
                view.SortDescriptions.Add(new SortDescription(nameof(CatalogueTrack.PostedAtSortTicks), ListSortDirection.Descending));
                if (allStyles)
                    view.SortDescriptions.Add(new SortDescription(nameof(CatalogueTrack.StyleSortOrder), ListSortDirection.Ascending));
                view.SortDescriptions.Add(new SortDescription(nameof(CatalogueTrack.DisplayTitle), ListSortDirection.Ascending));
                break;
        }
    }
}
