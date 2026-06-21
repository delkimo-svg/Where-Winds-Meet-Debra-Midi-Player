using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using WhereWindsMeetMidiPlayer.Helpers;
using WhereWindsMeetMidiPlayer.Infrastructure;
using WhereWindsMeetMidiPlayer.Models;

namespace WhereWindsMeetMidiPlayer.Services;

public sealed class MidiParserService
{
    private const int PercussionChannel = 9;

    public MidiParseResult Parse(string filePath, bool ignorePercussion = true)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("MIDI file not found.", filePath);

        var midiFile = MidiFile.Read(filePath, MidiTextEncoding.ReadingSettings);
        var tempoMap = midiFile.GetTempoMap();
        var trackIndex = 0;
        var notes = new List<NormalizedNote>();

        foreach (var trackChunk in midiFile.GetTrackChunks())
        {
            var fingerByNote = ExtractFingerAssignments(trackChunk, tempoMap);
            var timedNotes = trackChunk.GetNotes();
            foreach (var note in timedNotes)
            {
                if (ignorePercussion && note.Channel == PercussionChannel)
                    continue;

                var start = note.TimeAs<MetricTimeSpan>(tempoMap);
                var length = note.LengthAs<MetricTimeSpan>(tempoMap);
                var startMs = (long)Math.Round(start.TotalMicroseconds / 1000.0);
                var durationMs = Math.Max(1, (long)Math.Round(length.TotalMicroseconds / 1000.0));
                fingerByNote.TryGetValue((startMs, note.NoteNumber, note.Channel), out var fingerNumber);

                notes.Add(new NormalizedNote
                {
                    NoteNumber = note.NoteNumber,
                    OriginalNoteNumber = note.NoteNumber,
                    NoteName = NoteNames.FromMidiNumber(note.NoteNumber),
                    StartMs = startMs,
                    DurationMs = durationMs,
                    Velocity = note.Velocity,
                    TrackIndex = trackIndex,
                    Channel = note.Channel,
                    FingerNumber = fingerNumber
                });
            }

            trackIndex++;
        }

        notes.Sort((a, b) =>
        {
            var cmp = a.StartMs.CompareTo(b.StartMs);
            return cmp != 0 ? cmp : a.NoteNumber.CompareTo(b.NoteNumber);
        });

        // Illegal MIDI: duplicate NoteOn for the same key/channel at the same time — keep one (highest velocity).
        notes = notes
            .GroupBy(n => (n.StartMs, n.NoteNumber, n.Channel))
            .Select(g => g.OrderByDescending(n => n.Velocity).First())
            .OrderBy(n => n.StartMs)
            .ThenBy(n => n.NoteNumber)
            .ToList();

        var durationMsTotal = notes.Count == 0
            ? 0
            : notes.Max(n => n.StartMs + n.DurationMs);

        return new MidiParseResult
        {
            FilePath = filePath,
            Title = MidiTitleHelper.ResolveTitle(midiFile, filePath),
            DurationMs = durationMsTotal,
            BeatsPerMinute = GetInitialBeatsPerMinute(tempoMap),
            Notes = notes
        };
    }

    public IReadOnlyList<MidiTrackInfo> GetTracks(string filePath)
    {
        if (!File.Exists(filePath))
            return [];

        var midiFile = MidiFile.Read(filePath, MidiTextEncoding.ReadingSettings);
        var tracks = new List<MidiTrackInfo>();
        var trackIndex = 0;

        foreach (var trackChunk in midiFile.GetTrackChunks())
        {
            var name = string.Empty;
            foreach (var midiEvent in trackChunk.Events)
            {
                if (midiEvent is SequenceTrackNameEvent nameEvent)
                {
                    name = CleanTrackName(nameEvent.Text ?? string.Empty);
                    break;
                }
            }

            var noteCount = 0;
            foreach (var note in trackChunk.GetNotes())
            {
                if (note.Channel == PercussionChannel || note.Velocity == 0)
                    continue;
                noteCount++;
            }

            if (noteCount > 0)
            {
                if (string.IsNullOrWhiteSpace(name))
                    name = $"Track {trackIndex + 1}";

                tracks.Add(new MidiTrackInfo
                {
                    Index = trackIndex,
                    Name = name,
                    NoteCount = noteCount
                });
            }

            trackIndex++;
        }

        return tracks;
    }

    private static Dictionary<(long StartMs, int NoteNumber, int Channel), int> ExtractFingerAssignments(
        TrackChunk trackChunk,
        TempoMap tempoMap)
    {
        var result = new Dictionary<(long StartMs, int NoteNumber, int Channel), int>();
        var pendingFinger = 0;

        foreach (var timedEvent in trackChunk.GetTimedEvents())
        {
            switch (timedEvent.Event)
            {
                case LyricEvent lyric:
                    pendingFinger = ParseFingerLyric(lyric.Text);
                    break;
                case NoteOnEvent noteOn when noteOn.Velocity > 0:
                    if (pendingFinger > 0)
                    {
                        var startMs = (long)Math.Round(
                            timedEvent.TimeAs<MetricTimeSpan>(tempoMap).TotalMicroseconds / 1000.0);
                        result[(startMs, noteOn.NoteNumber, noteOn.Channel)] = pendingFinger;
                    }

                    pendingFinger = 0;
                    break;
            }
        }

        return result;
    }

    private static int ParseFingerLyric(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        text = text.Trim();
        if (text.Length != 2)
            return 0;

        var prefix = char.ToLowerInvariant(text[0]);
        if (prefix != 'f' || text[1] is < '1' or > '5')
            return 0;

        return text[1] - '0';
    }

    private static string CleanTrackName(string raw) =>
        string.Concat(raw.Where(c =>
            char.IsAsciiLetterOrDigit(c) || c is ' ' or '-' or '_' or '.' or '(' or ')'))
            .Trim();

    private static double GetInitialBeatsPerMinute(TempoMap tempoMap)
    {
        try
        {
            var tempo = tempoMap.GetTempoAtTime(new MetricTimeSpan(0));
            return tempo.MicrosecondsPerQuarterNote > 0
                ? 60000000.0 / tempo.MicrosecondsPerQuarterNote
                : 120;
        }
        catch
        {
            return 120;
        }
    }
}

public sealed class MidiParseResult
{
    public string FilePath { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public long DurationMs { get; init; }
    public double BeatsPerMinute { get; init; } = 120;
    public IReadOnlyList<NormalizedNote> Notes { get; init; } = [];
}
