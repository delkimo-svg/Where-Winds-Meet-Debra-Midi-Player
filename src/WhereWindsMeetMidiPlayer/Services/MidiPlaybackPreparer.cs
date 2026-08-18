using WhereWindsMeetMidiPlayer.Infrastructure;
using WhereWindsMeetMidiPlayer.Models;

namespace WhereWindsMeetMidiPlayer.Services;

public sealed class MidiPlaybackPreparer
{
    private readonly MidiParserService _midiParser;
    private readonly NoteRangeService _noteRange;

    public MidiPlaybackPreparer(MidiParserService midiParser, NoteRangeService noteRange)
    {
        _midiParser = midiParser;
        _noteRange = noteRange;
    }

    public MidiPrepareResult Prepare(string filePath, MidiPrepareRequest request, KeyMappingService keyMapping)
    {
        var parsed = _midiParser.Parse(filePath);
        var tracks = _midiParser.GetTracks(filePath);
        var notes = FilterTracks(parsed.Notes, request.TrackIndex, request.MutedTracks);

        // FFXIV-only arrangement pass (WWM untouched): octave suffixes, chord alignment,
        // then reduction — before transpose so detection scores only the notes actually played.
        if (GameProfiles.Current.Monophonic)
        {
            if (request.FfxivTrackOctaveSuffix)
                FfxivArrangeService.ApplyTrackOctaveSuffixes(notes, tracks);
            notes = FfxivArrangeService.AlignNearSimultaneous(notes, request.FfxivChordAlignWindowMs);
            if (request.FfxivChordReduction)
                notes = FfxivArrangeService.ReduceChords(notes);
            if (request.FfxivAdaptiveVoicing)
                notes = FfxivArrangeService.LimitChordVoicesBySpacing(notes, rollSpacingMs: 30, breatherMs: 60);
        }

        var autoTranspose = request.SmartTranspose
            ? MidiTransposeService.DetectBestTranspose(
                notes,
                preferNearestNatural: request.MappingMode == NoteMappingMode.ClosestNatural)
            : 0;

        var totalTranspose = autoTranspose + request.OctaveShift * 12;
        var transposed = totalTranspose == 0
            ? notes.Select(CloneNote).ToList()
            : MidiTransposeService.ApplyTranspose(notes, totalTranspose);

        // Phrase Fold (additive with any mapping mode): shift out-of-range passages by whole
        // octaves (contour preserved) before the per-note fold handles whatever still sticks out.
        if (request.PhraseFold || request.MappingMode == NoteMappingMode.PhraseFold)
            transposed = PhraseFoldService.Apply(transposed);

        var ranged = _noteRange.ApplyRange(
            transposed,
            smartTranspose: true,
            strictMode: request.StrictNoteRange);

        var mapped = NoteMappingService.ApplyMappingMode(ranged.Notes, request.MappingMode);

        var schedule = PlaybackEngine.BuildSchedule(
            mapped,
            keyMapping,
            request.ChordRollDelayMs,
            request.NoteDelayMs,
            request.FfxivMinNoteSpacingMs);

        // FFXIV electric guitar: weave tone-change events (program changes 27–31) into the
        // schedule so the engine fires them in time — before any note at the same instant.
        if (GameProfiles.Current.Monophonic)
            schedule = MergeGuitarTones(schedule, filePath, request);

        return new MidiPrepareResult
        {
            Parsed = parsed,
            Ranged = ranged,
            Schedule = schedule,
            AppliedTransposeSemitones = totalTranspose,
            Tracks = tracks
        };
    }

    private List<ScheduledNote> MergeGuitarTones(
        List<ScheduledNote> schedule, string filePath, MidiPrepareRequest request)
    {
        var tones = new List<ScheduledNote>();
        var lastTone = -1;
        foreach (var tone in _midiParser.GetGuitarToneEvents(filePath))
        {
            if (request.TrackIndex >= 0 && tone.TrackIndex != request.TrackIndex)
                continue;
            if (request.MutedTracks is { Count: > 0 } && request.MutedTracks.Contains(tone.TrackIndex))
                continue;
            if (tone.Tone == lastTone)
                continue;

            lastTone = tone.Tone;
            tones.Add(new ScheduledNote { StartMs = tone.StartMs, GuitarTone = tone.Tone });
        }

        if (tones.Count == 0)
            return schedule;

        // Stable merge of two sorted lists; ties put the tone change ahead of the notes it colors.
        var merged = new List<ScheduledNote>(schedule.Count + tones.Count);
        int noteIdx = 0, toneIdx = 0;
        while (noteIdx < schedule.Count || toneIdx < tones.Count)
        {
            if (toneIdx < tones.Count &&
                (noteIdx >= schedule.Count || tones[toneIdx].StartMs <= schedule[noteIdx].StartMs))
                merged.Add(tones[toneIdx++]);
            else
                merged.Add(schedule[noteIdx++]);
        }

        return merged;
    }

    private static List<NormalizedNote> FilterTracks(
        IReadOnlyList<NormalizedNote> notes,
        int trackIndex,
        IReadOnlyCollection<int>? mutedTracks)
    {
        IEnumerable<NormalizedNote> filtered = notes;
        if (trackIndex >= 0)
            filtered = filtered.Where(n => n.TrackIndex == trackIndex);
        if (mutedTracks is { Count: > 0 })
            filtered = filtered.Where(n => !mutedTracks.Contains(n.TrackIndex));

        return filtered.Select(CloneNote).ToList();
    }

    private static NormalizedNote CloneNote(NormalizedNote n) => new()
    {
        OriginalNoteNumber = n.OriginalNoteNumber,
        NoteName = n.NoteName,
        StartMs = n.StartMs,
        DurationMs = n.DurationMs,
        Velocity = n.Velocity,
        TrackIndex = n.TrackIndex,
        Channel = n.Channel,
        NoteNumber = n.NoteNumber,
        Skipped = n.Skipped
    };
}

public sealed class MidiPrepareRequest
{
    public bool SmartTranspose { get; init; } = true;
    public bool StrictNoteRange { get; init; }
    public int OctaveShift { get; init; }
    public int TrackIndex { get; init; } = -1;
    /// <summary>MIDI track indexes silenced by the player's track mixer.</summary>
    public IReadOnlyCollection<int>? MutedTracks { get; init; }
    public NoteMappingMode MappingMode { get; init; } = NoteMappingMode.Chromatic36;
    /// <summary>Additive on top of MappingMode: shift out-of-range passages as whole phrases.</summary>
    public bool PhraseFold { get; init; }
    public int ChordRollDelayMs { get; init; }
    public int NoteDelayMs { get; init; }
    /// <summary>FFXIV: merge window (ms) for near-simultaneous chord notes. 0 = off.</summary>
    public int FfxivChordAlignWindowMs { get; init; } = 60;
    /// <summary>FFXIV: outer-voices chord reduction (3–4 → 2 notes, 5+ → 3 notes).</summary>
    public bool FfxivChordReduction { get; init; } = true;
    /// <summary>FFXIV: honor "Track+1"/"Track-2" octave suffixes in track names.</summary>
    public bool FfxivTrackOctaveSuffix { get; init; } = true;
    /// <summary>FFXIV: minimum ms between consecutive note-ons (30 = musical floor, ~125 = midi2ffxiv-safe).</summary>
    public int FfxivMinNoteSpacingMs { get; init; } = 30;
    /// <summary>FFXIV: shed chord voices in fast passages so pre-rolls never exceed the game's input rate.</summary>
    public bool FfxivAdaptiveVoicing { get; init; } = true;
}

public sealed class MidiPrepareResult
{
    public MidiParseResult Parsed { get; init; } = new();
    public NoteRangeResult Ranged { get; init; } = new();
    public List<ScheduledNote> Schedule { get; init; } = [];
    public int AppliedTransposeSemitones { get; init; }
    public IReadOnlyList<MidiTrackInfo> Tracks { get; init; } = [];
}
