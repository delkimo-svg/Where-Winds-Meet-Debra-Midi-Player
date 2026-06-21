using WhereWindsMeetMidiPlayer.Models;
using WhereWindsMeetMidiPlayer.Services.Audio;

namespace WhereWindsMeetMidiPlayer.Services;

/// <summary>Plays practice chart and live notes through the built-in software synth (not the game).</summary>
public sealed class PracticeSoundService : IDisposable
{
    private readonly MidiSoundEngine _engine;
    private readonly ChartSoundScheduler _chart;
    private readonly HashSet<int> _activeLiveNotes = new();

    public PracticeSoundService(MidiSoundEngine engine)
    {
        _engine = engine;
        _chart = new ChartSoundScheduler(engine);
    }

    public bool IsAvailable => _engine.IsAvailable;

    public bool IsEnabled { get; set; }

    public void SetMasterVolume(float volume01) => _engine.SetMasterVolume(volume01);

    public void ResetSession()
    {
        _activeLiveNotes.Clear();
        _chart.Reset();
    }

    public void ProcessChartPosition(IReadOnlyList<PracticeVisualNote> notes, long positionMs, int windowMs = 45)
    {
        if (!IsEnabled)
            return;

        foreach (var note in notes)
        {
            if (Math.Abs(note.StartMs - positionMs) > windowMs)
                continue;

            _chart.PlayOnce(note);
        }
    }

    public void PlayChartNoteOnce(PracticeVisualNote note)
    {
        if (!IsEnabled)
            return;

        _chart.PlayOnce(note);
    }

    public void PlayLiveNote(int noteNumber, int velocity)
    {
        if (!IsEnabled)
            return;

        if (noteNumber is < 0 or > 127)
            return;

        _engine.NoteOn(noteNumber, Math.Clamp(velocity, 1, 127));
        _activeLiveNotes.Add(noteNumber);
    }

    public void StopLiveNote(int noteNumber)
    {
        if (!IsEnabled)
            return;

        if (_activeLiveNotes.Remove(noteNumber))
            _engine.NoteOff(noteNumber);
    }

    public void StopAllNotes()
    {
        _activeLiveNotes.Clear();
        _chart.Reset();
    }

    public void Dispose()
    {
        StopAllNotes();
    }
}
