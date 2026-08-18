using H.Formatters;
using H.Pipes;
using H.Pipes.AccessControl;
using H.Pipes.Args;

namespace WhereWindsMeetMidiPlayer.Services;

/// <summary>
/// Wire payload of the Hypnotoad Dalamud plugin (same protocol as LightAmp's DalamudBridge):
/// H.Pipes named pipe "Hypnotoad", Newtonsoft JSON, lower-case property names.
/// </summary>
public sealed class HypnotoadIpcMessage
{
    public int msgType { get; set; }
    public int msgChannel { get; set; }
    public string message { get; set; } = "";
}

/// <summary>FFXIV chat channel codes understood by Hypnotoad's ChatMessageChannelType.</summary>
public static class HypnotoadChatChannel
{
    /// <summary>Raw text — slash commands (/r, /sit, …) pass through to the game untouched.</summary>
    public const int Command = 0x0000;
    public const int Say = 0x000A;
    public const int Shout = 0x000B;
    public const int Party = 0x000E;
    public const int FreeCompany = 0x0018;
    public const int Yell = 0x001E;
}

/// <summary>
/// Hosts the named-pipe server the Hypnotoad Dalamud plugin auto-connects to.
/// Chat sent this way is delivered in-process by the plugin — the game's input box never
/// opens, so playback keystrokes are never swallowed. Only one server can own the pipe:
/// running LightAmp/BMP at the same time keeps the plugin attached to whichever grabbed it first.
/// </summary>
public sealed class HypnotoadService : IDisposable
{
    private const string PipeName = "Hypnotoad";
    private const int MsgTypeHandshake = 1;
    private const int MsgTypeNameAndHomeWorld = 11;
    private const int MsgTypeInstrument = 20;
    private const int MsgTypeNoteOn = 21;
    private const int MsgTypeProgramChange = 23;
    private const int MsgTypeNoteOff = 22;
    private const int MsgTypeChat = 40;
    // Note wire value = MIDI − 48 (C3 = 0 … C6 = 36), same scheme as MidiBard/LightAmp:
    // the plugin stores it as game note (wire + 39) in AgentPerformance.CurrentPressingNote.
    private const int NoteWireFloor = 48;
    private const int NoteWireMax = 36;

    private PipeServer<HypnotoadIpcMessage>? _server;
    private int _clientCount;
    private bool _starting;

    public bool IsRunning => _server is not null;
    public bool IsClientConnected => Volatile.Read(ref _clientCount) > 0;
    /// <summary>Logged-in character name, from the plugin's NameAndHomeWorld push ("pid:Name:worldId").</summary>
    public string CharacterName { get; private set; } = string.Empty;
    public string LastError { get; private set; } = string.Empty;

    /// <summary>Raised from background threads on connect/disconnect/error — marshal to UI.</summary>
    public event EventHandler? StateChanged;

    public async Task StartAsync()
    {
        if (_server is not null || _starting)
            return;

        _starting = true;
        var server = new PipeServer<HypnotoadIpcMessage>(PipeName, formatter: new NewtonsoftJsonFormatter());
        // The app runs elevated (requireAdministrator) while FFXIV runs as the normal user:
        // without this ACL the plugin's connect is denied and it silently retries forever.
        // Same call LightAmp's DalamudServer makes.
        server.AllowUsersReadWrite();
        server.ClientConnected += OnClientConnected;
        server.ClientDisconnected += OnClientDisconnected;
        server.MessageReceived += OnMessageReceived;
        server.ExceptionOccurred += OnExceptionOccurred;
        try
        {
            await server.StartAsync().ConfigureAwait(false);
            _server = server;
            LastError = string.Empty;
        }
        catch (Exception ex)
        {
            // Pipe name already owned (LightAmp/BMP running) or ACL failure.
            LastError = ex.Message;
            await SafeDisposeAsync(server).ConfigureAwait(false);
        }
        finally
        {
            _starting = false;
        }

        RaiseStateChanged();
    }

    public async Task StopAsync()
    {
        var server = _server;
        _server = null;
        _clientCount = 0;
        CharacterName = string.Empty;
        if (server is not null)
            await SafeDisposeAsync(server).ConfigureAwait(false);

        RaiseStateChanged();
    }

    /// <summary>Sends chat to the game. Channel 0 delivers the text verbatim (slash commands work);
    /// any other code makes the plugin prepend that channel's shortcut (/s, /y, …).</summary>
    public async Task<bool> SendChatAsync(int channelCode, string text)
    {
        var server = _server;
        if (server is null || !IsClientConnected || string.IsNullOrWhiteSpace(text))
            return false;

        try
        {
            await server.WriteAsync(new HypnotoadIpcMessage
            {
                msgType = MsgTypeChat,
                msgChannel = channelCode,
                message = text
            }).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            RaiseStateChanged();
            return false;
        }
    }

    /// <summary>Equips a performance instrument in-game (Perform-sheet row id, 1 = Harp).
    /// The plugin runs the game's DoPerformAction on the next framework tick, which opens
    /// the performance UI with that instrument — same call LightAmp's "open instrument" makes.</summary>
    public async Task<bool> OpenInstrumentAsync(uint instrumentId)
    {
        var server = _server;
        if (server is null || !IsClientConnected)
            return false;

        try
        {
            await server.WriteAsync(new HypnotoadIpcMessage
            {
                msgType = MsgTypeInstrument,
                msgChannel = 0,
                message = instrumentId.ToString()
            }).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            RaiseStateChanged();
            return false;
        }
    }

    /// <summary>
    /// Plays/releases a note in-game through the plugin (no keyboard involved). Fire-and-forget:
    /// returns false when not connected or out of the game's C3–C6 range so the caller can fall
    /// back to keyboard delivery for that note.
    /// </summary>
    public bool SendNote(int midiNote, bool on)
    {
        var server = _server;
        if (server is null || !IsClientConnected)
            return false;

        var wire = midiNote - NoteWireFloor;
        if (wire is < 0 or > NoteWireMax)
            return false;

        _ = WriteNoteAsync(server, wire, on);
        return true;
    }

    /// <summary>Switches the electric-guitar tone in-game (0 Overdriven … 4 Special).
    /// Fire-and-forget like notes; false when not connected so the caller can fall back
    /// to the game's Tone keybinds.</summary>
    public bool SendProgramChange(int tone)
    {
        var server = _server;
        if (server is null || !IsClientConnected || tone is < 0 or > 4)
            return false;

        _ = WriteToneAsync(server, tone);
        return true;
    }

    private async Task WriteToneAsync(PipeServer<HypnotoadIpcMessage> server, int tone)
    {
        try
        {
            await server.WriteAsync(new HypnotoadIpcMessage
            {
                msgType = MsgTypeProgramChange,
                msgChannel = 0,
                message = tone.ToString()
            }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            RaiseStateChanged();
        }
    }

    private async Task WriteNoteAsync(PipeServer<HypnotoadIpcMessage> server, int wireNote, bool on)
    {
        try
        {
            await server.WriteAsync(new HypnotoadIpcMessage
            {
                msgType = on ? MsgTypeNoteOn : MsgTypeNoteOff,
                msgChannel = 0,
                message = wireNote.ToString()
            }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            RaiseStateChanged();
        }
    }

    private void OnClientConnected(object? sender, ConnectionEventArgs<HypnotoadIpcMessage> e)
    {
        Interlocked.Increment(ref _clientCount);
        RaiseStateChanged();
    }

    private void OnClientDisconnected(object? sender, ConnectionEventArgs<HypnotoadIpcMessage> e)
    {
        if (Interlocked.Decrement(ref _clientCount) <= 0)
        {
            _clientCount = 0;
            CharacterName = string.Empty;
        }

        RaiseStateChanged();
    }

    private void OnMessageReceived(object? sender, ConnectionMessageEventArgs<HypnotoadIpcMessage?> e)
    {
        var msg = e.Message;
        if (msg is null)
            return;

        switch (msg.msgType)
        {
            case MsgTypeHandshake:
                RaiseStateChanged();
                break;
            case MsgTypeNameAndHomeWorld:
                var parts = msg.message.Split(':');
                if (parts.Length >= 2 && !string.IsNullOrWhiteSpace(parts[1]))
                {
                    CharacterName = parts[1].Trim();
                    RaiseStateChanged();
                }
                break;
        }
    }

    private void OnExceptionOccurred(object? sender, ExceptionEventArgs e)
    {
        LastError = e.Exception.Message;
        RaiseStateChanged();
    }

    private void RaiseStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);

    private static async Task SafeDisposeAsync(PipeServer<HypnotoadIpcMessage> server)
    {
        try
        {
            await server.DisposeAsync().ConfigureAwait(false);
        }
        catch
        {
            // Best effort — the pipe handle dies with the process anyway.
        }
    }

    public void Dispose() => _ = StopAsync();
}
