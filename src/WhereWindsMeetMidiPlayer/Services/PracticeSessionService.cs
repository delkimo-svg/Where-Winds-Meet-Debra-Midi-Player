using System.Diagnostics;
using WhereWindsMeetMidiPlayer.Models;

namespace WhereWindsMeetMidiPlayer.Services;

public sealed class PracticeSessionService : IDisposable
{
    private readonly object _gate = new();
    private readonly HashSet<int> _enabledTrackIndices = [];
    private CancellationTokenSource? _cts;
    private Task? _runTask;
    private long _leadInMs = DefaultLeadInMs;
    private List<PracticeVisualNote> _notes = [];
    private long _durationMs;
    private PlaybackState _state = PlaybackState.Stopped;
    private long _pausedAtMs;
    private readonly Stopwatch _clock = new();
    private double _tempoMultiplier = 1.0;
    private readonly HashSet<(long StartMs, int NoteNumber)> _completed = new();
    private List<PracticeVisualNote> _waitingNotes = [];
    private long _learnResumeMs;
    private bool _isWaitingForInput;
    private readonly Dictionary<int, long> _recentGamePressMs = new();
    private readonly Dictionary<int, long> _recentDisplayPressMs = new();
    private const int RecentPressGraceMs = 450;

    public event Action<long>? PositionChanged;
    public event Action<PlaybackState>? StateChanged;
    public event Action? Completed;
    public event Action? HitLineNotesChanged;
    public event Action? WaitingNotesChanged;
    public event Action? EnabledTracksChanged;

    public PracticeMode Mode { get; set; } = PracticeMode.Follow;

    public const int DefaultLeadInMs = 2000;

    public bool IsInPlaybackLeadIn
    {
        get { lock (_gate) return CurrentPositionMs < 0; }
    }

    public long PlaybackLeadInMs => _leadInMs;

    public bool IsWaitingForInput
    {
        get { lock (_gate) return _isWaitingForInput; }
    }

    public IReadOnlyList<PracticeVisualNote> WaitingNotes
    {
        get { lock (_gate) return _waitingNotes; }
    }

    public IReadOnlyList<PracticeVisualNote> Notes
    {
        get { lock (_gate) return _notes; }
    }

    public IReadOnlyList<PracticeVisualNote> VisibleNotes
    {
        get { lock (_gate) return _notes.Where(IsTrackEnabled).ToList(); }
    }

    public long DurationMs
    {
        get { lock (_gate) return _durationMs; }
    }

    public PlaybackState State
    {
        get { lock (_gate) return _state; }
    }

    public long CurrentPositionMs
    {
        get
        {
            lock (_gate)
            {
                return (long)Math.Round(GetPositionMsExactUnlocked());
            }
        }
    }

    /// <summary>Sub-millisecond playback position for smooth piano-roll rendering.</summary>
    public double CurrentPositionMsExact
    {
        get
        {
            lock (_gate)
                return GetPositionMsExactUnlocked();
        }
    }

    private double GetPositionMsExactUnlocked() =>
        _state switch
        {
            PlaybackState.Playing => _clock.Elapsed.TotalMilliseconds * _tempoMultiplier + _pausedAtMs,
            PlaybackState.Paused => _pausedAtMs,
            PlaybackState.Stopped => _pausedAtMs,
            _ => 0
        };

    public void SetTempoPercent(int percent) =>
        _tempoMultiplier = Math.Clamp(percent, 50, 200) / 100.0;

    public void SetPlaybackLeadInMs(int leadInMs) =>
        _leadInMs = Math.Clamp(leadInMs, 0, 10_000);

    public void SetEnabledTrackIndices(IReadOnlyCollection<int> enabledTrackIndices)
    {
        lock (_gate)
        {
            _enabledTrackIndices.Clear();
            foreach (var index in enabledTrackIndices)
                _enabledTrackIndices.Add(index);

            if (_isWaitingForInput)
            {
                _waitingNotes = _waitingNotes.Where(IsTrackEnabled).ToList();
                if (_waitingNotes.Count == 0)
                {
                    _isWaitingForInput = false;
                    if (_state == PlaybackState.Paused)
                    {
                        _pausedAtMs = _learnResumeMs;
                        _clock.Reset();
                        _clock.Start();
                        SetState(PlaybackState.Playing);
                        EnsureRunner();
                    }
                }

                WaitingNotesChanged?.Invoke();
            }

            HitLineNotesChanged?.Invoke();
        }

        EnabledTracksChanged?.Invoke();
    }

    public event Action? NotesLoaded;

    public void NotifyFallingNoteLayoutChanged()
    {
        if (_notes.Count > 0)
            NotesLoaded?.Invoke();
    }

    public void UpdateNoteColors(IReadOnlyList<PracticeVisualNote> coloredNotes)
    {
        lock (_gate)
        {
            if (coloredNotes.Count == 0)
                return;

            _notes = coloredNotes.ToList();

            if (_waitingNotes.Count > 0)
            {
                var waitingKeys = _waitingNotes
                    .Select(w => (w.StartMs, w.NoteNumber, w.TrackIndex))
                    .ToHashSet();
                _waitingNotes = _notes
                    .Where(n => waitingKeys.Contains((n.StartMs, n.NoteNumber, n.TrackIndex)))
                    .ToList();
            }
        }

        NotesLoaded?.Invoke();
        HitLineNotesChanged?.Invoke();
        WaitingNotesChanged?.Invoke();
    }

    public void Load(IReadOnlyList<PracticeVisualNote> notes, long durationMs)
    {
        lock (_gate)
        {
            StopInternal();
            _notes = notes.ToList();
            _durationMs = Math.Max(durationMs, 1);
            _enabledTrackIndices.Clear();
            foreach (var trackIndex in _notes.Select(n => n.TrackIndex).Distinct())
                _enabledTrackIndices.Add(trackIndex);
            _pausedAtMs = 0;
            _completed.Clear();
            _waitingNotes = [];
            _isWaitingForInput = false;
            _recentGamePressMs.Clear();
            _recentDisplayPressMs.Clear();
        }

        NotesLoaded?.Invoke();
    }

    public IReadOnlyList<PracticeVisualNote> GetNotesAtHitLine(int windowMs = 45)
    {
        lock (_gate)
        {
            var pos = CurrentPositionMs;
            return _notes
                .Where(IsTrackEnabled)
                .Where(n => Math.Abs(n.StartMs - pos) <= windowMs)
                .ToList();
        }
    }

    public bool TryRegisterHit(int gameNoteNumber)
    {
        lock (_gate)
        {
            if (!_isWaitingForInput)
                return false;

            var hit = FindWaitingMatch(gameNoteNumber, 0);
            if (hit is null)
                return false;

            RecordPressedNote(hit.NoteNumber, hit.GameNoteNumber > 0 ? hit.GameNoteNumber : gameNoteNumber);
            return CompleteHit(hit);
        }
    }

    public void RecordPressedNote(int displayNote, int? gameNote = null)
    {
        if (displayNote <= 0)
            return;

        var now = Environment.TickCount64;
        lock (_gate)
        {
            _recentDisplayPressMs[displayNote] = now;
            if (gameNote is > 0)
                _recentGamePressMs[gameNote.Value] = now;
        }
    }

    public void TryReconcileActiveInput(
        IReadOnlyCollection<int> activeGameNotes,
        IReadOnlyCollection<int> activeDisplayNotes)
    {
        lock (_gate)
        {
            if (!_isWaitingForInput || _waitingNotes.Count == 0)
                return;

            var now = Environment.TickCount64;
            foreach (var waiting in _waitingNotes.ToList())
            {
                if (IsWaitingNoteSatisfied(waiting, activeGameNotes, activeDisplayNotes, now))
                    CompleteHit(waiting);
            }
        }
    }

    private bool IsWaitingNoteSatisfied(
        PracticeVisualNote waiting,
        IReadOnlyCollection<int> activeGameNotes,
        IReadOnlyCollection<int> activeDisplayNotes,
        long nowMs)
    {
        foreach (var gameNote in activeGameNotes)
            if (NoteMatchesPressedKey(waiting, gameNote, 0))
                return true;

        foreach (var displayNote in activeDisplayNotes)
            if (NoteMatchesPressedKey(waiting, 0, displayNote))
                return true;

        var gameKey = waiting.GameNoteNumber > 0 ? waiting.GameNoteNumber : waiting.NoteNumber;
        if (_recentGamePressMs.TryGetValue(gameKey, out var gameAt) && nowMs - gameAt <= RecentPressGraceMs)
            return true;

        if (_recentDisplayPressMs.TryGetValue(waiting.NoteNumber, out var displayAt) && nowMs - displayAt <= RecentPressGraceMs)
            return true;

        return false;
    }

    private static bool NoteMatchesPressedKey(PracticeVisualNote waiting, int gameNote, int displayNote)
    {
        if (gameNote > 0)
        {
            if (waiting.GameNoteNumber == gameNote)
                return true;
            if (waiting.NoteNumber == gameNote)
                return true;
        }

        if (displayNote > 0 && waiting.NoteNumber == displayNote)
            return true;

        return false;
    }

    private PracticeVisualNote? FindWaitingMatch(int gameNote, int displayNote)
    {
        return _waitingNotes.FirstOrDefault(n => NoteMatchesPressedKey(n, gameNote, displayNote));
    }

    public bool TryRegisterHitFromRawMidi(
        int rawMidi,
        int velocity,
        NoteRangeService noteRange,
        bool smartTranspose,
        bool strictNoteRange,
        int octaveShift,
        NoteMappingMode mappingMode)
    {
        lock (_gate)
        {
            if (!_isWaitingForInput)
                return false;

            var mapped = LiveMidiMapper.MapToGameNoteNumber(
                rawMidi,
                velocity,
                noteRange,
                smartTranspose,
                strictNoteRange,
                octaveShift,
                mappingMode);

            if (mapped is not null)
            {
                var byGame = FindWaitingMatch(mapped.Value, 0);
                if (byGame is not null)
                {
                    RecordPressedNote(byGame.NoteNumber, mapped.Value);
                    return CompleteHit(byGame);
                }
            }

            var shifted = rawMidi + octaveShift * 12;
            var byDisplayPitch = FindWaitingMatch(0, shifted);
            if (byDisplayPitch is not null)
            {
                RecordPressedNote(byDisplayPitch.NoteNumber, mapped);
                return CompleteHit(byDisplayPitch);
            }

            if (mapped is not null)
            {
                foreach (var waiting in _waitingNotes)
                {
                    if (waiting.GameNoteNumber > 0)
                        continue;

                    var waitingMapped = LiveMidiMapper.MapToGameNoteNumber(
                        waiting.NoteNumber,
                        127,
                        noteRange,
                        smartTranspose,
                        strictNoteRange,
                        octaveShift,
                        mappingMode);

                    if (waitingMapped == mapped)
                    {
                        RecordPressedNote(waiting.NoteNumber, mapped.Value);
                        return CompleteHit(waiting);
                    }
                }
            }

            RecordPressedNote(shifted, mapped);
            return false;
        }
    }

    private bool CompleteHit(PracticeVisualNote hit)
    {
        _waitingNotes.Remove(hit);
        _completed.Add((hit.StartMs, hit.NoteNumber));

        if (_waitingNotes.Count > 0)
        {
            WaitingNotesChanged?.Invoke();
            return true;
        }

        WaitingNotesChanged?.Invoke();
        _isWaitingForInput = false;
        _pausedAtMs = _learnResumeMs;
        _clock.Reset();
        _clock.Start();
        SetState(PlaybackState.Playing);
        EnsureRunner();
        return true;
    }

    public void Start()
    {
        lock (_gate)
        {
            if (_notes.Count == 0)
                return;

            if (_state == PlaybackState.Paused && !_isWaitingForInput)
            {
                _clock.Reset();
                _clock.Start();
                SetState(PlaybackState.Playing);
                EnsureRunner();
                return;
            }

            if (_isWaitingForInput)
                return;

            _pausedAtMs = -_leadInMs;
            _completed.Clear();
            _waitingNotes = [];
            _isWaitingForInput = false;
            _clock.Reset();
            _clock.Start();
            SetState(PlaybackState.Playing);
            EnsureRunner();
        }
    }

    public void Pause()
    {
        lock (_gate)
        {
            if (_state != PlaybackState.Playing)
                return;

            _pausedAtMs = CurrentPositionMs;
            _clock.Stop();
            SetState(PlaybackState.Paused);
        }
    }

    public void SeekToMs(long positionMs)
    {
        lock (_gate)
        {
            if (_notes.Count == 0)
                return;

            positionMs = Math.Clamp(positionMs, 0, _durationMs);
            var wasPlaying = _state == PlaybackState.Playing;

            _isWaitingForInput = false;
            _waitingNotes = [];

            _completed.Clear();
            foreach (var note in _notes)
            {
                if (note.StartMs < positionMs)
                    _completed.Add((note.StartMs, note.NoteNumber));
            }

            _pausedAtMs = positionMs;
            _clock.Reset();

            if (wasPlaying)
            {
                _clock.Start();
                SetState(PlaybackState.Playing);
                EnsureRunner();
            }

            PositionChanged?.Invoke(CurrentPositionMs);
            HitLineNotesChanged?.Invoke();
            WaitingNotesChanged?.Invoke();
        }
    }

    public void Stop()
    {
        lock (_gate)
            StopInternal();
    }

    private void StopInternal()
    {
        _cts?.Cancel();
        _runTask = null;
        _clock.Stop();
        _pausedAtMs = 0;
        _waitingNotes = [];
        _isWaitingForInput = false;
        _completed.Clear();
        _recentGamePressMs.Clear();
        _recentDisplayPressMs.Clear();
        SetState(PlaybackState.Stopped);
        WaitingNotesChanged?.Invoke();
        HitLineNotesChanged?.Invoke();
    }

    private void EnsureRunner()
    {
        if (_runTask is { IsCompleted: false })
            return;

        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        _runTask = Task.Run(() => RunLoopAsync(token), token);
    }

    private async Task RunLoopAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                lock (_gate)
                {
                    if (_state != PlaybackState.Playing)
                        break;

                    var pos = CurrentPositionMs;
                    PositionChanged?.Invoke(pos);

                    if (Mode == PracticeMode.Learn && pos >= 0)
                        TryEnterLearnWait(pos);

                    if (pos >= _durationMs)
                    {
                        StopInternal();
                        Completed?.Invoke();
                        break;
                    }
                }

                await Task.Delay(16, token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // expected on stop
        }
    }

    private void TryEnterLearnWait(long pos)
    {
        if (_isWaitingForInput)
            return;

        var nextGroup = _notes
            .Where(IsTrackEnabled)
            .Where(n => !_completed.Contains((n.StartMs, n.NoteNumber)))
            .Where(n => n.StartMs <= pos + 30)
            .GroupBy(n => n.StartMs)
            .OrderBy(g => g.Key)
            .FirstOrDefault();

        if (nextGroup is null)
            return;

        _isWaitingForInput = true;
        _waitingNotes = nextGroup.ToList();
        _learnResumeMs = nextGroup.Key + 1;
        _pausedAtMs = nextGroup.Key;
        _clock.Stop();
        SetState(PlaybackState.Paused);
        WaitingNotesChanged?.Invoke();
    }

    private bool IsTrackEnabled(PracticeVisualNote note) =>
        _enabledTrackIndices.Count == 0 || _enabledTrackIndices.Contains(note.TrackIndex);

    private void SetState(PlaybackState state)
    {
        _state = state;
        StateChanged?.Invoke(state);
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }
    }
}
