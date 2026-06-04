namespace WhereWindsMeetMidiPlayer.Models;

/// <summary>
/// How keystrokes are delivered to the game (mirrors SnowiyQ Where-Winds-Meet-Midi-Player).
/// </summary>
public enum InputDeliveryMode
{
    /// <summary>SendInput first (game focused), then PostMessage if needed.</summary>
    Auto,

    /// <summary>PostMessage WM_KEYDOWN/UP to game HWND — background (not minimized).</summary>
    LocalPostMessage
}
