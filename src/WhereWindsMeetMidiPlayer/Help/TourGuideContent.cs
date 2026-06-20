using WhereWindsMeetMidiPlayer.Localization;
using WhereWindsMeetMidiPlayer.ViewModels;

namespace WhereWindsMeetMidiPlayer.Help;

public static class TourGuideContent
{
    public static IReadOnlyList<TourStep> GetSteps() =>
    [
        new TourStep
        {
            Title = L.T("Tour_01_Title"),
            Description = L.T("Tour_01_Desc"),
            Placement = TourCalloutPlacement.Center
        },
        new TourStep
        {
            Title = L.T("Tour_02_Title"),
            Description = L.T("Tour_02_Desc"),
            TargetName = "TourTarget_Sidebar"
        },
        new TourStep
        {
            Title = L.T("Tour_03_Title"),
            Description = L.T("Tour_03_Desc"),
            TargetName = "TourTarget_Header",
            Placement = TourCalloutPlacement.Below
        },
        new TourStep
        {
            Title = L.T("Tour_04_Title"),
            Description = L.T("Tour_04_Desc"),
            TargetName = "LibraryDropTarget",
            ShowSection = NavigationSection.Library
        },
        new TourStep
        {
            Title = L.T("Tour_05_Title"),
            Description = L.T("Tour_05_Desc"),
            TargetName = "TourTarget_Catalogue",
            ShowSection = NavigationSection.Catalogue
        },
        new TourStep
        {
            Title = L.T("Tour_06_Title"),
            Description = L.T("Tour_06_Desc"),
            TargetName = "TourTarget_Playlist",
            ShowSection = NavigationSection.Catalogue
        },
        new TourStep
        {
            Title = L.T("Tour_07_Title"),
            Description = L.T("Tour_07_Desc"),
            TargetName = "FavoritesDropTarget",
            ShowSection = NavigationSection.Favorites
        },
        new TourStep
        {
            Title = L.T("Tour_08_Title"),
            Description = L.T("Tour_08_Desc"),
            TargetName = "TourTarget_History",
            ShowSection = NavigationSection.History
        },
        new TourStep
        {
            Title = L.T("Tour_09_Title"),
            Description = L.T("Tour_09_Desc"),
            TargetName = "TourTarget_NowPlaying",
            ShowSection = NavigationSection.Catalogue
        },
        new TourStep
        {
            Title = L.T("Tour_10_Title"),
            Description = L.T("Tour_10_Desc"),
            TargetName = "TourTarget_PlayerChrome"
        },
        new TourStep
        {
            Title = L.T("Tour_14_Title"),
            Description = L.T("Tour_14_Desc"),
            TargetName = "TourTarget_PlayerMore",
            Placement = TourCalloutPlacement.Above
        },
        new TourStep
        {
            Title = L.T("Tour_11_Title"),
            Description = L.T("Tour_11_Desc"),
            TargetName = "TourTarget_Settings",
            ShowSection = NavigationSection.Settings
        },
        new TourStep
        {
            Title = L.T("Tour_12_Title"),
            Description = L.T("Tour_12_Desc"),
            Placement = TourCalloutPlacement.Center
        },
        new TourStep
        {
            Title = L.T("Tour_13_Title"),
            Description = L.T("Tour_13_Desc"),
            TargetName = "TourTarget_HelpButton",
            Placement = TourCalloutPlacement.Below
        }
    ];

    [Obsolete("Use GetSteps() so strings follow the active UI language.")]
    public static IReadOnlyList<TourStep> Steps => GetSteps();
}
