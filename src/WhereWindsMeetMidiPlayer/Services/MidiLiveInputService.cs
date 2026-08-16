using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Multimedia;
using WhereWindsMeetMidiPlayer.Infrastructure;
using WhereWindsMeetMidiPlayer.Models;

namespace WhereWindsMeetMidiPlayer.Services;

public sealed class MidiLiveInputService : IDisposable
{
    private readonly InputService _input;
    private readonly KeyMappingService _keyMapping;
    private readonly NoteRangeService _noteRange;
    private readonly Func<LiveMidiContext> _getContext;
    private readonly object _gate = new();

    private InputDevice? _device;
    private string? _connectedDeviceName;
    private bool _isEnabled;
    private bool _watcherSubscribed;

    public event EventHandler? DevicesChanged;
    public event Action<int>? MappedGameNoteOn;
    public event Action<int, int>? RawNoteOn;
    public event Action<int>? RawNoteOff;

    public MidiLiveInputService(
        InputService input,
        KeyMappingService keyMapping,
        NoteRangeService noteRange,
        Func<LiveMidiContext> getContext)
    {
        _input = input;
        _keyMapping = keyMapping;
        _noteRange = noteRange;
        _getContext = getContext;
    }

    public bool IsEnabled => _isEnabled;
    public string? ConnectedDeviceName => _connectedDeviceName;

    public IReadOnlyList<string> GetDeviceNames()
    {
        try
        {
            return InputDevice.GetAll()
                .Select(d => d.Name)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct()
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex)
        {
            AppPaths.WriteDiagnosticLog("midi-live-enumerate", ex);
            return [];
        }
    }

    public void StartDevicesWatcher()
    {
        if (_watcherSubscribed)
            return;

        try
        {
            DevicesWatcher.Instance.DeviceAdded += OnDevicesWatcherChanged;
            DevicesWatcher.Instance.DeviceRemoved += OnDevicesWatcherChanged;
            _watcherSubscribed = true;
        }
        catch (Exception ex)
        {
            AppPaths.WriteDiagnosticLog("midi-live-watcher", ex);
        }
    }

    public void SetEnabled(bool enabled, string? deviceName)
    {
        lock (_gate)
        {
            _isEnabled = enabled;
            if (!enabled)
            {
                DisconnectDevice();
                return;
            }

            if (string.IsNullOrWhiteSpace(deviceName))
            {
                _isEnabled = false;
                return;
            }

            ConnectDevice(deviceName);
        }
    }

    public void Reconnect(string? deviceName)
    {
        lock (_gate)
        {
            if (!_isEnabled)
                return;

            if (string.IsNullOrWhiteSpace(deviceName))
            {
                DisconnectDevice();
                return;
            }

            ConnectDevice(deviceName);
        }
    }

    private void ConnectDevice(string deviceName)
    {
        DisconnectDevice();

        try
        {
            var device = InputDevice.GetByName(deviceName);
            device.EventReceived += OnEventReceived;
            device.StartEventsListening();
            _device = device;
            _connectedDeviceName = deviceName;
        }
        catch (Exception ex)
        {
            AppPaths.WriteDiagnosticLog("midi-live-connect", ex);
            _connectedDeviceName = null;
            throw;
        }
    }

    private void DisconnectDevice()
    {
        if (_device is null)
        {
            _connectedDeviceName = null;
            return;
        }

        try
        {
            _device.EventReceived -= OnEventReceived;
            if (_device.IsListeningForEvents)
                _device.StopEventsListening();
            _device.Dispose();
        }
        catch (Exception ex)
        {
            AppPaths.WriteDiagnosticLog("midi-live-disconnect", ex);
        }
        finally
        {
            _device = null;
            _connectedDeviceName = null;
        }
    }

    private void OnDevicesWatcherChanged(object? sender, DeviceAddedRemovedEventArgs e) =>
        DevicesChanged?.Invoke(this, EventArgs.Empty);

    private void OnEventReceived(object? sender, MidiEventReceivedEventArgs e)
    {
        if (!_isEnabled)
            return;

        switch (e.Event)
        {
            case NoteOnEvent noteOn when noteOn.Velocity == 0:
                HandleNoteOff(noteOn.NoteNumber);
                break;
            case NoteOnEvent noteOn:
                HandleNoteOn(noteOn);
                break;
            case NoteOffEvent noteOff:
                HandleNoteOff(noteOff.NoteNumber);
                break;
        }
    }

    /// <summary>Same contract as PlaybackEngine.DirectNoteSink: (midiNote, on) → handled.</summary>
    public Func<int, bool, bool>? DirectNoteSink { get; set; }

    // Live direct notes held in-game: source MIDI note → mapped game note (for the note-off).
    private readonly Dictionary<int, int> _liveDirectHeld = new();

    private void HandleNoteOn(NoteOnEvent noteOn)
    {
        RawNoteOn?.Invoke(noteOn.NoteNumber, noteOn.Velocity);

        var context = _getContext();
        var gameNote = LiveMidiMapper.MapToGameNoteNumber(
            noteOn.NoteNumber,
            noteOn.Velocity,
            _noteRange,
            context.SmartTranspose,
            context.StrictNoteRange,
            context.OctaveShift,
            context.MappingMode);

        if (gameNote is null)
            return;

        if (!context.SuppressGameInput)
        {
            // Direct delivery holds the note until the player releases the key (real sustain);
            // keyboard fallback stays a tap like before.
            var sink = DirectNoteSink;
            if (sink is not null && sink(gameNote.Value, true))
            {
                _liveDirectHeld[noteOn.NoteNumber] = gameNote.Value;
                MappedGameNoteOn?.Invoke(gameNote.Value);
                return;
            }

            var combo = _keyMapping.GetKeyCombo(gameNote.Value);
            if (combo is null)
                return;

            MappedGameNoteOn?.Invoke(gameNote.Value);

            _input.QueuePressKeyCombo(combo);
        }
    }

    private void HandleNoteOff(int midiNote)
    {
        if (_liveDirectHeld.Remove(midiNote, out var gameNote))
            DirectNoteSink?.Invoke(gameNote, false);

        RawNoteOff?.Invoke(midiNote);
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _isEnabled = false;
            DisconnectDevice();
        }

        if (_watcherSubscribed)
        {
            try
            {
                DevicesWatcher.Instance.DeviceAdded -= OnDevicesWatcherChanged;
                DevicesWatcher.Instance.DeviceRemoved -= OnDevicesWatcherChanged;
            }
            catch
            {
                // ignore watcher teardown errors
            }

            _watcherSubscribed = false;
        }
    }
}

public sealed class LiveMidiContext
{
    public bool SmartTranspose { get; init; }
    public bool StrictNoteRange { get; init; }
    public int OctaveShift { get; init; }
    public NoteMappingMode MappingMode { get; init; }
    public bool SuppressGameInput { get; init; }
}
