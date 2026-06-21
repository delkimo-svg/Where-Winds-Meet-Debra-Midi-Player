using System.Diagnostics;
using MeltySynth;
using NAudio.Wave;
using WhereWindsMeetMidiPlayer.Infrastructure;

namespace WhereWindsMeetMidiPlayer.Services.Audio;

/// <summary>
/// SoundFont-based software synth (MeltySynth + GeneralUser GS) for chart and monitor playback.
/// </summary>
public sealed class MidiSoundEngine : ISampleProvider, IDisposable
{
    private const int SampleRate = 44100;
    private const int MelodicChannel = 0;
    private const float SoundFontHeadroom = 0.58f;

    private readonly object _gate = new();
    private readonly Synthesizer? _synth;
    private readonly WaveOutEvent? _output;
    private readonly List<ScheduledNoteOff> _scheduledOffs = new();
    private float _masterVolume = 0.85f;
    private bool _disposed;

    public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(SampleRate, 2);

    public bool IsAvailable { get; private set; }

    public MidiSoundEngine()
    {
        try
        {
            var soundFontPath = ResolveSoundFontPath();
            if (soundFontPath is null)
            {
                AppPaths.WriteDiagnosticLog(
                    "midi-sound-engine-init",
                    new FileNotFoundException("GeneralUser GS SoundFont not found under Assets\\Sounds."));
                IsAvailable = false;
                return;
            }

            var settings = new SynthesizerSettings(SampleRate)
            {
                MaximumPolyphony = 64,
                EnableReverbAndChorus = true
            };

            _synth = new Synthesizer(soundFontPath, settings);
            InitializePrograms();

            _output = new WaveOutEvent { DesiredLatency = 120 };
            _output.Init(this);
            _output.Play();
            IsAvailable = true;
        }
        catch (Exception ex)
        {
            AppPaths.WriteDiagnosticLog("midi-sound-engine-init", ex);
            IsAvailable = false;
        }
    }

    public void SetMasterVolume(float volume01) =>
        _masterVolume = Math.Clamp(volume01, 0f, 1f);

    public void NoteOn(int noteNumber, int velocity, long durationMs = 0)
    {
        if (!IsAvailable || _synth is null || noteNumber is < 0 or > 127)
            return;

        velocity = Math.Clamp(velocity, 1, 127);
        var releaseAt = durationMs > 0
            ? Stopwatch.GetTimestamp() + (long)(durationMs * Stopwatch.Frequency / 1000.0)
            : 0L;

        lock (_gate)
        {
            _synth.NoteOn(MelodicChannel, noteNumber, velocity);
            if (releaseAt > 0)
                _scheduledOffs.Add(new ScheduledNoteOff(noteNumber, releaseAt));
        }
    }

    public void NoteOff(int noteNumber)
    {
        if (!IsAvailable || _synth is null || noteNumber is < 0 or > 127)
            return;

        lock (_gate)
        {
            _synth.NoteOff(MelodicChannel, noteNumber);
            _scheduledOffs.RemoveAll(s => s.NoteNumber == noteNumber);
        }
    }

    public void AllNotesOff()
    {
        lock (_gate)
        {
            if (_synth is null)
                return;

            _scheduledOffs.Clear();
            _synth.NoteOffAll(immediate: false);
        }
    }

    public int Read(float[] buffer, int offset, int count)
    {
        if (!IsAvailable || _synth is null)
        {
            Array.Clear(buffer, offset, count);
            return count;
        }

        var frames = count / 2;
        var now = Stopwatch.GetTimestamp();
        var gain = _masterVolume * SoundFontHeadroom;

        lock (_gate)
        {
            ProcessScheduledNoteOffs(now);

            var written = 0;
            while (written < frames)
            {
                var blockFrames = Math.Min(_synth.BlockSize, frames - written);
                var left = new float[blockFrames];
                var right = new float[blockFrames];
                _synth.Render(left, right);

                for (var i = 0; i < blockFrames; i++)
                {
                    var sampleIndex = offset + (written + i) * 2;
                    buffer[sampleIndex] = left[i] * gain;
                    buffer[sampleIndex + 1] = right[i] * gain;
                }

                written += blockFrames;
            }
        }

        return count;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        try
        {
            lock (_gate)
            {
                _scheduledOffs.Clear();
                if (_synth is not null)
                    _synth.NoteOffAll(immediate: true);
            }

            _output?.Stop();
            _output?.Dispose();
        }
        catch (Exception ex)
        {
            AppPaths.WriteDiagnosticLog("midi-sound-engine-dispose", ex);
        }
    }

    private void InitializePrograms()
    {
        if (_synth is null)
            return;

        _synth.Reset();
        for (var channel = 0; channel < 16; channel++)
        {
            if (channel == _synth.PercussionChannel)
                continue;

            // GM acoustic grand piano (program 0).
            _synth.ProcessMidiMessage(channel, 0xC0, 0, 0);
            _synth.ProcessMidiMessage(channel, 0xB0, 7, 110);
        }
    }

    private void ProcessScheduledNoteOffs(long now)
    {
        if (_synth is null)
            return;

        for (var i = _scheduledOffs.Count - 1; i >= 0; i--)
        {
            if (now < _scheduledOffs[i].ReleaseAtTimestamp)
                continue;

            _synth.NoteOff(MelodicChannel, _scheduledOffs[i].NoteNumber);
            _scheduledOffs.RemoveAt(i);
        }
    }

    private static string? ResolveSoundFontPath()
    {
        var baseDir = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(baseDir, "Assets", "Sounds", "GeneralUser-GS.sf2"),
            Path.Combine(baseDir, "Assets", "Sounds", "GeneralUser.sf2")
        };

        foreach (var path in candidates)
        {
            if (File.Exists(path))
                return path;
        }

        return null;
    }

    private sealed record ScheduledNoteOff(int NoteNumber, long ReleaseAtTimestamp);
}
