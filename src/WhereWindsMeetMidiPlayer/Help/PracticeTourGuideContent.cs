using WhereWindsMeetMidiPlayer.Localization;
using WhereWindsMeetMidiPlayer.ViewModels;

namespace WhereWindsMeetMidiPlayer.Help;

public static class PracticeTourGuideContent
{
    public static IReadOnlyList<TourStep> GetSteps() =>
    [
        new TourStep
        {
            Title = L.T("PracticeTour_01_Title"),
            Description = L.T("PracticeTour_01_Desc"),
            Placement = TourCalloutPlacement.Center,
            ShowSection = NavigationSection.Practice
        },
        new TourStep
        {
            Title = L.T("PracticeTour_02_Title"),
            Description = L.T("PracticeTour_02_Desc"),
            TargetName = "PracticeDropTarget",
            ShowSection = NavigationSection.Practice
        },
        new TourStep
        {
            Title = L.T("PracticeTour_03_Title"),
            Description = L.T("PracticeTour_03_Desc"),
            TargetName = "TourTarget_PracticeToolbar",
            ShowSection = NavigationSection.Practice,
            Placement = TourCalloutPlacement.Above
        },
        new TourStep
        {
            Title = L.T("PracticeTour_04_Title"),
            Description = L.T("PracticeTour_04_Desc"),
            TargetName = "TourTarget_PracticeLibraryBtn",
            ShowSection = NavigationSection.Practice,
            Placement = TourCalloutPlacement.Above
        },
        new TourStep
        {
            Title = L.T("PracticeTour_05_Title"),
            Description = L.T("PracticeTour_05_Desc"),
            TargetName = "TourTarget_PracticeAcademyBtn",
            ShowSection = NavigationSection.Practice,
            Placement = TourCalloutPlacement.Above
        },
        new TourStep
        {
            Title = L.T("PracticeTour_06_Title"),
            Description = L.T("PracticeTour_06_Desc"),
            TargetName = "TourTarget_PracticeTransport",
            ShowSection = NavigationSection.Practice,
            Placement = TourCalloutPlacement.Above
        },
        new TourStep
        {
            Title = L.T("PracticeTour_07_Title"),
            Description = L.T("PracticeTour_07_Desc"),
            Placement = TourCalloutPlacement.Center,
            ShowSection = NavigationSection.Practice
        }
    ];
}
