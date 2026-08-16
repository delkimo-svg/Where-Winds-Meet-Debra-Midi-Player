using System.Diagnostics;
using System.Runtime.InteropServices;

using WhereWindsMeetMidiPlayer.Infrastructure;
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

    private double _tempoMultiplier = 1.0;

    public const double MinTempoMultiplier = 0.5;

    public const double MaxTempoMultiplier = 2.0;

    // FFXIV (monophonic) shaping — constants taken from BardMusicPlayer/LightAmp preprocessing:
    // chords become a 30 ms-spaced low→high roll of 25 ms taps (last note on the beat),
    // every key-up precedes the next key-down by 25 ms (60 ms after notes ≥ 100 ms),
    // and holds stay under the game's 4 s auto-release.
    private const int MonophonicChordSpacingMs = 30;
    private const int MonophonicChordTapMs = 25;
    private const int MonophonicShortGapMs = 25;
    private const int MonophonicLongGapMs = 60;
    private const int MonophonicLongNoteMs = 100;
    private const int MonophonicMaxHoldMs = 3925;

    public double TempoMultiplier
    {
        get { lock (_gate) return _tempoMultiplier; }
    }

    private volatile Func<int, bool, bool>? _directNoteSink;

    /// <summary>
    /// Optional direct note delivery (FFXIV via Hypnotoad): called as (midiNote, on) and returns
    /// true when it handled the note. False falls back to keyboard delivery for that note, so a
    /// plugin disconnect mid-song degrades gracefully instead of going silent.
    /// </summary>
    public Func<int, bool, bool>? DirectNoteSink
    {
        get => _directNoteSink;
        set => _directNoteSink = value;
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

                return _state switch

                {

                    PlaybackState.Playing => _pausedAtMs + (long)(_clock.ElapsedMilliseconds * _tempoMultiplier),

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

    /// <summary>Swap schedule during playback; keeps song position and playing/paused state.</summary>
    public void ReloadSchedule(
        IReadOnlyList<ScheduledNote> notes,
        long totalDurationMs,
        long positionMs,
        PlaybackState resumeState)
    {
        lock (_gate)
        {
            CancelLoop();

            _schedule = notes is List<ScheduledNote> list ? list : notes.ToList();
            TotalDurationMs = totalDurationMs;

            var max = Math.Max(0, TotalDurationMs);
            _pausedAtMs = max > 0 ? Math.Clamp(positionMs, 0, max) : 0;
            _clock.Reset();

            if (resumeState == PlaybackState.Playing)
                SetState(PlaybackState.Playing);
            else if (resumeState == PlaybackState.Paused)
                SetState(PlaybackState.Paused);
            else
                SetState(PlaybackState.Stopped);

            RaisePositionChanged();
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



            _pausedAtMs += (long)(_clock.ElapsedMilliseconds * _tempoMultiplier);

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

    public void SetTempoMultiplier(double multiplier)
    {
        multiplier = Math.Clamp(multiplier, MinTempoMultiplier, MaxTempoMultiplier);
        lock (_gate)
        {
            if (_state == PlaybackState.Playing)
            {
                _pausedAtMs += (long)(_clock.ElapsedMilliseconds * _tempoMultiplier);
                _clock.Restart();
            }

            _tempoMultiplier = multiplier;
        }
    }

    public void ResetTempoMultiplier() => SetTempoMultiplier(1.0);

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

        _tempoMultiplier = 1.0;

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

        // FFXIV: real key-down at note-on, key-up at note-off (BMP/LightAmp HoldNotes).
        var holdNotes = GameProfiles.Current.HoldNotes;
        var pendingUps = new List<(long UpAtMs, string Combo, int Note, bool Direct)>();

        // 1 ms system timer while playing (BMP-style): without it Task.Delay quantizes to
        // ~15.6 ms and fast passages jitter enough to bunch key events together.
        _ = TimeBeginPeriod(1);

        try
        {

        while (index < events.Count)

        {

            cancellationToken.ThrowIfCancellationRequested();



            PlaybackState state;

            lock (_gate)

                state = _state;



            if (state == PlaybackState.Paused)

            {

                ReleasePendingUps(pendingUps);

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

                var resumePositionMs = GetSongPositionMs();

                while (index < events.Count && events[index].StartMs < resumePositionMs)

                    index++;

                continue;

            }



            if (state == PlaybackState.Stopped)

                return;



            var targetMs = events[index].StartMs;

            await FlushPendingUpsAsync(pendingUps, targetMs, cancellationToken);

            await WaitUntilSongTimeAsync(targetMs, cancellationToken);



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



                    if (holdNotes && evt.DurationMs > 0)
                    {
                        var sink = _directNoteSink;
                        var direct = sink is not null && sink(evt.NoteNumber, true);
                        if (!direct)
                            _inputService.SendKeyDown(evt.KeyCombo);
                        // Direct notes sustain to their legato length; keyboard keeps the gapped hold.
                        var holdMs = direct && evt.LegatoDurationMs > 0 ? evt.LegatoDurationMs : evt.DurationMs;
                        pendingUps.Add((evt.StartMs + holdMs, evt.KeyCombo, evt.NoteNumber, direct));
                    }
                    else
                    {
                        _inputService.PressKeyCombo(evt.KeyCombo);
                    }

                    lastKeyTime[evt.KeyCombo] = sessionClock.ElapsedMilliseconds;

                }



                index++;

            }



            RaisePositionChanged();

        }

        // Let the last held note ring for its full duration before releasing.
        await FlushPendingUpsAsync(pendingUps, long.MaxValue, cancellationToken);

        }
        finally
        {
            _ = TimeEndPeriod(1);
            // Stop/pause mid-hold: release direct notes too, else they ring until the game's 4 s auto-release.
            foreach (var (_, _, note, direct) in pendingUps)
                if (direct)
                    _directNoteSink?.Invoke(note, false);
            if (holdNotes)
                _inputService.ReleaseAllHeldKeys();
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



    private void ReleaseNote(string combo, int note, bool direct)
    {
        if (direct)
            _directNoteSink?.Invoke(note, false);
        else
            _inputService.SendKeyUp(combo);
    }

    private void ReleasePendingUps(List<(long UpAtMs, string Combo, int Note, bool Direct)> pendingUps)
    {
        foreach (var (_, combo, note, direct) in pendingUps)
            ReleaseNote(combo, note, direct);
        pendingUps.Clear();
    }

    /// <summary>Sends every pending key-up scheduled before <paramref name="untilMs"/>, waiting for each in song time.</summary>
    private async Task FlushPendingUpsAsync(
        List<(long UpAtMs, string Combo, int Note, bool Direct)> pendingUps,
        long untilMs,
        CancellationToken cancellationToken)
    {
        while (pendingUps.Count > 0)
        {
            var next = 0;
            for (var i = 1; i < pendingUps.Count; i++)
                if (pendingUps[i].UpAtMs < pendingUps[next].UpAtMs)
                    next = i;

            if (pendingUps[next].UpAtMs >= untilMs)
                return;

            await WaitUntilSongTimeAsync(pendingUps[next].UpAtMs, cancellationToken);
            ReleaseNote(pendingUps[next].Combo, pendingUps[next].Note, pendingUps[next].Direct);
            pendingUps.RemoveAt(next);
        }
    }



    [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
    private static extern uint TimeBeginPeriod(uint uPeriod);

    [DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
    private static extern uint TimeEndPeriod(uint uPeriod);



    private long GetSongPositionMs()

    {

        lock (_gate)

        {

            return _state switch

            {

                PlaybackState.Playing => _pausedAtMs + (long)(_clock.ElapsedMilliseconds * _tempoMultiplier),

                PlaybackState.Paused => _pausedAtMs,

                _ => 0

            };

        }

    }



    private async Task WaitUntilSongTimeAsync(long targetMs, CancellationToken cancellationToken)

    {

        while (GetSongPositionMs() < targetMs)

        {

            cancellationToken.ThrowIfCancellationRequested();



            PlaybackState state;

            double tempoMultiplier;

            lock (_gate)

            {

                state = _state;

                tempoMultiplier = _tempoMultiplier;

            }



            if (state != PlaybackState.Playing)

                return;



            var positionMs = GetSongPositionMs();

            if (positionMs >= targetMs)

                break;



            var remainingSongMs = targetMs - positionMs;

            var wallMs = remainingSongMs / Math.Max(tempoMultiplier, MinTempoMultiplier);

            var sleep = (int)Math.Clamp(wallMs, 1, 50);

            await Task.Delay(sleep, cancellationToken);

        }

    }



    public static List<ScheduledNote> BuildSchedule(

        IReadOnlyList<NormalizedNote> notes,

        KeyMappingService keyMapping,

        int chordRollDelayMs,

        int noteDelayMs,

        int monophonicMinSpacingMs = 0)

    {

        var monophonic = GameProfiles.Current.Monophonic;

        var grouped = notes

            .Where(n => !n.Skipped)

            .GroupBy(n => n.StartMs)

            .OrderBy(g => g.Key);



        var schedule = new List<ScheduledNote>();

        foreach (var chord in grouped)

        {

            var members = new List<ScheduledNote>();

            foreach (var note in chord
                .GroupBy(n => n.NoteNumber)
                .Select(g => g.OrderByDescending(n => n.Velocity).First())
                .OrderBy(n => n.NoteNumber))

            {

                var combo = keyMapping.GetKeyCombo(note.NoteNumber);

                if (combo is null)

                    continue;



                members.Add(new ScheduledNote

                {

                    NoteNumber = note.NoteNumber,

                    StartMs = chord.Key,

                    DurationMs = monophonic ? note.DurationMs : 0,

                    KeyCombo = combo

                });

            }

            if (monophonic)
            {
                // BMP/LightAmp chord roll: low→high pre-roll, last (melody) note lands on the beat.
                // Humanized strum: the roll starts unhurried and tightens toward the beat (the
                // interval next to the melody note stays at base spacing, earlier ones widen up
                // to +35%). Deterministic, and never below base spacing — keyboard-safe.
                var spacing = Math.Max(chordRollDelayMs, MonophonicChordSpacingMs);
                long offset = 0;
                for (var i = members.Count - 1; i >= 0; i--)
                {
                    members[i].StartMs = chord.Key - offset;
                    if (i < members.Count - 1)
                    {
                        // Roll grace notes: tap length for keyboard, full musical length for legato.
                        members[i].LegatoDurationMs = members[i].DurationMs;
                        members[i].DurationMs = MonophonicChordTapMs;
                    }

                    var stepsFromBeat = members.Count - 1 - i;
                    var stretch = members.Count > 2
                        ? 1.0 + 0.35 * stepsFromBeat / (members.Count - 2)
                        : 1.0;
                    offset += (long)Math.Round(spacing * stretch);
                }
            }
            else
            {
                for (var i = 0; i < members.Count; i++)
                    members[i].StartMs = chord.Key + (long)i * chordRollDelayMs;
            }

            schedule.AddRange(members);

        }



        var ordered = schedule.OrderBy(n => n.StartMs).ThenBy(n => n.NoteNumber).ToList();
        if (monophonic)
            ApplyMonophonicShaping(
                ordered,
                Math.Max(Math.Max(noteDelayMs, monophonicMinSpacingMs), MonophonicChordSpacingMs));
        else
            ApplyMinimumNoteSpacing(ordered, noteDelayMs);
        return ordered;

    }

    /// <summary>
    /// FFXIV-style shaping (as BardMusicPlayer/LightAmp preprocess offline): note-ons at least
    /// <paramref name="minSpacingMs"/> apart, every key released before the next key-down
    /// (25 ms gap, 60 ms after notes ≥ 100 ms), holds capped below the game's 4 s auto-release.
    /// </summary>
    private static void ApplyMonophonicShaping(List<ScheduledNote> schedule, int minSpacingMs)
    {
        if (schedule.Count == 0)
            return;

        schedule[0].StartMs = Math.Max(0, schedule[0].StartMs);
        for (var i = 1; i < schedule.Count; i++)
        {
            var minStart = schedule[i - 1].StartMs + minSpacingMs;
            if (schedule[i].StartMs < minStart)
                schedule[i].StartMs = minStart;
        }

        for (var i = 0; i < schedule.Count; i++)
        {
            var note = schedule[i];
            var gap = note.DurationMs >= MonophonicLongNoteMs ? MonophonicLongGapMs : MonophonicShortGapMs;
            var dur = Math.Clamp(note.DurationMs, MonophonicChordTapMs, MonophonicMaxHoldMs);

            // Direct delivery has no key-up/key-down constraint: honor the note's musical length
            // (staccato stays staccato) and let legato lines ring to just before the next onset.
            var legato = Math.Clamp(
                note.LegatoDurationMs > 0 ? note.LegatoDurationMs : note.DurationMs,
                MonophonicChordTapMs,
                MonophonicMaxHoldMs);

            if (i + 1 < schedule.Count)
            {
                var toNext = schedule[i + 1].StartMs - note.StartMs;
                if (dur > toNext - gap)
                    dur = Math.Max(MonophonicChordTapMs, toNext - gap);
                dur = Math.Min(dur, toNext - 5);
                legato = Math.Max(MonophonicChordTapMs, Math.Min(legato, toNext - 5));
            }

            note.DurationMs = dur;
            note.LegatoDurationMs = legato;
        }
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


