using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;

var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
var outDir = Path.Combine(root, "src", "WhereWindsMeetMidiPlayer", "Assets", "academy-pack", "BB");
Directory.CreateDirectory(outDir);

// Snappy learning tempo — short notes, clear rhythm.
const int tempoBpm = 128;
const int ticksPerQuarter = 480;
const int eighthSpacing = ticksPerQuarter / 2;
const int eighthLength = 200;
const int quarterSpacing = ticksPerQuarter;
const int quarterLength = 360;

WriteRhythm(outDir, "Academy_BB_EX01_Find-Middle-C-RH.mid", tempoBpm, 92,
    [60, 60, 60, 60], eighthSpacing, eighthLength);
WriteRhythm(outDir, "Academy_BB_EX02_C-Five-Finger-RH.mid", tempoBpm, 90,
    [60, 62, 64, 65, 67, 65, 64, 62, 60], eighthSpacing, eighthLength);
WriteRhythm(outDir, "Academy_BB_EX03_RH-Quarter-Pulses.mid", tempoBpm, 90,
    [60, 60, 60, 60], quarterSpacing, quarterLength);
WriteRhythm(outDir, "Academy_BB_EX04_Middle-C-LH.mid", tempoBpm, 88,
    [48, 50, 52, 53, 55, 53, 52, 50, 48], eighthSpacing, eighthLength);

Console.WriteLine($"Wrote exercise MIDIs to {outDir} at {tempoBpm} BPM");

static void WriteRhythm(
    string dir,
    string fileName,
    int tempoBpm,
    int velocity,
    int[] notes,
    int spacingTicks,
    int lengthTicks)
{
    var track = new TrackChunk();
    var microsecondsPerQuarter = 60_000_000 / tempoBpm;

    track.Events.Add(new SetTempoEvent((int)microsecondsPerQuarter) { DeltaTime = 0 });

    for (var i = 0; i < notes.Length; i++)
    {
        var onDelta = i == 0 ? 0 : spacingTicks - lengthTicks;
        if (onDelta < 0)
            onDelta = 0;

        track.Events.Add(new NoteOnEvent((SevenBitNumber)notes[i], (SevenBitNumber)velocity) { DeltaTime = onDelta });
        track.Events.Add(new NoteOffEvent((SevenBitNumber)notes[i], SevenBitNumber.MinValue) { DeltaTime = lengthTicks });
    }

    var file = new MidiFile(track);
    file.Write(Path.Combine(dir, fileName), true);
}
