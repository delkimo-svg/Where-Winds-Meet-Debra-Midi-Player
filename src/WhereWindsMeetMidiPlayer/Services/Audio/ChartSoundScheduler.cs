using WhereWindsMeetMidiPlayer.Models;

namespace WhereWindsMeetMidiPlayer.Services.Audio;

/// <summary>Plays chart notes once at the correct timeline position through a shared synth.</summary>
public sealed class ChartSoundScheduler
{
    private readonly MidiSoundEngine _engine;
    private readonly HashSet<(long StartMs, int NoteNumber)> _played = new();

    public ChartSoundScheduler(MidiSoundEngine engine) => _engine = engine;

    public void Reset()
    {
        _played.Clear();
        _engine.AllNotesOff();
    }

    public void ProcessPosition(IReadOnlyList<SoundChartNote> notes, long positionMs, int windowMs = 45)
    {
        foreach (var note in notes)
        {
            if (Math.Abs(note.StartMs - positionMs) > windowMs)
                continue;

            PlayOnce(note);
        }
    }

    public void PlayOnce(SoundChartNote note)
    {
        var key = (note.StartMs, note.NoteNumber);
        if (!_played.Add(key))
            return;

        var duration = note.DurationMs > 0 ? note.DurationMs : 0;
        _engine.NoteOn(note.NoteNumber, note.Velocity, duration);
    }

    public void PlayOnce(PracticeVisualNote note, int velocity = 90)
    {
        var key = (note.StartMs, note.NoteNumber);
        if (!_played.Add(key))
            return;

        var duration = note.DurationMs > 0 ? note.DurationMs : 0;
        _engine.NoteOn(note.NoteNumber, velocity, duration);
    }
}
