using CommunityToolkit.Mvvm.Input;
using WhereWindsMeetMidiPlayer.Helpers;
using WhereWindsMeetMidiPlayer.Infrastructure;
using WhereWindsMeetMidiPlayer.Localization;
using WhereWindsMeetMidiPlayer.Models;
using WhereWindsMeetMidiPlayer.Services;

namespace WhereWindsMeetMidiPlayer.ViewModels;

public partial class MainViewModel
{
    private const int AcademyPracticeTempoPercent = 115;

    private async Task ActivatePracticeAcademyOverlayAsync()
    {
        AcademyPanel.EnsureLoaded();
        await AcademyPanel.ActivateCurrentLessonPreviewAsync(autoPlayListen: false).ConfigureAwait(true);
    }

    private async Task RunPracticeCountdownAsync(bool showGoFlash = false)
    {
        var seconds = Math.Clamp(PracticeAcademyCountdownSeconds, 0, 30);
        if (seconds > 0)
        {
            IsPracticeCountdownActive = true;
            PracticeCountdownDisplay = L.T(UiText.PracticeGetReady);
            await Task.Delay(600).ConfigureAwait(true);

            for (var i = seconds; i > 0; i--)
            {
                PracticeCountdownDisplay = i.ToString();
                await Task.Delay(1000).ConfigureAwait(true);
            }
        }
        else if (!showGoFlash)
        {
            return;
        }

        IsPracticeCountdownActive = true;
        PracticeCountdownDisplay = L.T(UiText.PracticeGo);
        await Task.Delay(450).ConfigureAwait(true);
        IsPracticeCountdownActive = false;
        PracticeCountdownDisplay = string.Empty;
    }

    private AcademyLesson? _armedAcademyLesson;
    private AcademyHand _activeAcademyHand = AcademyHand.Any;
    private AcademyLessonKind _activeAcademyLessonKind = AcademyLessonKind.Guide;
    private string? _lastPreviewedAcademyLessonId;
    private bool _suppressAcademyPracticeSongReload;
    private AcademyLesson? _academyTourLesson;
    private AcademyModule? _academyTourModule;
    private List<AcademyTourStep> _academyTourSteps = [];
    private int _academyTourStepIndex;

    private void MarkAcademyLessonComplete(string lessonId)
    {
        if (string.IsNullOrWhiteSpace(lessonId))
            return;

        var list = _settings.Settings.CompletedAcademyLessonIds;
        if (list.Contains(lessonId))
            return;

        list.Add(lessonId);
        ScheduleSettingsSave();
    }

    private async Task PreviewAcademyLessonAsync(AcademyLesson lesson, AcademyModule module)
    {
        if (lesson.Kind == AcademyLessonKind.Guide)
            return;

        StopAcademyLessonPlayback();

        try
        {
            var path = await ResolveAcademyLessonPathAsync(lesson).ConfigureAwait(true);
            ApplyAcademyPracticePreset(lesson);
            IsAcademyPracticeMode = true;
            _activeAcademyHand = lesson.Hand;
            AcademyGuideText = BuildAcademyGuideText(module, lesson);

            var song = new Song
            {
                Title = $"{module.Id} · {lesson.Title}",
                FilePath = path,
                AddedAt = DateTime.UtcNow
            };

            _suppressAcademyPracticeSongReload = true;
            SelectedPracticeSong = song;
            _suppressAcademyPracticeSongReload = false;
            PracticeTitle = song.Title;
            await ReloadPracticeChartAsync(song).ConfigureAwait(true);
            ApplyAcademyLessonToPracticeChart(lesson);
            IsPracticeLessonArmed = false;
            _lastPreviewedAcademyLessonId = lesson.Id;
            OnPropertyChanged(nameof(PracticeFallingNoteLabelMode));
            RefreshPracticeFallingNoteLayout();
            if (IsPracticeAcademyOverlayOpen)
                StartAcademyTour(lesson, module);
        }
        catch (Exception ex)
        {
            AppPaths.WriteDiagnosticLog("academy-preview", ex);
            DebraDialogs.Error(L.T(UiText.SectionAcademy), ex.Message);
        }
    }

    private async Task ReadyAcademyLessonAsync(AcademyLesson lesson, AcademyModule module)
    {
        if (lesson.Kind == AcademyLessonKind.Guide)
        {
            MarkAcademyLessonComplete(lesson.Id);
            DebraDialogs.Info(L.T(UiText.SectionAcademy), L.T(UiText.AcademyGuideRead));
            return;
        }

        await PreviewAcademyLessonAsync(lesson, module).ConfigureAwait(true);

        if (_practiceSession.Notes.Count == 0)
        {
            DebraDialogs.Warning(L.T(UiText.SectionPractice), L.T(UiText.PracticeNoNotes));
            return;
        }

        _armedAcademyLesson = lesson;
        IsPracticeLessonArmed = true;
        IsPracticeAcademyOverlayOpen = false;
        OnPropertyChanged(nameof(ShowPracticeCenterPlay));
    }

    private async Task ListenAcademyLessonAsync(AcademyLesson lesson, AcademyModule module)
    {
        if (lesson.Kind == AcademyLessonKind.Guide || lesson.ComingSoon)
            return;

        try
        {
            if (_lastPreviewedAcademyLessonId != lesson.Id || _practiceSession.Notes.Count == 0)
                await PreviewAcademyLessonAsync(lesson, module).ConfigureAwait(true);
            else if (IsPracticeAcademyOverlayOpen)
                StartAcademyTour(lesson, module);

            if (_practiceSession.Notes.Count == 0)
            {
                DebraDialogs.Warning(L.T(UiText.SectionPractice), L.T(UiText.PracticeNoNotes));
                return;
            }

            await StartAcademyLessonPlayback().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            AppPaths.WriteDiagnosticLog("academy-listen", ex);
            DebraDialogs.Error(L.T(UiText.SectionAcademy), ex.Message);
        }
    }

    private void StopAcademyLessonPlayback()
    {
        _playback.Stop();
        _practiceSession.Stop();
        _practiceKeyboardPress.Clear();
        _practicePcKeysHeld.Clear();
        _practiceSound.ResetSession();
        IsPracticePlaying = false;
        PracticeProgress = 0;
        PracticeTimeText = "0:00";
        SyncPracticeSoundState();
    }

    private Task StartAcademyLessonPlayback()
    {
        StopAcademyLessonPlayback();

        _suppressPracticeModeSync = true;
        IsPracticeLearnMode = false;
        IsPracticeFollowMode = true;
        _practiceSession.Mode = PracticeMode.Follow;
        _suppressPracticeModeSync = false;

        IsPracticeLessonArmed = false;
        OnPropertyChanged(nameof(ShowPracticeCenterPlay));
        OnPropertyChanged(nameof(ShowPracticeHandPreview));

        SyncPracticeSoundState();
        _suppressPracticeTempoChange = true;
        PracticeTempoPercent = AcademyPracticeTempoPercent;
        _suppressPracticeTempoChange = false;
        _practiceSession.SetTempoPercent(PracticeTempoPercent);
        _practiceSession.SetPlaybackLeadInMs(PracticeSessionService.DefaultLeadInMs);
        _practiceSession.Start();
        return Task.CompletedTask;
    }

    private async Task<string> ResolveAcademyLessonPathAsync(AcademyLesson lesson)
    {
        var path = AcademyService.ResolveBundledMidiPath(lesson);
        if (path is not null)
            return path;

        _discordCredentials ??= DiscordCredentialStore.Load();
        if (_discordCredentials is null || string.IsNullOrWhiteSpace(_discordCredentials.BotToken))
            throw new InvalidOperationException(L.T(UiText.AcademyMidiMissing));

        return await _discordAcademy.ResolveLessonMidiPathAsync(
            lesson,
            _discordCredentials.BotToken).ConfigureAwait(true);
    }

    private void ApplyAcademyPracticePreset(AcademyLesson lesson)
    {
        _suppressPracticeViewSync = true;
        _suppressPracticeLabelSync = true;
        _suppressPracticeModeSync = true;

        IsPracticeFullPianoView = true;
        IsPracticeGameKeysView = false;
        IsPracticeLetterLabels = true;
        IsPracticeSolfegeLabels = false;
        IsPracticeKeyboardLabels = false;
        IsPracticeLearnMode = lesson.LearnMode;
        IsPracticeFollowMode = !lesson.LearnMode;

        _suppressPracticeTempoChange = true;
        PracticeTempoPercent = AcademyPracticeTempoPercent;
        _suppressPracticeTempoChange = false;

        _suppressPracticeViewSync = false;
        _suppressPracticeLabelSync = false;
        _suppressPracticeModeSync = false;

        OnPropertyChanged(nameof(PracticeKeyboardViewMode));
        OnPropertyChanged(nameof(PracticeNoteLabelMode));
        _practiceSession.Mode = lesson.LearnMode ? PracticeMode.Learn : PracticeMode.Follow;
    }

    private void ApplyAcademyTrackSelection(AcademyLesson lesson)
    {
        if (lesson.EnabledTracks is not { Length: > 0 })
            return;

        var enabled = lesson.EnabledTracks.ToHashSet();
        foreach (var option in PracticeTrackOptions)
            option.IsEnabled = enabled.Contains(option.TrackIndex);
    }

    private void ApplyAcademyLessonToPracticeChart(AcademyLesson lesson)
    {
        _activeAcademyHand = lesson.Hand;
        _activeAcademyLessonKind = lesson.Kind;
        OnPropertyChanged(nameof(ShowAcademyFingerLabelsOnNotes));
        OnPropertyChanged(nameof(PracticeFallingNoteLabelMode));

        ApplyAcademyHandTrackColors(lesson.Hand, lesson.Kind);
        ApplyAcademyTrackSelection(lesson);
        ApplyPracticeEnabledTracks();

        if (_practiceSession.Notes.Count == 0)
            return;

        var colored = ColorizePracticeVisualNotes(_practiceSession.Notes.ToList());
        _practiceSession.Load(colored, _practiceSession.DurationMs);
        RefreshPracticeHandKeyPreview();
        OnPropertyChanged(nameof(ShowPracticeHandTrackPicker));
        RefreshPracticeFallingNoteLayout();
    }

    private void ApplyAcademyHandTrackColors(AcademyHand hand, AcademyLessonKind kind)
    {
        if (kind == AcademyLessonKind.Song)
        {
            if (PracticeTrackOptions.Count == 2)
            {
                var notes = _practiceSession.Notes;
                var (right, left) = PracticeHandTrackLayout.Classify(
                    PracticeTrackOptions[0],
                    PracticeTrackOptions[1],
                    notes.Count > 0 ? notes : null);
                PracticeHandTrackLayout.ApplyHandColors(
                    right,
                    left,
                    PracticeHandColorResolver.RightHandHex,
                    PracticeHandColorResolver.LeftHandHex);
                _practiceRightHandTrack = right;
                _practiceLeftHandTrack = left;
            }

            return;
        }

        if (hand is AcademyHand.Left or AcademyHand.Right)
        {
            foreach (var option in PracticeTrackOptions)
                option.ColorHex = AcademyHandColors.ForHand(hand);
            return;
        }

        if (PracticeTrackOptions.Count == 2)
        {
            var notes = _practiceSession.Notes;
            var (right, left) = PracticeHandTrackLayout.Classify(
                PracticeTrackOptions[0],
                PracticeTrackOptions[1],
                notes.Count > 0 ? notes : null);
            PracticeHandTrackLayout.ApplyHandColors(right, left);
            _practiceRightHandTrack = right;
            _practiceLeftHandTrack = left;
        }
    }

    private void RefreshPracticeHandKeyPreview()
    {
        var preview = new PracticeHandKeyPreview();
        const int rightVirtualTrack = 0;
        const int leftVirtualTrack = 1;

        if (PracticeTrackOptions.Count == 2 && ShowPracticeHandTrackPicker)
        {
            foreach (var option in PracticeTrackOptions)
                preview.TrackColors[option.TrackIndex] = option.ColorHex;

            foreach (var note in _practiceSession.Notes)
            {
                if (!PracticeTrackOptions.Any(t => t.TrackIndex == note.TrackIndex && t.IsEnabled))
                    continue;

                preview.MidiToTrack[note.NoteNumber] = note.TrackIndex;
            }
        }
        else
        {
            preview.TrackColors[rightVirtualTrack] = PracticeHandColorResolver.RightHandHex;
            preview.TrackColors[leftVirtualTrack] = PracticeHandColorResolver.LeftHandHex;

            foreach (var note in _practiceSession.Notes)
            {
                if (PracticeTrackOptions.Count > 0
                    && !PracticeTrackOptions.Any(t => t.TrackIndex == note.TrackIndex && t.IsEnabled))
                    continue;

                preview.MidiToTrack[note.NoteNumber] =
                    note.NoteNumber < PracticeHandColorResolver.SplitMidiNote
                        ? leftVirtualTrack
                        : rightVirtualTrack;
            }
        }

        PracticeHandKeyPreview = preview;
        OnPropertyChanged(nameof(ShowPracticeHandPreview));
    }

    private static string BuildAcademyGuideText(AcademyModule module, AcademyLesson lesson)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(module.Guide))
            parts.Add(module.Guide.Trim());
        if (!string.IsNullOrWhiteSpace(lesson.Guide))
            parts.Add(lesson.Guide.Trim());

        return string.Join("\n\n", parts);
    }

    private void EndAcademyPracticeMode()
    {
        if (!IsAcademyPracticeMode && !IsPracticeLessonArmed)
            return;

        IsAcademyPracticeMode = false;
        IsPracticeLessonArmed = false;
        _armedAcademyLesson = null;
        _activeAcademyHand = AcademyHand.Any;
        _activeAcademyLessonKind = AcademyLessonKind.Guide;
        OnPropertyChanged(nameof(ShowAcademyFingerLabelsOnNotes));
        OnPropertyChanged(nameof(PracticeFallingNoteLabelMode));
        _lastPreviewedAcademyLessonId = null;
        AcademyGuideText = string.Empty;
        EndAcademyTour();
        PracticeHandKeyPreview = null;
        IsPracticeCountdownActive = false;
        PracticeCountdownDisplay = string.Empty;
        OnPropertyChanged(nameof(ShowPracticeCenterPlay));
        OnPropertyChanged(nameof(ShowPracticeHandPreview));
        OnPropertyChanged(nameof(PracticeNoteLabelMode));
        RefreshPracticeFallingNoteLayout();
    }

    [RelayCommand]
    private void ClosePracticeAcademyOverlay()
    {
        IsPracticeAcademyOverlayOpen = false;
    }

    public bool ShowAcademyTourOnPiano => IsAcademyTourVisible && IsPracticeAcademyOverlayOpen;

    [RelayCommand]
    private async Task ConfirmPracticePlayAsync()
    {
        if (SelectedPracticeSong is null)
        {
            DebraDialogs.Warning(L.T(UiText.SectionPractice), L.T(UiText.PracticeSelectSong));
            return;
        }

        if (_practiceSession.Notes.Count == 0)
            await ReloadPracticeChartAsync(SelectedPracticeSong).ConfigureAwait(true);

        if (_practiceSession.Notes.Count == 0)
        {
            DebraDialogs.Warning(L.T(UiText.SectionPractice), L.T(UiText.PracticeNoNotes));
            return;
        }

        await RunPracticeCountdownAsync(showGoFlash: true).ConfigureAwait(true);

        IsPracticeLessonArmed = false;
        OnPropertyChanged(nameof(ShowPracticeCenterPlay));
        EndAcademyTour();

        await RunPracticeStartCoreAsync().ConfigureAwait(true);

        if (_armedAcademyLesson is not null)
            MarkAcademyLessonComplete(_armedAcademyLesson.Id);
    }

    private async Task<bool> TryConfirmPracticeMidiInputAsync()
    {
        if (_settings.Settings.PracticeNoMidiKeyboardWarningDismissed)
            return true;

        RefreshMidiInputDevices();
        if (IsLiveMidiEnabled && _liveMidi.ConnectedDeviceName is not null)
            return true;

        var proceed = false;
        var dontRemind = false;
        await UiDispatcher.RunAsync(() =>
        {
            proceed = DebraDialogs.ConfirmWithDontRemind(
                L.T(UiText.PracticeNoMidiTitle),
                L.T(UiText.PracticeNoMidiMessage),
                L.T(UiText.PracticeNoMidiContinue),
                "Cancel",
                L.T(UiText.PracticeNoMidiDontRemind),
                out dontRemind);
        }).ConfigureAwait(true);

        if (dontRemind)
        {
            _settings.Settings.PracticeNoMidiKeyboardWarningDismissed = true;
            _settings.Save();
        }

        return proceed;
    }

    private async Task RunPracticeStartCoreAsync()
    {
        _playback.Stop();

        if (!IsAcademyPracticeMode && !await PrepareGameConnectionAsync().ConfigureAwait(true))
            return;

        if (!await TryConfirmPracticeMidiInputAsync().ConfigureAwait(true))
            return;

        await EnableLiveMidiForPracticeAsync().ConfigureAwait(true);

        _practiceSound.ResetSession();
        SyncPracticeSoundState();
        _practiceSession.SetTempoPercent(PracticeTempoPercent);
        _practiceSession.SetPlaybackLeadInMs(PracticeSessionService.DefaultLeadInMs);
        _practiceSession.Start();
    }

    private void StartAcademyTour(AcademyLesson lesson, AcademyModule module)
    {
        _academyTourLesson = lesson;
        _academyTourModule = module;
        _academyTourSteps = lesson.TourSteps is { Count: > 0 }
            ? lesson.TourSteps
            : BuildTourStepsFromGuide(lesson.Guide);

        if (_academyTourSteps.Count == 0)
        {
            EndAcademyTour();
            return;
        }

        _academyTourStepIndex = 0;
        IsAcademyTourVisible = true;
        OnPropertyChanged(nameof(ShowAcademyTourOnPiano));
        OnPropertyChanged(nameof(AcademyTourShowHandDiagram));
        OnPropertyChanged(nameof(AcademyTourHighlightHand));
        ApplyAcademyTourStep();
        OnPropertyChanged(nameof(AcademyTourShowPlayButton));
        OnPropertyChanged(nameof(AcademyTourPlayLabel));
    }

    private void ApplyAcademyTourStep()
    {
        if (_academyTourStepIndex < 0 || _academyTourStepIndex >= _academyTourSteps.Count)
        {
            EndAcademyTour();
            return;
        }

        IsAcademyTourSongPickerVisible = false;

        var step = _academyTourSteps[_academyTourStepIndex];
        AcademyTourText = step.Text.Trim();
        AcademyTourStepLabel = L.F(UiText.AcademyTourStepLabel, _academyTourStepIndex + 1, _academyTourSteps.Count);
        AcademyTourHighlightNotes = step.HighlightNotes ?? [];
        AcademyTourPictogramHint = AcademyTourHintParser.Parse(step.Hint);
        OnPropertyChanged(nameof(AcademyTourShowHandDiagram));
        OnPropertyChanged(nameof(AcademyTourShowPictogram));
        OnPropertyChanged(nameof(AcademyTourNextLabel));
        OnPropertyChanged(nameof(IsAcademyTourLastStep));
        OnPropertyChanged(nameof(CanAcademyTourSkipToNextLesson));
        OnPropertyChanged(nameof(AcademyTourShowDismissButton));
        OnPropertyChanged(nameof(AcademyTourShowPrimaryNextButton));
        OnPropertyChanged(nameof(AcademyTourShowPlayButton));
        OnPropertyChanged(nameof(AcademyTourPlayLabel));
        OnPropertyChanged(nameof(AcademyTourShowSongPickerPlayButton));
        OnPropertyChanged(nameof(AcademyTourShowTourContent));
        OnPropertyChanged(nameof(ShouldOfferSongPickerAfterTour));
        AcademyTourBackCommand.NotifyCanExecuteChanged();
        AcademyTourSkipNextCommand.NotifyCanExecuteChanged();
    }

    private void EndAcademyTour()
    {
        _academyTourSteps = [];
        _academyTourStepIndex = 0;
        _academyTourLesson = null;
        _academyTourModule = null;
        IsAcademyTourSongPickerVisible = false;
        IsAcademyTourVisible = false;
        OnPropertyChanged(nameof(ShowAcademyTourOnPiano));
        AcademyTourText = string.Empty;
        AcademyTourStepLabel = string.Empty;
        AcademyTourHighlightNotes = [];
        AcademyTourPictogramHint = AcademyTourHintKind.None;
        OnPropertyChanged(nameof(AcademyTourShowHandDiagram));
        OnPropertyChanged(nameof(AcademyTourShowPictogram));
        OnPropertyChanged(nameof(AcademyTourNextLabel));
        OnPropertyChanged(nameof(IsAcademyTourLastStep));
        OnPropertyChanged(nameof(CanAcademyTourSkipToNextLesson));
        OnPropertyChanged(nameof(AcademyTourShowDismissButton));
        OnPropertyChanged(nameof(AcademyTourShowPrimaryNextButton));
        OnPropertyChanged(nameof(AcademyTourShowPlayButton));
        OnPropertyChanged(nameof(AcademyTourPlayLabel));
        OnPropertyChanged(nameof(AcademyTourShowSongPickerPlayButton));
        OnPropertyChanged(nameof(AcademyTourShowTourContent));
        OnPropertyChanged(nameof(ShouldOfferSongPickerAfterTour));
        AcademyTourBackCommand.NotifyCanExecuteChanged();
        AcademyTourSkipNextCommand.NotifyCanExecuteChanged();
    }

    private void ShowAcademyTourSongPicker()
    {
        if (_academyTourModule is null)
            return;

        IsAcademyTourSongPickerVisible = true;
        if (AcademyPanel.SelectedSongLesson is null && AcademyPanel.SongLessons.Count > 0)
            AcademyPanel.SelectedSongLesson = AcademyPanel.SongLessons[0];

        AcademyTourStepLabel = L.T(UiText.AcademyTourSongPickerTitle);
        AcademyTourText = string.Empty;
        AcademyTourHighlightNotes = [];
        AcademyTourPictogramHint = AcademyTourHintKind.None;

        OnPropertyChanged(nameof(AcademyTourShowHandDiagram));
        OnPropertyChanged(nameof(AcademyTourShowPictogram));
        OnPropertyChanged(nameof(AcademyTourNextLabel));
        OnPropertyChanged(nameof(IsAcademyTourLastStep));
        OnPropertyChanged(nameof(CanAcademyTourSkipToNextLesson));
        OnPropertyChanged(nameof(AcademyTourShowDismissButton));
        OnPropertyChanged(nameof(AcademyTourShowPrimaryNextButton));
        OnPropertyChanged(nameof(AcademyTourShowPlayButton));
        OnPropertyChanged(nameof(AcademyTourPlayLabel));
        OnPropertyChanged(nameof(AcademyTourShowSongPickerPlayButton));
        OnPropertyChanged(nameof(AcademyTourShowTourContent));
        OnPropertyChanged(nameof(ShouldOfferSongPickerAfterTour));
        AcademyTourBackCommand.NotifyCanExecuteChanged();
        AcademyTourSkipNextCommand.NotifyCanExecuteChanged();
    }

    private bool ShouldOfferSongPickerAfterTour() =>
        IsAcademyTourVisible &&
        !IsAcademyTourSongPickerVisible &&
        _academyTourLesson?.Kind == AcademyLessonKind.Exercise &&
        _academyTourModule is not null &&
        IsLastExerciseInModule(_academyTourLesson, _academyTourModule) &&
        AcademyPanel.ModuleHasPracticeSongs();

    private static bool IsLastExerciseInModule(AcademyLesson lesson, AcademyModule module)
    {
        var exercises = module.Lessons
            .Where(l => l.Kind == AcademyLessonKind.Exercise && !l.ComingSoon)
            .OrderBy(l => l.Order)
            .ToList();

        return exercises.Count > 0 && exercises[^1].Id == lesson.Id;
    }

    public bool AcademyTourShowTourContent =>
        IsAcademyTourVisible && !IsAcademyTourSongPickerVisible;

    public string AcademyTourSwitchSongHint => L.T(UiText.AcademyTourSwitchSongHint);

    private static List<AcademyTourStep> BuildTourStepsFromGuide(string? guide)
    {
        if (string.IsNullOrWhiteSpace(guide))
            return [];

        return guide
            .Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(p => new AcademyTourStep { Text = p })
            .ToList();
    }

    public bool CanAcademyTourGoBack =>
        IsAcademyTourVisible && (IsAcademyTourSongPickerVisible || _academyTourStepIndex > 0);

    public bool IsAcademyTourLastStep =>
        IsAcademyTourVisible &&
        !IsAcademyTourSongPickerVisible &&
        _academyTourSteps.Count > 0 &&
        _academyTourStepIndex >= _academyTourSteps.Count - 1;

    public string AcademyTourNextLabel =>
        IsAcademyTourSongPickerVisible
            ? L.T(UiText.AcademyTourDone)
            : ShouldOfferSongPickerAfterTour()
                ? L.T(UiText.AcademyTourChooseSong)
                : IsAcademyTourLastStep
                    ? L.T(UiText.AcademyTourDone)
                    : L.T(UiText.AcademyTourNext);

    public string AcademyTourBackLabel => L.T(UiText.AcademyTourBack);

    public string AcademyTourSkipNextLabel => L.T(UiText.AcademyTourNextLesson);

    public bool CanAcademyTourSkipToNextLesson =>
        IsAcademyTourLastStep &&
        !IsAcademyTourSongPickerVisible &&
        AcademyPanel.CanAdvanceToNextExercise(_academyTourLesson);

    public bool AcademyTourShowDismissButton => CanAcademyTourSkipToNextLesson;

    public bool AcademyTourShowPrimaryNextButton =>
        !IsAcademyTourSongPickerVisible && !CanAcademyTourSkipToNextLesson;

    public bool AcademyTourShowPlayButton =>
        AcademyTourShowTourContent &&
        _academyTourLesson is not null &&
        _academyTourLesson.Kind != AcademyLessonKind.Guide &&
        !_academyTourLesson.ComingSoon;

    public bool AcademyTourShowSongPickerPlayButton => IsAcademyTourSongPickerVisible;

    public string AcademyTourPlayLabel =>
        IsAcademyTourSongPickerVisible || _academyTourLesson?.Kind == AcademyLessonKind.Song
            ? L.T(UiText.AcademyTourPlaySong)
            : L.T(UiText.AcademyTourPlayExercise);

    [RelayCommand]
    private async Task AcademyTourPlayCurrentLessonAsync()
    {
        if (_academyTourLesson is null || _academyTourModule is null)
            return;

        await PlayAcademyLessonAsync(_academyTourLesson, _academyTourModule).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task AcademyTourPlaySelectedSongAsync()
    {
        if (_academyTourModule is null || AcademyPanel.SelectedSongLesson is null)
            return;

        await PlayAcademyLessonAsync(AcademyPanel.SelectedSongLesson.Lesson, _academyTourModule).ConfigureAwait(true);
    }

    private async Task PlayAcademyLessonAsync(AcademyLesson lesson, AcademyModule module)
    {
        await PreviewAcademyLessonAsync(lesson, module).ConfigureAwait(true);

        if (_practiceSession.Notes.Count == 0)
        {
            DebraDialogs.Warning(L.T(UiText.SectionPractice), L.T(UiText.PracticeNoNotes));
            return;
        }

        await StartAcademyLessonPlayback().ConfigureAwait(true);
    }

    public bool AcademyTourShowHandDiagram =>
        AcademyTourShowTourContent && IsAcademyPracticeMode &&
        (AcademyTourPictogramHint == AcademyTourHintKind.Hand ||
         AcademyTourPictogramHint == AcademyTourHintKind.None);

    public bool AcademyTourShowPictogram =>
        AcademyTourShowTourContent &&
        AcademyTourPictogramHint != AcademyTourHintKind.None &&
        AcademyTourPictogramHint != AcademyTourHintKind.Hand;

    public AcademyHand AcademyTourHighlightHand => _activeAcademyHand;

    [RelayCommand(CanExecute = nameof(CanAcademyTourGoBack))]
    private void AcademyTourBack()
    {
        if (!CanAcademyTourGoBack)
            return;

        if (IsAcademyTourSongPickerVisible)
        {
            IsAcademyTourSongPickerVisible = false;
            ApplyAcademyTourStep();
            AcademyTourBackCommand.NotifyCanExecuteChanged();
            return;
        }

        _academyTourStepIndex--;
        ApplyAcademyTourStep();
        AcademyTourBackCommand.NotifyCanExecuteChanged();
        AcademyTourNextCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(IsAcademyTourLastStep));
        OnPropertyChanged(nameof(CanAcademyTourSkipToNextLesson));
        AcademyTourSkipNextCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void AcademyTourNext()
    {
        if (!IsAcademyTourVisible)
            return;

        if (IsAcademyTourSongPickerVisible)
        {
            EndAcademyTour();
            return;
        }

        if (_academyTourSteps.Count == 0)
            return;

        if (_academyTourStepIndex < _academyTourSteps.Count - 1)
        {
            _academyTourStepIndex++;
            ApplyAcademyTourStep();
            AcademyTourBackCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(AcademyTourNextLabel));
            OnPropertyChanged(nameof(IsAcademyTourLastStep));
            OnPropertyChanged(nameof(CanAcademyTourSkipToNextLesson));
            AcademyTourSkipNextCommand.NotifyCanExecuteChanged();
            return;
        }

        if (ShouldOfferSongPickerAfterTour())
        {
            ShowAcademyTourSongPicker();
            return;
        }

        EndAcademyTour();
    }

    [RelayCommand(CanExecute = nameof(CanAcademyTourSkipToNextLesson))]
    private async Task AcademyTourSkipNextAsync()
    {
        if (!CanAcademyTourSkipToNextLesson || _academyTourLesson is null)
            return;

        var current = _academyTourLesson;
        EndAcademyTour();
        await AcademyPanel.AdvanceToNextExerciseAsync(current).ConfigureAwait(true);
    }
}
