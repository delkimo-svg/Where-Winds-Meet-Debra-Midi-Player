using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;

var path = args.Length > 0 ? args[0] : "";
if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
{
    Console.WriteLine("Usage: MidiInspect <path.mid>");
    return 1;
}

var mf = MidiFile.Read(path);
var tempoMap = mf.GetTempoMap();
var trackIndex = 0;
foreach (var track in mf.GetTrackChunks())
{
    var notes = track.GetNotes();
    if (notes.Count == 0)
    {
        trackIndex++;
        continue;
    }

    Console.WriteLine(
        $"Track {trackIndex}: notes={notes.Count} min={notes.Min(n => n.NoteNumber)} max={notes.Max(n => n.NoteNumber)}");
    trackIndex++;
}

var all = mf.GetTrackChunks().SelectMany(t => t.GetNotes()).ToList();
if (all.Count > 0)
    Console.WriteLine($"ALL: notes={all.Count} min={all.Min(n => n.NoteNumber)} max={all.Max(n => n.NoteNumber)}");

return 0;
