using WhereWindsMeetMidiPlayer.Infrastructure;
using WhereWindsMeetMidiPlayer.Models;
using WhereWindsMeetMidiPlayer.ViewModels;

namespace WhereWindsMeetMidiPlayer.Services;

public sealed class PracticePrepareService
{
    private readonly MidiParserService _midiParser;
    private readonly NoteRangeService _noteRange;

    public static readonly string[] DefaultTrackColors =
        ["#4A9EFF", "#4ADE80", "#F59E0B", "#F472B6", "#A78BFA", "#38BDF8", "#FB7185"];

    public PracticePrepareService(MidiParserService midiParser, NoteRangeService noteRange)
    {
        _midiParser = midiParser;
        _noteRange = noteRange;
    }

    public PracticePrepareResult Prepare(
        string filePath,
        MidiPrepareRequest request,
        KeyMappingService keyMapping,
        IReadOnlyList<PracticeTrackOption> trackOptions,
        PracticeKeyboardViewMode viewMode = PracticeKeyboardViewMode.GameAdapted36)
    {
        var parsed = _midiParser.Parse(filePath);
        var tracks = _midiParser.GetTracks(filePath);
        var parsedNotes = parsed.Notes.ToList();
        PracticeFingersSidecarService.MergeIntoNotes(filePath, parsedNotes, tracks);

        var enabledTracks = ResolveEnabledTracks(trackOptions, parsedNotes);

        var notes = parsedNotes.Where(n => enabledTracks.Contains(n.TrackIndex)).ToList();
        var colorByTrack = BuildColorMap(trackOptions, parsedNotes);

        List<PracticeVisualNote> visual;
        if (viewMode == PracticeKeyboardViewMode.FullPiano88)
        {
            visual = BuildFullPianoVisual(notes, enabledTracks, colorByTrack, request);
        }
        else
        {
            visual = BuildAdaptedVisual(notes, enabledTracks, colorByTrack, request);
        }

        visual = PracticeFingersSidecarService.MergeIntoVisualNotes(filePath, visual, tracks);

        return new PracticePrepareResult
        {
            VisualNotes = visual,
            DurationMs = parsed.DurationMs,
            Tracks = tracks,
            NoteCount = visual.Count,
            ViewMode = viewMode,
            SourceNoteMin = notes.Count > 0 ? notes.Min(n => n.NoteNumber) : 0,
            SourceNoteMax = notes.Count > 0 ? notes.Max(n => n.NoteNumber) : 0
        };
    }

    private static HashSet<int> ResolveEnabledTracks(
        IReadOnlyList<PracticeTrackOption> trackOptions,
        IReadOnlyList<NormalizedNote> allNotes)
    {
        if (trackOptions.Count > 0)
        {
            var enabled = trackOptions.Where(t => t.IsEnabled).Select(t => t.TrackIndex).ToHashSet();
            if (enabled.Count > 0)
                return enabled;
        }

        return allNotes.Select(n => n.TrackIndex).Distinct().ToHashSet();
    }

    private static Dictionary<int, string> BuildColorMap(
        IReadOnlyList<PracticeTrackOption> trackOptions,
        IReadOnlyList<NormalizedNote> allNotes)
    {
        var map = trackOptions.ToDictionary(t => t.TrackIndex, t => t.ColorHex);
        var trackIndices = allNotes.Select(n => n.TrackIndex).Distinct().OrderBy(i => i).ToList();
        for (var i = 0; i < trackIndices.Count; i++)
        {
            var idx = trackIndices[i];
            if (!map.ContainsKey(idx))
                map[idx] = DefaultTrackColors[i % DefaultTrackColors.Length];
        }

        return map;
    }

    private List<PracticeVisualNote> BuildFullPianoVisual(
        List<NormalizedNote> notes,
        HashSet<int> enabledTracks,
        Dictionary<int, string> colorByTrack,
        MidiPrepareRequest request)
    {
        var semitones = request.OctaveShift * 12;
        var shifted = semitones == 0
            ? notes
            : MidiTransposeService.ApplyTranspose(notes, semitones);

        return shifted
            .Where(n => enabledTracks.Contains(n.TrackIndex))
            .Where(n => n.NoteNumber >= NoteNames.MinPianoNote && n.NoteNumber <= NoteNames.MaxPianoNote)
            .Select(n => new PracticeVisualNote
            {
                StartMs = n.StartMs,
                DurationMs = Math.Max(n.DurationMs, 80),
                NoteNumber = n.NoteNumber,
                GameNoteNumber = MapGameNoteNumber(n, request),
                TrackIndex = n.TrackIndex,
                ColorHex = colorByTrack.TryGetValue(n.TrackIndex, out var c)
                    ? c
                    : DefaultTrackColors[n.TrackIndex % DefaultTrackColors.Length],
                FingerNumber = n.FingerNumber
            })
            .OrderBy(n => n.StartMs)
            .ToList();
    }

    private List<PracticeVisualNote> BuildAdaptedVisual(
        List<NormalizedNote> notes,
        HashSet<int> enabledTracks,
        Dictionary<int, string> colorByTrack,
        MidiPrepareRequest request)
    {
        var autoTranspose = request.SmartTranspose
            ? MidiTransposeService.DetectBestTranspose(
                notes,
                preferNearestNatural: request.MappingMode == NoteMappingMode.ClosestNatural,
                maxSemitones: 48)
            : 0;

        var totalTranspose = autoTranspose + request.OctaveShift * 12;
        var transposed = totalTranspose == 0
            ? notes.Select(Clone).ToList()
            : MidiTransposeService.ApplyTranspose(notes, totalTranspose);

        // Keep the practice roll consistent with playback's Phrase Fold arrangement.
        if (request.PhraseFold || request.MappingMode == NoteMappingMode.PhraseFold)
            transposed = PhraseFoldService.Apply(transposed);

        var ranged = _noteRange.ApplyRange(transposed, smartTranspose: true, strictMode: request.StrictNoteRange);
        var mapped = NoteMappingService.ApplyMappingMode(ranged.Notes, request.MappingMode);

        return mapped
            .Where(n => !n.Skipped && enabledTracks.Contains(n.TrackIndex))
            .Select(n => new PracticeVisualNote
            {
                StartMs = n.StartMs,
                DurationMs = Math.Max(n.DurationMs, 80),
                NoteNumber = n.NoteNumber,
                GameNoteNumber = n.NoteNumber,
                TrackIndex = n.TrackIndex,
                ColorHex = colorByTrack.TryGetValue(n.TrackIndex, out var c)
                    ? c
                    : DefaultTrackColors[n.TrackIndex % DefaultTrackColors.Length],
                FingerNumber = n.FingerNumber
            })
            .OrderBy(n => n.StartMs)
            .ToList();
    }

    private int MapGameNoteNumber(NormalizedNote note, MidiPrepareRequest request) =>
        LiveMidiMapper.MapToGameNoteNumber(
            note.OriginalNoteNumber > 0 ? note.OriginalNoteNumber : note.NoteNumber,
            note.Velocity,
            _noteRange,
            request.SmartTranspose,
            request.StrictNoteRange,
            request.OctaveShift,
            request.MappingMode) ?? 0;

    private static NormalizedNote Clone(NormalizedNote n) => new()
    {
        OriginalNoteNumber = n.OriginalNoteNumber,
        NoteName = n.NoteName,
        StartMs = n.StartMs,
        DurationMs = n.DurationMs,
        Velocity = n.Velocity,
        TrackIndex = n.TrackIndex,
        Channel = n.Channel,
        NoteNumber = n.NoteNumber,
        Skipped = n.Skipped,
        FingerNumber = n.FingerNumber
    };
}

public sealed class PracticePrepareResult
{
    public List<PracticeVisualNote> VisualNotes { get; init; } = [];
    public long DurationMs { get; init; }
    public IReadOnlyList<MidiTrackInfo> Tracks { get; init; } = [];
    public int NoteCount { get; init; }
    public PracticeKeyboardViewMode ViewMode { get; init; } = PracticeKeyboardViewMode.GameAdapted36;
    public int SourceNoteMin { get; init; }
    public int SourceNoteMax { get; init; }
}
