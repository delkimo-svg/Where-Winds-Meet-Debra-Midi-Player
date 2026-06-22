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
        var notes = FilterTracks(parsed.Notes, request.TrackIndex);

        var autoTranspose = request.SmartTranspose
            ? MidiTransposeService.DetectBestTranspose(
                notes,
                preferNearestNatural: request.MappingMode == NoteMappingMode.ClosestNatural)
            : 0;

        var totalTranspose = autoTranspose + request.OctaveShift * 12;
        var transposed = totalTranspose == 0
            ? notes.Select(CloneNote).ToList()
            : MidiTransposeService.ApplyTranspose(notes, totalTranspose);

        var ranged = _noteRange.ApplyRange(
            transposed,
            smartTranspose: true,
            strictMode: request.StrictNoteRange);

        var mapped = NoteMappingService.ApplyMappingMode(ranged.Notes, request.MappingMode);

        var schedule = PlaybackEngine.BuildSchedule(
            mapped,
            keyMapping,
            request.ChordRollDelayMs,
            request.NoteDelayMs);

        return new MidiPrepareResult
        {
            Parsed = parsed,
            Ranged = ranged,
            Schedule = schedule,
            AppliedTransposeSemitones = totalTranspose,
            Tracks = _midiParser.GetTracks(filePath)
        };
    }

    private static List<NormalizedNote> FilterTracks(IReadOnlyList<NormalizedNote> notes, int trackIndex)
    {
        if (trackIndex < 0)
            return notes.Select(CloneNote).ToList();

        return notes.Where(n => n.TrackIndex == trackIndex).Select(CloneNote).ToList();
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
    public NoteMappingMode MappingMode { get; init; } = NoteMappingMode.Chromatic36;
    public int ChordRollDelayMs { get; init; }
    public int NoteDelayMs { get; init; }
}

public sealed class MidiPrepareResult
{
    public MidiParseResult Parsed { get; init; } = new();
    public NoteRangeResult Ranged { get; init; } = new();
    public List<ScheduledNote> Schedule { get; init; } = [];
    public int AppliedTransposeSemitones { get; init; }
    public IReadOnlyList<MidiTrackInfo> Tracks { get; init; } = [];
}
