using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WhereWindsMeetMidiPlayer.Helpers;
using WhereWindsMeetMidiPlayer.Infrastructure;
using WhereWindsMeetMidiPlayer.Localization;
using WhereWindsMeetMidiPlayer.Models;
using WhereWindsMeetMidiPlayer.Services;

namespace WhereWindsMeetMidiPlayer.ViewModels;

public enum CommunitySortMode
{
    Newest,
    Title,
    Artist,
    Downloads
}

public sealed class CommunitySortOption
{
    public CommunitySortMode Mode { get; init; }
    public string Label { get; init; } = string.Empty;
}

/// <summary>Origin filter entry; null Origin = all sources.</summary>
public sealed class CommunityOriginOption
{
    public CommunityOrigin? Origin { get; init; }
    public string Label { get; init; } = string.Empty;
}

/// <summary>
/// Community page: solo MIDIs from bardmusicplayer.com merged with the Debra Discord catalogue.
/// Nothing touches the network at startup — the list loads from a local index when the page is
/// first opened, and only the Update button refreshes both sources.
/// </summary>
public partial class MainViewModel
{
    private readonly CommunityCatalogueService _communityCatalogue = new();
    private readonly CollectionViewSource _communityViewSource = new();
    private IReadOnlyList<CommunitySong> _communityBmpSongs = [];
    private bool _communityLoadRequested;
    private bool _communityViewRefreshScheduled;
    private bool _communityMergeScheduled;
    private CommunitySong? _nowPlayingCommunitySong;
    private CommunitySong? _lastSelectedCommunitySong;

    public BulkObservableCollection<CommunitySong> CommunitySongs { get; } = [];
    public BulkObservableCollection<string> CommunityGenres { get; } = [];
    public ObservableCollection<CommunitySortOption> CommunitySortOptions { get; } = [];
    public ObservableCollection<CommunityOriginOption> CommunityOriginOptions { get; } = [];

    [ObservableProperty] private string _communitySearchText = string.Empty;
    [ObservableProperty] private string? _communityGenreFilter;
    [ObservableProperty] private CommunityOriginOption? _selectedCommunityOrigin;
    [ObservableProperty] private CommunitySortOption? _selectedCommunitySortOption;
    [ObservableProperty] private CommunitySong? _selectedCommunitySong;
    [ObservableProperty] private string _communityStatsText = string.Empty;
    [ObservableProperty] private string _communityStatusText = string.Empty;
    [ObservableProperty] private bool _isCommunityLoading;

    public ICollectionView FilteredCommunitySongs => _communityViewSource.View;

    private void InitializeCommunity()
    {
        _communityViewSource.Source = CommunitySongs;
        _communityViewSource.View.Filter = FilterCommunitySong;
        CommunitySongs.CollectionChanged += (_, _) => ScheduleRefreshCommunityView();

        // Debra catalogue rows track the live catalogue list (startup sync, refresh).
        CatalogueTracks.CollectionChanged += (_, _) => ScheduleCommunityMerge();

        RebuildCommunityFilterOptions();
    }

    /// <summary>Idempotent; called when the Community page is first opened. Local cache only.</summary>
    public void EnsureCommunityLoaded()
    {
        if (_communityLoadRequested)
            return;

        _communityLoadRequested = true;
        _ = LoadCommunityFromCacheAsync();
    }

    private async Task LoadCommunityFromCacheAsync()
    {
        try
        {
            var cached = await Task.Run(() => _communityCatalogue.LoadIndex()).ConfigureAwait(false);
            await UiDispatcher.RunAsync(() =>
            {
                _communityBmpSongs = cached;
                ApplyCommunityMerge();
                CommunityStatusText = cached.Count == 0
                    ? L.T(UiText.CommunityEmptyHint)
                    : string.Empty;
            }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            AppPaths.WriteDiagnosticLog("community-load", ex);
        }
    }

    [RelayCommand]
    private async Task RefreshCommunity()
    {
        if (IsCommunityLoading)
            return;

        IsCommunityLoading = true;
        try
        {
            var progress = new Progress<(int Page, int TotalPages)>(p =>
                CommunityStatusText = L.F(UiText.CommunityFetchProgress, p.Page, p.TotalPages));
            var bmp = await _communityCatalogue.FetchBmpSoloCatalogueAsync(progress).ConfigureAwait(false);
            await Task.Run(() => _communityCatalogue.SaveIndex(bmp)).ConfigureAwait(false);

            await UiDispatcher.RunAsync(() => _communityBmpSongs = bmp).ConfigureAwait(false);

            // Refresh the Debra catalogue too, so one button updates both sources.
            await FetchCatalogueFromDiscordAsync(showErrors: false).ConfigureAwait(false);

            await UiDispatcher.RunAsync(() =>
            {
                ApplyCommunityMerge();
                CommunityStatusText = L.F(UiText.CommunityLoaded, CommunitySongs.Count);
            }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            AppPaths.WriteDiagnosticLog("community-refresh", ex);
            await UiDispatcher.RunAsync(() =>
                CommunityStatusText = L.F(UiText.CommunityError, ExceptionMessageHelper.FormatUserMessage(ex))
            ).ConfigureAwait(false);
        }
        finally
        {
            await UiDispatcher.RunAsync(() => IsCommunityLoading = false).ConfigureAwait(false);
        }
    }

    private void ScheduleCommunityMerge()
    {
        if (!_communityLoadRequested || _communityMergeScheduled)
            return;

        _communityMergeScheduled = true;
        UiDispatcher.Post(() =>
        {
            _communityMergeScheduled = false;
            ApplyCommunityMerge();
        });
    }

    private void ApplyCommunityMerge()
    {
        var merged = new List<CommunitySong>(CatalogueTracks.Count + _communityBmpSongs.Count);
        foreach (var track in CatalogueTracks)
        {
            merged.Add(new CommunitySong
            {
                Key = $"debra:{track.Id}",
                Origin = CommunityOrigin.Debra,
                Title = track.Title,
                Creator = "Debra",
                Genre = DebraStyleToGenre(track.StyleName),
                DurationMs = track.DurationMs,
                CreatedAt = track.PostedAt,
                DebraTrack = track
            });
        }

        merged.AddRange(_communityBmpSongs);

        var selectedKey = SelectedCommunitySong?.Key;
        CommunitySongs.ReplaceAll(merged);
        RebuildCommunityGenres();
        ApplyCommunitySort();
        if (selectedKey is not null)
            SelectedCommunitySong = CommunitySongs.FirstOrDefault(s => s.Key == selectedKey);
    }

    /// <summary>Debra style channels ("music-dl-anime") share the BMP genre vocabulary in the filter.</summary>
    private static string DebraStyleToGenre(string styleName)
    {
        if (string.IsNullOrWhiteSpace(styleName))
            return CommunityGenreMap.UnknownGenre;

        var s = styleName.ToLowerInvariant();
        if (s.Contains("anime")) return "Anime";
        if (s.Contains("kpop") || s.Contains("k-pop")) return "K-Pop";
        if (s.Contains("jpop") || s.Contains("j-pop")) return "J-Pop";
        if (s.Contains("classic")) return "Classical";
        if (s.Contains("movie") || s.Contains("series") || s.Contains("film")) return "Movies & TV";
        if (s.Contains("game") || s.Contains("gaming")) return "Video Games";
        if (s.Contains("metal")) return "Metal";
        if (s.Contains("rock")) return "Rock";
        if (s.Contains("jazz")) return "Jazz";
        if (s.Contains("electro")) return "Electronic";
        if (s.Contains("hip-hop") || s.Contains("hiphop") || s.Contains("rap") || s.Contains("r&b")) return "Hip-Hop & R&B";
        if (s.Contains("disney") || s.Contains("musical")) return "Musicals & Disney";
        if (s.Contains("christmas") || s.Contains("holiday") || s.Contains("noel") || s.Contains("noël")) return "Holiday";
        if (s.Contains("vocaloid")) return "Vocaloid";
        if (s.Contains("wuxia") || s.Contains("folk") || s.Contains("tradition") || s.Contains("ambiance")) return "Folk & Traditional";
        if (s.Contains("pop")) return "Pop";
        if (s.Contains("ost") || s.Contains("soundtrack")) return "Movies & TV";
        return CommunityGenreMap.UnknownGenre;
    }

    private void RebuildCommunityGenres()
    {
        var wasAll = IsAllCommunityGenres(CommunityGenreFilter);
        var previous = CommunityGenreFilter;

        var genres = CommunitySongs
            .Select(s => s.Genre)
            .Where(g => !string.IsNullOrWhiteSpace(g))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var list = new List<string>(1 + genres.Count) { AllCommunityGenresLabel };
        list.AddRange(genres);
        CommunityGenres.ReplaceAll(list);

        CommunityGenreFilter = !wasAll && previous is not null && list.Contains(previous, StringComparer.OrdinalIgnoreCase)
            ? previous
            : AllCommunityGenresLabel;
    }

    private static string AllCommunityGenresLabel => L.T(UiText.CommunityAllGenres);

    private static bool IsAllCommunityGenres(string? value) =>
        LocalizationService.Instance.MatchesAnyTranslation(UiText.CommunityAllGenres, value);

    private bool FilterCommunitySong(object obj)
    {
        if (obj is not CommunitySong song)
            return false;

        if (SelectedCommunityOrigin?.Origin is { } origin && song.Origin != origin)
            return false;

        if (!IsAllCommunityGenres(CommunityGenreFilter) &&
            !song.Genre.Equals(CommunityGenreFilter, StringComparison.OrdinalIgnoreCase))
            return false;

        var query = CommunitySearchText;
        if (string.IsNullOrWhiteSpace(query))
            return true;

        query = query.Trim();
        return song.DisplayTitle.Contains(query, StringComparison.OrdinalIgnoreCase)
               || song.Artist.Contains(query, StringComparison.OrdinalIgnoreCase)
               || song.Creator.Contains(query, StringComparison.OrdinalIgnoreCase)
               || song.SourceWork.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private void ScheduleRefreshCommunityView()
    {
        if (_communityViewRefreshScheduled)
            return;

        _communityViewRefreshScheduled = true;
        UiDispatcher.Post(() =>
        {
            _communityViewRefreshScheduled = false;
            _communityViewSource.View.Refresh();
            RefreshCommunityStats();
        });
    }

    private void RefreshCommunityStats()
    {
        var unfiltered = IsAllCommunityGenres(CommunityGenreFilter)
                         && string.IsNullOrWhiteSpace(CommunitySearchText)
                         && SelectedCommunityOrigin?.Origin is null;
        var count = unfiltered
            ? CommunitySongs.Count
            : _communityViewSource.View.Cast<object>().Count();
        CommunityStatsText = L.F(UiText.CommunityStats, count);
    }

    private void ApplyCommunitySort()
    {
        var view = _communityViewSource.View;
        using (view.DeferRefresh())
        {
            view.SortDescriptions.Clear();
            switch (SelectedCommunitySortOption?.Mode ?? CommunitySortMode.Newest)
            {
                case CommunitySortMode.Title:
                    view.SortDescriptions.Add(new SortDescription(nameof(CommunitySong.DisplayTitle), ListSortDirection.Ascending));
                    break;
                case CommunitySortMode.Artist:
                    view.SortDescriptions.Add(new SortDescription(nameof(CommunitySong.DisplayArtist), ListSortDirection.Ascending));
                    view.SortDescriptions.Add(new SortDescription(nameof(CommunitySong.DisplayTitle), ListSortDirection.Ascending));
                    break;
                case CommunitySortMode.Downloads:
                    view.SortDescriptions.Add(new SortDescription(nameof(CommunitySong.Downloads), ListSortDirection.Descending));
                    view.SortDescriptions.Add(new SortDescription(nameof(CommunitySong.DisplayTitle), ListSortDirection.Ascending));
                    break;
                default:
                    view.SortDescriptions.Add(new SortDescription(nameof(CommunitySong.CreatedAtSortTicks), ListSortDirection.Descending));
                    view.SortDescriptions.Add(new SortDescription(nameof(CommunitySong.DisplayTitle), ListSortDirection.Ascending));
                    break;
            }
        }

        RefreshCommunityStats();
    }

    private void RebuildCommunityFilterOptions()
    {
        var sortMode = SelectedCommunitySortOption?.Mode ?? CommunitySortMode.Newest;
        CommunitySortOptions.Clear();
        CommunitySortOptions.Add(new CommunitySortOption { Mode = CommunitySortMode.Newest, Label = L.T(UiText.CommunitySortNewest) });
        CommunitySortOptions.Add(new CommunitySortOption { Mode = CommunitySortMode.Title, Label = L.T(UiText.CommunitySortTitle) });
        CommunitySortOptions.Add(new CommunitySortOption { Mode = CommunitySortMode.Artist, Label = L.T(UiText.CommunitySortArtist) });
        CommunitySortOptions.Add(new CommunitySortOption { Mode = CommunitySortMode.Downloads, Label = L.T(UiText.CommunitySortDownloads) });
        SelectedCommunitySortOption = CommunitySortOptions.First(o => o.Mode == sortMode);

        var origin = SelectedCommunityOrigin?.Origin;
        CommunityOriginOptions.Clear();
        CommunityOriginOptions.Add(new CommunityOriginOption { Origin = null, Label = L.T(UiText.CommunityAllOrigins) });
        CommunityOriginOptions.Add(new CommunityOriginOption { Origin = CommunityOrigin.Debra, Label = L.T(UiText.CommunityOriginDebra) });
        CommunityOriginOptions.Add(new CommunityOriginOption { Origin = CommunityOrigin.Bmp, Label = L.T(UiText.CommunityOriginBmp) });
        SelectedCommunityOrigin = CommunityOriginOptions.First(o => o.Origin == origin);
    }

    /// <summary>Language switch: rebuild option labels and re-key the "All genres" entry.</summary>
    private void RefreshCommunityLocalization()
    {
        RebuildCommunityFilterOptions();
        if (CommunityGenres.Count > 0)
        {
            var wasAll = IsAllCommunityGenres(CommunityGenreFilter);
            CommunityGenres[0] = AllCommunityGenresLabel;
            if (wasAll)
                CommunityGenreFilter = AllCommunityGenresLabel;
        }

        RefreshCommunityStats();
    }

    partial void OnCommunitySearchTextChanged(string value) => ScheduleRefreshCommunityView();

    partial void OnCommunityGenreFilterChanged(string? value) => ScheduleRefreshCommunityView();

    partial void OnSelectedCommunityOriginChanged(CommunityOriginOption? value) => ScheduleRefreshCommunityView();

    partial void OnSelectedCommunitySortOptionChanged(CommunitySortOption? value)
    {
        if (value is not null)
            ApplyCommunitySort();
    }

    partial void OnSelectedCommunitySongChanged(CommunitySong? value)
    {
        if (value is not null)
        {
            _lastSelectedCommunitySong = value;
            OnPrimaryListItemSelected(PrimarySelectionSource.Community);
        }
        else if (_primarySelection == PrimarySelectionSource.Community)
        {
            _primarySelection = PrimarySelectionSource.None;
        }
    }

    private List<CommunitySong> GetFilteredCommunitySongs() =>
        _communityViewSource.View.Cast<CommunitySong>().ToList();

    /// <summary>Explicit community click, else the now-playing row, else last remembered.</summary>
    private CommunitySong? GetNavigationCommunitySong() =>
        SelectedCommunitySong ?? _nowPlayingCommunitySong ?? _lastSelectedCommunitySong;

    private void SetActivePlaybackContext(ActivePlaybackList list, CommunitySong song)
    {
        _activePlaybackList = list;
        _activeListIndex = FindCommunityIndex(GetFilteredCommunitySongs(), song);
    }

    private static int FindCommunityIndex(IReadOnlyList<CommunitySong> songs, CommunitySong song)
    {
        for (var i = 0; i < songs.Count; i++)
        {
            if (songs[i].Key == song.Key)
                return i;
        }

        return -1;
    }

    private int ResolveCommunityListIndex(IReadOnlyList<CommunitySong> songs, CommunitySong? selected)
    {
        selected ??= GetNavigationCommunitySong();

        if (_activePlaybackList == ActivePlaybackList.Community &&
            _activeListIndex >= 0 &&
            _activeListIndex < songs.Count)
        {
            return _activeListIndex;
        }

        if (selected is not null)
        {
            var selectedIndex = FindCommunityIndex(songs, selected);
            if (selectedIndex >= 0)
                return selectedIndex;
        }

        if (_nowPlayingCommunitySong is not null)
        {
            var playingIndex = FindCommunityIndex(songs, _nowPlayingCommunitySong);
            if (playingIndex >= 0)
                return playingIndex;
        }

        return songs.Count > 0 ? 0 : -1;
    }

    [RelayCommand]
    private async Task PlayCommunitySong(CommunitySong? song)
    {
        song ??= GetNavigationCommunitySong();
        if (song is null)
            return;

        try
        {
            CommunityStatusText = L.F(UiText.CommunityDownloading, song.DisplayTitle);
            var path = song.Origin == CommunityOrigin.Debra && song.DebraTrack is not null
                ? await _discordCatalogue.ResolvePlayablePathAsync(song.DebraTrack, _discordCredentials?.BotToken)
                : await _communityCatalogue.DownloadToCacheAsync(song);

            var librarySong = await Task.Run(() =>
                _library.AddFile(path, SmartTranspose, StrictNoteRange, song.DisplayTitle));
            if (song.DurationMs <= 0)
                song.DurationMs = librarySong.DurationMs;

            _nowPlayingCommunitySong = song;
            SetPrimaryListSelection(PrimarySelectionSource.Community, null, communitySong: song);
            SetActivePlaybackContext(ActivePlaybackList.Community, song);
            CommunityStatusText = string.Empty;
            await StartSongAsync(librarySong);
        }
        catch (Exception ex)
        {
            AppPaths.WriteDiagnosticLog("community-play", ex);
            CommunityStatusText = L.F(UiText.CommunityError, ExceptionMessageHelper.FormatUserMessage(ex));
        }
    }
}
