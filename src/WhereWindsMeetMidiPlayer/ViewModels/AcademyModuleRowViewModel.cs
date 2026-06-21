using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WhereWindsMeetMidiPlayer.Models;

namespace WhereWindsMeetMidiPlayer.ViewModels;

public partial class AcademyModuleRowViewModel : ObservableObject
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Badge { get; init; }
    public bool ComingSoon { get; init; }

    [ObservableProperty] private bool _isComplete;
    [ObservableProperty] private bool _isSelected;
}

public partial class AcademyLessonRowViewModel : ObservableObject
{
    public required AcademyLesson Lesson { get; init; }
    public required string KindLabel { get; init; }
    public required string HandLabel { get; init; }

    [ObservableProperty] private bool _isComplete;

    public bool CanStart => !Lesson.ComingSoon &&
        (Lesson.Kind == AcademyLessonKind.Guide ||
         !string.IsNullOrWhiteSpace(Lesson.BundledMidiPath) ||
         Lesson.Discord is not null);
}
