using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WhereWindsMeetMidiPlayer.Helpers;
using WhereWindsMeetMidiPlayer.Infrastructure;
using WhereWindsMeetMidiPlayer.Localization;
using WhereWindsMeetMidiPlayer.Models;
using WhereWindsMeetMidiPlayer.Services;
using WhereWindsMeetMidiPlayer.Services.Discord;

namespace WhereWindsMeetMidiPlayer.ViewModels;

public sealed partial class AcademyPanelViewModel : ObservableObject
{
    private readonly AcademyService _academy = new();
    private readonly DiscordAcademyService _discordAcademy = new();
    private readonly Func<DiscordCredentials?> _getCredentials;
    private readonly Func<IReadOnlyCollection<string>> _getCompletedLessons;
    private readonly Func<(string? ModuleId, string? ExerciseId, string? SongId, string? LessonId)> _getLastSelections;
    private readonly Action<string?, string?, string?, string?> _saveLastSelections;
    private readonly Action<string> _markLessonComplete;
    private readonly Func<AcademyLesson, AcademyModule, Task> _previewLessonAsync;
    private readonly Func<AcademyLesson, AcademyModule, Task> _readyLessonAsync;
    private readonly Func<AcademyLesson, AcademyModule, Task> _listenLessonAsync;
    private readonly Action _closeOverlay;
    private readonly Func<bool> _isAcademyOverlayOpen;

    private AcademyLessonRowViewModel? _activeLessonRow;
    private bool _suppressLessonPreview;

    public ObservableCollection<AcademyModuleRowViewModel> Modules { get; } = [];

    [ObservableProperty] private AcademyModuleRowViewModel? _selectedModule;
    [ObservableProperty] private AcademyLessonRowViewModel? _selectedExerciseLesson;
    [ObservableProperty] private AcademyLessonRowViewModel? _selectedSongLesson;
    [ObservableProperty] private string _moduleGuideText = string.Empty;
    [ObservableProperty] private string _statusText = string.Empty;
    [ObservableProperty] private string _activeLessonGuide = string.Empty;
    [ObservableProperty] private AcademyHand _activeLessonHand = AcademyHand.Both;
    [ObservableProperty] private bool _hasActiveLesson;
    [ObservableProperty] private bool _isLoading;

    public ObservableCollection<AcademyLessonRowViewModel> ExerciseLessons { get; } = [];
    public ObservableCollection<AcademyLessonRowViewModel> SongLessons { get; } = [];

    public AcademyPanelViewModel(
        Func<DiscordCredentials?> getCredentials,
        Func<IReadOnlyCollection<string>> getCompletedLessons,
        Action<string> markLessonComplete,
        Func<AcademyLesson, AcademyModule, Task> previewLessonAsync,
        Func<AcademyLesson, AcademyModule, Task> readyLessonAsync,
        Func<AcademyLesson, AcademyModule, Task> listenLessonAsync,
        Action closeOverlay,
        Func<bool> isAcademyOverlayOpen,
        Func<(string? ModuleId, string? ExerciseId, string? SongId, string? LessonId)> getLastSelections,
        Action<string?, string?, string?, string?> saveLastSelections)
    {
        _getCredentials = getCredentials;
        _getCompletedLessons = getCompletedLessons;
        _markLessonComplete = markLessonComplete;
        _previewLessonAsync = previewLessonAsync;
        _readyLessonAsync = readyLessonAsync;
        _listenLessonAsync = listenLessonAsync;
        _closeOverlay = closeOverlay;
        _isAcademyOverlayOpen = isAcademyOverlayOpen;
        _getLastSelections = getLastSelections;
        _saveLastSelections = saveLastSelections;
    }

    public void EnsureLoaded()
    {
        if (_academy.Current is null || _academy.Current.Modules.Count == 0)
        {
            _academy.TryLoadCache();
            if (_academy.Current is null || _academy.Current.Modules.Count == 0)
            {
                _academy.LoadBundled();
                if (_academy.Current is not null && _academy.Current.Modules.Count > 0)
                    _academy.SaveCache(_academy.Current);
            }
        }

        _academy.EnrichFromBundled();

        if (Modules.Count > 0)
        {
            PlayExerciseCommand.NotifyCanExecuteChanged();
            PlaySongCommand.NotifyCanExecuteChanged();
            return;
        }

        RebuildFromManifest();
        StatusText = _academy.Current is not null && Modules.Count > 0
            ? L.T(UiText.AcademyStatusBundled)
            : L.T(UiText.AcademyStatusMissingBundled);
    }

    [RelayCommand]
    private void CloseOverlay() => _closeOverlay();

    [RelayCommand]
    private async Task RefreshAsync()
    {
        var creds = _getCredentials();
        if (creds is null ||
            string.IsNullOrWhiteSpace(creds.BotToken) ||
            string.IsNullOrWhiteSpace(creds.AcademyManifestChannelId) ||
            string.IsNullOrWhiteSpace(creds.AcademyManifestMessageId))
        {
            StatusText = L.T(UiText.AcademyStatusNoDiscord);
            return;
        }

        IsLoading = true;
        StatusText = L.T(UiText.AcademyStatusLoading);
        try
        {
            var manifest = await _discordAcademy.FetchManifestFromDiscordAsync(creds).ConfigureAwait(true);
            _academy.ApplyRemote(manifest);
            _academy.SaveCache(manifest);
            RebuildFromManifest();
            StatusText = L.T(UiText.AcademyStatusUpdated);
        }
        catch (Exception ex)
        {
            AppPaths.WriteDiagnosticLog("academy-refresh", ex);
            StatusText = L.T(UiText.AcademyStatusError);
            DebraDialogs.Warning(L.T(UiText.SectionAcademy), ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task ReadyAsync()
    {
        if (SelectedModule is null)
            return;

        var row = _activeLessonRow ?? SelectedExerciseLesson ?? SelectedSongLesson;
        if (row is null)
            return;

        if (!row.CanStart)
        {
            DebraDialogs.Info(L.T(UiText.SectionAcademy), L.T(UiText.AcademyLessonComingSoon));
            return;
        }

        var module = _academy.FindModule(SelectedModule.Id);
        if (module is null)
            return;

        await _readyLessonAsync(row.Lesson, module).ConfigureAwait(true);
    }

    [RelayCommand]
    private void MarkLessonComplete()
    {
        var row = _activeLessonRow ?? SelectedExerciseLesson ?? SelectedSongLesson;
        if (row is null)
            return;

        _markLessonComplete(row.Lesson.Id);
        row.IsComplete = true;
        RefreshModuleCompletion(SelectedModule);
    }

    partial void OnSelectedModuleChanged(AcademyModuleRowViewModel? value)
    {
        ExerciseLessons.Clear();
        SongLessons.Clear();
        SelectedExerciseLesson = null;
        SelectedSongLesson = null;
        _activeLessonRow = null;
        HasActiveLesson = false;
        ActiveLessonGuide = string.Empty;
        ActiveLessonHand = AcademyHand.Both;

        if (value is null)
        {
            ModuleGuideText = string.Empty;
            return;
        }

        var module = _academy.FindModule(value.Id);
        if (module is null)
            return;

        ModuleGuideText = module.Guide ?? string.Empty;
        var completed = _getCompletedLessons();
        foreach (var lesson in module.Lessons.OrderBy(l => l.Order))
        {
            var row = new AcademyLessonRowViewModel
            {
                Lesson = lesson,
                KindLabel = FormatKind(lesson.Kind),
                HandLabel = FormatHand(lesson.Hand),
                IsComplete = completed.Contains(lesson.Id)
            };

            switch (lesson.Kind)
            {
                case AcademyLessonKind.Exercise:
                    ExerciseLessons.Add(row);
                    break;
                case AcademyLessonKind.Song:
                    SongLessons.Add(row);
                    break;
            }
        }

        var saved = _getLastSelections();
        _suppressLessonPreview = true;
        SelectedExerciseLesson = PickLessonRow(ExerciseLessons, saved.ExerciseId) ?? ExerciseLessons.FirstOrDefault();
        SelectedSongLesson = PickLessonRow(SongLessons, saved.SongId) ?? SongLessons.FirstOrDefault();
        _suppressLessonPreview = false;

        var previewRow = PickLessonRow(ExerciseLessons.Concat(SongLessons), saved.LessonId)
            ?? SelectedExerciseLesson
            ?? SelectedSongLesson;
        if (previewRow is not null)
            ActivateLessonRow(previewRow, persist: false, autoPlayListen: false);

        PersistSelections(previewRow?.Lesson.Id);
        PlayExerciseCommand.NotifyCanExecuteChanged();
        PlaySongCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanPlaySelectedExercise))]
    private async Task PlayExerciseAsync()
    {
        if (SelectedExerciseLesson is null || SelectedModule is null)
            return;

        var module = _academy.FindModule(SelectedModule.Id);
        if (module is null)
            return;

        await ReplayLessonListenAsync(SelectedExerciseLesson, module).ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(CanPlaySelectedSong))]
    private async Task PlaySongAsync()
    {
        if (SelectedSongLesson is null || SelectedModule is null)
            return;

        var module = _academy.FindModule(SelectedModule.Id);
        if (module is null)
            return;

        await ReplayLessonListenAsync(SelectedSongLesson, module).ConfigureAwait(true);
    }

    private bool CanPlaySelectedExercise => SelectedExerciseLesson?.CanStart == true;

    private bool CanPlaySelectedSong => SelectedSongLesson?.CanStart == true;

    partial void OnSelectedExerciseLessonChanged(AcademyLessonRowViewModel? value)
    {
        PlayExerciseCommand.NotifyCanExecuteChanged();
        if (value is null || _suppressLessonPreview)
            return;

        ActivateLessonRow(value, persist: true, autoPlayListen: false);
    }

    partial void OnSelectedSongLessonChanged(AcademyLessonRowViewModel? value)
    {
        PlaySongCommand.NotifyCanExecuteChanged();
        if (value is null || _suppressLessonPreview)
            return;

        ActivateLessonRow(value, persist: true, autoPlayListen: false);
    }

    private void ActivateLessonRow(AcademyLessonRowViewModel row, bool persist, bool autoPlayListen)
    {
        _activeLessonRow = row;
        UpdateActiveLessonContext(row);
        if (row.Lesson.Kind == AcademyLessonKind.Guide)
            return;

        if (persist)
            PersistSelections(row.Lesson.Id);

        if (SelectedModule is null)
            return;

        var module = _academy.FindModule(SelectedModule.Id);
        if (module is null)
            return;

        _ = RunLessonActivationAsync(row, module, autoPlayListen);
    }

    public async Task ActivateCurrentLessonPreviewAsync(bool autoPlayListen)
    {
        var row = _activeLessonRow ?? SelectedExerciseLesson ?? SelectedSongLesson;
        if (row is null || SelectedModule is null)
            return;

        var module = _academy.FindModule(SelectedModule.Id);
        if (module is null)
            return;

        await RunLessonActivationAsync(row, module, autoPlayListen).ConfigureAwait(true);
    }

    private void UpdateActiveLessonContext(AcademyLessonRowViewModel? row)
    {
        if (row is null || row.Lesson.Kind == AcademyLessonKind.Guide)
        {
            HasActiveLesson = false;
            ActiveLessonGuide = string.Empty;
            ActiveLessonHand = AcademyHand.Both;
            return;
        }

        HasActiveLesson = true;
        ActiveLessonGuide = row.Lesson.Guide ?? string.Empty;
        ActiveLessonHand = row.Lesson.Hand;
    }

    private async Task RunLessonActivationAsync(
        AcademyLessonRowViewModel row,
        AcademyModule module,
        bool autoPlayListen)
    {
        await PreviewSelectedLessonAsync(row.Lesson, module).ConfigureAwait(true);
        if (autoPlayListen && row.CanStart)
            await _listenLessonAsync(row.Lesson, module).ConfigureAwait(true);
    }

    private async Task ReplayLessonListenAsync(AcademyLessonRowViewModel row, AcademyModule module)
    {
        if (!row.CanStart)
            return;

        await _listenLessonAsync(row.Lesson, module).ConfigureAwait(true);
    }

    public bool CanAdvanceToNextLesson(AcademyLesson? current) => GetNextLessonRow(current) is not null;

    public bool CanAdvanceToNextExercise(AcademyLesson? current)
    {
        var next = GetNextLessonRow(current);
        return next?.Lesson.Kind == AcademyLessonKind.Exercise;
    }

    public bool ModuleHasPracticeSongs() => SongLessons.Any(r => r.CanStart);

    public async Task AdvanceToNextLessonAsync(AcademyLesson? current)
    {
        var next = GetNextLessonRow(current);
        if (next is null)
            return;

        await SelectLessonRowAsync(next).ConfigureAwait(true);
    }

    public async Task AdvanceToNextExerciseAsync(AcademyLesson? current)
    {
        var next = GetNextLessonRow(current);
        if (next?.Lesson.Kind != AcademyLessonKind.Exercise)
            return;

        await SelectLessonRowAsync(next).ConfigureAwait(true);
    }

    private AcademyLessonRowViewModel? GetNextLessonRow(AcademyLesson? current)
    {
        if (current is null || SelectedModule is null)
            return null;

        var module = _academy.FindModule(SelectedModule.Id);
        if (module is null)
            return null;

        var playable = module.Lessons
            .Where(l => l.Kind != AcademyLessonKind.Guide && !l.ComingSoon)
            .OrderBy(l => l.Order)
            .ToList();

        var index = playable.FindIndex(l => l.Id == current.Id);
        if (index < 0 || index >= playable.Count - 1)
            return null;

        var nextId = playable[index + 1].Id;
        return ExerciseLessons.FirstOrDefault(r => r.Lesson.Id == nextId)
            ?? SongLessons.FirstOrDefault(r => r.Lesson.Id == nextId);
    }

    private async Task SelectLessonRowAsync(AcademyLessonRowViewModel row)
    {
        _suppressLessonPreview = true;
        switch (row.Lesson.Kind)
        {
            case AcademyLessonKind.Exercise:
                SelectedExerciseLesson = row;
                break;
            case AcademyLessonKind.Song:
                SelectedSongLesson = row;
                break;
        }
        _suppressLessonPreview = false;

        if (SelectedModule is null)
            return;

        var module = _academy.FindModule(SelectedModule.Id);
        if (module is null)
            return;

        await RunLessonActivationAsync(row, module, autoPlayListen: false).ConfigureAwait(true);
    }

    private void PersistSelections(string? activeLessonId = null)
    {
        _saveLastSelections(
            SelectedModule?.Id,
            SelectedExerciseLesson?.Lesson.Id,
            SelectedSongLesson?.Lesson.Id,
            activeLessonId ?? _activeLessonRow?.Lesson.Id);
    }

    private async Task PreviewSelectedLessonAsync(AcademyLesson lesson, AcademyModule module)
    {
        try
        {
            await _previewLessonAsync(lesson, module).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            AppPaths.WriteDiagnosticLog("academy-preview", ex);
        }
    }

    private void RebuildFromManifest()
    {
        Modules.Clear();
        ExerciseLessons.Clear();
        SongLessons.Clear();
        SelectedModule = null;
        SelectedExerciseLesson = null;
        SelectedSongLesson = null;
        _activeLessonRow = null;
        ModuleGuideText = string.Empty;

        if (_academy.Current is null)
            return;

        var completed = _getCompletedLessons();
        foreach (var module in _academy.Current.Modules.OrderBy(m => m.SortOrder))
        {
            var lessonIds = module.Lessons.Select(l => l.Id).ToList();
            var complete = lessonIds.Count > 0 && lessonIds.All(id => completed.Contains(id));
            Modules.Add(new AcademyModuleRowViewModel
            {
                Id = module.Id,
                Title = module.Title,
                Badge = module.Id,
                ComingSoon = module.ComingSoon,
                IsComplete = complete
            });
        }

        if (Modules.Count == 0)
            return;

        var saved = _getLastSelections();
        SelectedModule = Modules.FirstOrDefault(m => m.Id == saved.ModuleId)
            ?? Modules.FirstOrDefault(m => !m.ComingSoon)
            ?? Modules[0];
        PersistSelections();
    }

    private static AcademyLessonRowViewModel? PickLessonRow(
        IEnumerable<AcademyLessonRowViewModel> rows,
        string? lessonId)
    {
        if (string.IsNullOrWhiteSpace(lessonId))
            return null;

        return rows.FirstOrDefault(r => r.Lesson.Id.Equals(lessonId, StringComparison.OrdinalIgnoreCase));
    }

    private void RefreshModuleCompletion(AcademyModuleRowViewModel? moduleRow)
    {
        if (moduleRow is null)
            return;

        var module = _academy.FindModule(moduleRow.Id);
        if (module is null)
            return;

        var completed = _getCompletedLessons();
        moduleRow.IsComplete = module.Lessons.Count > 0 &&
            module.Lessons.All(l => completed.Contains(l.Id));
    }

    private static string FormatKind(AcademyLessonKind kind) =>
        kind switch
        {
            AcademyLessonKind.Exercise => L.T(UiText.AcademyKindExercise),
            AcademyLessonKind.Song => L.T(UiText.AcademyKindSong),
            _ => L.T(UiText.AcademyKindGuide)
        };

    private static string FormatHand(AcademyHand hand) =>
        hand switch
        {
            AcademyHand.Right => L.T(UiText.AcademyHandRight),
            AcademyHand.Left => L.T(UiText.AcademyHandLeft),
            AcademyHand.Both => L.T(UiText.AcademyHandBoth),
            _ => L.T(UiText.AcademyHandAny)
        };
}
