using System.Diagnostics;

using WhereWindsMeetMidiPlayer.Models;



namespace WhereWindsMeetMidiPlayer.Services;



public enum PlaybackState

{

    Stopped,

    Playing,

    Paused

}



public sealed class PlaybackEngine : IDisposable

{

    private readonly InputService _inputService;

    private readonly object _gate = new();

    private CancellationTokenSource? _cts;

    private Task? _playbackTask;

    private List<ScheduledNote> _schedule = [];

    private PlaybackState _state = PlaybackState.Stopped;

    private long _pausedAtMs;

    private readonly Stopwatch _clock = new();



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

                return _state switch

                {

                    PlaybackState.Playing => _pausedAtMs + _clock.ElapsedMilliseconds,

                    PlaybackState.Paused => _pausedAtMs,

                    _ => 0

                };

            }

        }

    }



    public long TotalDurationMs { get; private set; }



    public event EventHandler? PositionChanged;

    public event EventHandler? PlaybackCompleted;

    public event EventHandler<PlaybackState>? StateChanged;



    public PlaybackEngine(InputService inputService)

    {

        _inputService = inputService;

    }



    public void LoadSchedule(IReadOnlyList<ScheduledNote> notes, long totalDurationMs)

    {

        lock (_gate)

        {

            StopInternal();

            // BuildSchedule already returns notes sorted by StartMs; avoid re-sorting on the UI thread.
            _schedule = notes is List<ScheduledNote> list ? list : notes.ToList();

            TotalDurationMs = totalDurationMs;

        }

    }



    public void Play()

    {

        lock (_gate)

        {

            if (_schedule.Count == 0)

                return;



            if (_state == PlaybackState.Paused)

            {

                _clock.Restart();

                SetState(PlaybackState.Playing);

                return;

            }



            if (_state == PlaybackState.Playing)

                return;



            _pausedAtMs = 0;

            _clock.Restart();

            SetState(PlaybackState.Playing);

            StartPlaybackLoop();

        }

    }



    public void Pause()

    {

        lock (_gate)

        {

            if (_state != PlaybackState.Playing)

                return;



            _pausedAtMs += _clock.ElapsedMilliseconds;

            _clock.Reset();

            CancelLoop();

            SetState(PlaybackState.Paused);

        }

    }



    public void Stop()

    {

        lock (_gate)

        {

            StopInternal();

        }

    }

    public void SeekToMs(long positionMs)

    {

        lock (_gate)

        {

            if (_schedule.Count == 0)

                return;

            var max = Math.Max(0, TotalDurationMs);

            _pausedAtMs = max > 0 ? Math.Clamp(positionMs, 0, max) : 0;

            _clock.Reset();

            RaisePositionChanged();

        }

    }

    public void PlayFromCurrentPosition(

        int noteDelayMs,

        int chordRollDelayMs,

        int minKeyPressDurationMs,

        int identicalKeyGapMs)

    {

        lock (_gate)

        {

            if (_schedule.Count == 0)

                return;

            CancelLoop();

            _clock.Restart();

            SetState(PlaybackState.Playing);

            _cts = new CancellationTokenSource();

            var token = _cts.Token;

            _playbackTask = Task.Run(async () =>

            {

                try

                {

                    await RunWithSettings(token, noteDelayMs, chordRollDelayMs, minKeyPressDurationMs, identicalKeyGapMs);

                }

                catch (OperationCanceledException)

                {

                    // expected on stop

                }

            }, token);

        }

    }



    private void StopInternal()

    {

        _pausedAtMs = 0;

        _clock.Reset();

        CancelLoop();

        SetState(PlaybackState.Stopped);

        RaisePositionChanged();

    }



    private void StartPlaybackLoop()

    {

        CancelLoop();

        _cts = new CancellationTokenSource();

        var token = _cts.Token;

        _playbackTask = Task.Run(() => RunPlaybackAsync(token), token);

    }



    private void CancelLoop()

    {

        try

        {

            _cts?.Cancel();

        }

        catch

        {

            // ignored

        }

    }



    private async Task RunPlaybackAsync(CancellationToken cancellationToken) =>

        await RunWithSettings(cancellationToken, 0, 0, 0, 0);



    /// <summary>

    /// SnowiyQ-style playback: wait until each event time, tap keys instantly (no MIDI hold delay).

    /// </summary>

    public async Task RunWithSettings(

        CancellationToken cancellationToken,

        int noteDelayMs,

        int chordRollDelayMs,

        int minKeyPressDurationMs,

        int identicalKeyGapMs)

    {

        _ = minKeyPressDurationMs;



        var startOffset = CurrentPositionMs;

        var lastKeyTime = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);



        var events = _schedule;

        var index = 0;

        while (index < events.Count && events[index].StartMs < startOffset)

            index++;



        var sessionClock = Stopwatch.StartNew();



        while (index < events.Count)

        {

            cancellationToken.ThrowIfCancellationRequested();



            PlaybackState state;

            lock (_gate)

                state = _state;



            if (state == PlaybackState.Paused)

            {

                await Task.Delay(20, cancellationToken);

                sessionClock.Stop();

                while (true)

                {

                    cancellationToken.ThrowIfCancellationRequested();

                    lock (_gate)

                    {

                        if (_state == PlaybackState.Playing)

                            break;

                        if (_state == PlaybackState.Stopped)

                            return;

                    }



                    await Task.Delay(20, cancellationToken);

                }



                sessionClock.Start();

                startOffset = CurrentPositionMs;

                while (index < events.Count && events[index].StartMs < startOffset)

                    index++;

                continue;

            }



            if (state == PlaybackState.Stopped)

                return;



            var targetMs = events[index].StartMs;

            var elapsed = startOffset + sessionClock.ElapsedMilliseconds;



            await WaitUntilSongTimeAsync(targetMs, elapsed, cancellationToken, sessionClock, startOffset);



            // Fire every note at this timestamp (chords = back-to-back taps, like SnowiyQ)

            while (index < events.Count && events[index].StartMs == targetMs)

            {

                cancellationToken.ThrowIfCancellationRequested();

                var evt = events[index];



                if (!string.IsNullOrEmpty(evt.KeyCombo))

                {

                    if (identicalKeyGapMs > 0 && lastKeyTime.TryGetValue(evt.KeyCombo, out var lastMs))

                    {

                        var gapNeeded = identicalKeyGapMs - (sessionClock.ElapsedMilliseconds - lastMs);

                        if (gapNeeded > 0)

                            await Task.Delay((int)gapNeeded, cancellationToken);

                    }



                    _inputService.PressKeyCombo(evt.KeyCombo);

                    lastKeyTime[evt.KeyCombo] = sessionClock.ElapsedMilliseconds;

                }



                index++;

            }



            RaisePositionChanged();

        }



        var completed = false;
        lock (_gate)
        {
            if (_state == PlaybackState.Playing)
            {
                StopInternal();
                completed = true;
            }
        }

        if (completed)
            PlaybackCompleted?.Invoke(this, EventArgs.Empty);
    }



    private static async Task WaitUntilSongTimeAsync(

        long targetMs,

        long elapsed,

        CancellationToken cancellationToken,

        Stopwatch sessionClock,

        long startOffset)

    {

        while (targetMs > startOffset + sessionClock.ElapsedMilliseconds)

        {

            var remaining = targetMs - (startOffset + sessionClock.ElapsedMilliseconds);

            var sleep = (int)Math.Min(remaining, 2);

            if (sleep > 0)

                await Task.Delay(sleep, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

        }

    }



    public static List<ScheduledNote> BuildSchedule(

        IReadOnlyList<NormalizedNote> notes,

        KeyMappingService keyMapping,

        int chordRollDelayMs,

        int noteDelayMs)

    {

        var grouped = notes

            .Where(n => !n.Skipped)

            .GroupBy(n => n.StartMs)

            .OrderBy(g => g.Key);



        var schedule = new List<ScheduledNote>();

        foreach (var chord in grouped)

        {

            var rollIndex = 0;

            foreach (var note in chord.OrderBy(n => n.NoteNumber))

            {

                var combo = keyMapping.GetKeyCombo(note.NoteNumber);

                if (combo is null)

                    continue;



                schedule.Add(new ScheduledNote

                {

                    NoteNumber = note.NoteNumber,

                    StartMs = chord.Key + rollIndex * chordRollDelayMs,

                    DurationMs = 0,

                    KeyCombo = combo

                });

                rollIndex++;

            }

        }



        var ordered = schedule.OrderBy(n => n.StartMs).ThenBy(n => n.NoteNumber).ToList();
        ApplyMinimumNoteSpacing(ordered, noteDelayMs);
        return ordered;

    }



    /// <summary>Ensures at least <paramref name="noteDelayMs"/> between each pair of consecutive taps.</summary>
    private static void ApplyMinimumNoteSpacing(List<ScheduledNote> schedule, int noteDelayMs)
    {
        if (noteDelayMs <= 0 || schedule.Count < 2)
            return;

        for (var i = 1; i < schedule.Count; i++)
        {
            var minStart = schedule[i - 1].StartMs + noteDelayMs;
            if (schedule[i].StartMs < minStart)
                schedule[i].StartMs = minStart;
        }
    }



    public void ConfigureAndPlay(

        int noteDelayMs,

        int chordRollDelayMs,

        int minKeyPressDurationMs,

        int identicalKeyGapMs)

    {

        lock (_gate)

        {

            if (_state == PlaybackState.Playing)

                return;



            if (_state == PlaybackState.Paused)

            {

                _clock.Restart();

                SetState(PlaybackState.Playing);

            }

            else

            {

                _pausedAtMs = 0;

                _clock.Restart();

                SetState(PlaybackState.Playing);

            }



            PlayFromCurrentPosition(noteDelayMs, chordRollDelayMs, minKeyPressDurationMs, identicalKeyGapMs);

        }

    }



    private void SetState(PlaybackState state)

    {

        _state = state;

        StateChanged?.Invoke(this, state);

    }



    private void RaisePositionChanged() => PositionChanged?.Invoke(this, EventArgs.Empty);



    public void Dispose()

    {

        Stop();

        _cts?.Dispose();

    }

}


