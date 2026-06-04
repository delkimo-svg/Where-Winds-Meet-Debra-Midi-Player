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
            var timedNotes = trackChunk.GetNotes();
            foreach (var note in timedNotes)
            {
                if (ignorePercussion && note.Channel == PercussionChannel)
                    continue;

                var start = note.TimeAs<MetricTimeSpan>(tempoMap);
                var length = note.LengthAs<MetricTimeSpan>(tempoMap);
                var startMs = (long)Math.Round(start.TotalMicroseconds / 1000.0);
                var durationMs = Math.Max(1, (long)Math.Round(length.TotalMicroseconds / 1000.0));

                notes.Add(new NormalizedNote
                {
                    NoteNumber = note.NoteNumber,
                    OriginalNoteNumber = note.NoteNumber,
                    NoteName = NoteNames.FromMidiNumber(note.NoteNumber),
                    StartMs = startMs,
                    DurationMs = durationMs,
                    Velocity = note.Velocity,
                    TrackIndex = trackIndex,
                    Channel = note.Channel
                });
            }

            trackIndex++;
        }

        notes.Sort((a, b) =>
        {
            var cmp = a.StartMs.CompareTo(b.StartMs);
            return cmp != 0 ? cmp : a.NoteNumber.CompareTo(b.NoteNumber);
        });

        var durationMsTotal = notes.Count == 0
            ? 0
            : notes.Max(n => n.StartMs + n.DurationMs);

        return new MidiParseResult
        {
            FilePath = filePath,
            Title = MidiTitleHelper.ResolveTitle(midiFile, filePath),
            DurationMs = durationMsTotal,
            Notes = notes
        };
    }
}

public sealed class MidiParseResult
{
    public string FilePath { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public long DurationMs { get; init; }
    public IReadOnlyList<NormalizedNote> Notes { get; init; } = [];
}
