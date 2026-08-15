using System.Runtime.InteropServices;
using WhereWindsMeetMidiPlayer.Infrastructure;
using WhereWindsMeetMidiPlayer.Models;

namespace WhereWindsMeetMidiPlayer.Services;

public sealed class InputService
{
    private const int InputKeyboard = 1;
    private const uint KeyeventfKeyup = 0x0002;
    private const uint KeyeventfScancode = 0x0008;
    private const uint WmKeydown = 0x0100;
    private const uint WmKeyup = 0x0101;
    private const uint WmSyskeydown = 0x0104;
    private const uint WmSyskeyup = 0x0105;
    private const uint VkShift = 0x10;
    private const uint VkControl = 0x11;
    private const uint VkMenu = 0x12; // Alt
    private const uint MapvkVkToVsc = 0;

    private readonly GameWindowService _gameWindow;
    private readonly object _heldLock = new();
    private readonly HashSet<string> _heldCombos = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _liveQueueLock = new();
    private readonly Queue<string> _liveQueue = new();
    private Func<InputDeliveryMode> _getMode = () => InputDeliveryMode.Auto;
    private Func<int> _getModifierDelayMs = () => 0;
    private Func<int> _getLiveNoteDelayMs = () => 0;
    private Func<int> _getLiveIdenticalKeyGapMs = () => 0;
    private int _liveQueueWorkerActive;
    private long _lastLivePressTick;
    private string? _lastLiveCombo;

    public long KeysSentCount { get; private set; }
    public string LastKeySent { get; private set; } = string.Empty;
    public string LastDeliveryMethod { get; private set; } = string.Empty;

    public InputService(GameWindowService gameWindow) => _gameWindow = gameWindow;

    public void ConfigureMode(Func<InputDeliveryMode> getMode) => _getMode = getMode;

    public void ConfigureModifierDelay(Func<int> getDelayMs) => _getModifierDelayMs = getDelayMs;

    public void ConfigureLiveInputTiming(Func<int> getNoteDelayMs, Func<int> getIdenticalKeyGapMs)
    {
        _getLiveNoteDelayMs = getNoteDelayMs;
        _getLiveIdenticalKeyGapMs = getIdenticalKeyGapMs;
    }

    private int ModifierDelayMs => Math.Max(0, _getModifierDelayMs());

    /// <summary>
    /// Serializes live PC/MIDI key taps with optional spacing (same rules as MIDI playback).
    /// Prevents parallel PostMessage calls from dropping or interleaving fast notes.
    /// </summary>
    public void QueuePressKeyCombo(string combo)
    {
        if (string.IsNullOrWhiteSpace(combo))
            return;

        lock (_liveQueueLock)
        {
            _liveQueue.Enqueue(combo);
            if (_liveQueueWorkerActive == 0)
            {
                _liveQueueWorkerActive = 1;
                Task.Run(ProcessLiveQueue);
            }
        }
    }

    public void ClearLiveQueue()
    {
        lock (_liveQueueLock)
        {
            _liveQueue.Clear();
            _lastLiveCombo = null;
            _lastLivePressTick = 0;
        }
    }

    private void ProcessLiveQueue()
    {
        while (true)
        {
            string combo;
            lock (_liveQueueLock)
            {
                if (_liveQueue.Count == 0)
                {
                    _liveQueueWorkerActive = 0;
                    return;
                }

                combo = _liveQueue.Dequeue();
            }

            var noteDelayMs = Math.Max(0, _getLiveNoteDelayMs());
            if (noteDelayMs > 0)
                Thread.Sleep(noteDelayMs);

            var gapMs = Math.Max(0, _getLiveIdenticalKeyGapMs());
            if (gapMs > 0 &&
                _lastLiveCombo is not null &&
                _lastLiveCombo.Equals(combo, StringComparison.OrdinalIgnoreCase))
            {
                var elapsed = Environment.TickCount64 - _lastLivePressTick;
                if (elapsed < gapMs)
                    Thread.Sleep((int)(gapMs - elapsed));
            }

            PressKeyCombo(combo);
            _lastLiveCombo = combo;
            _lastLivePressTick = Environment.TickCount64;
        }
    }

    public void ResetDiagnostics()
    {
        KeysSentCount = 0;
        LastKeySent = string.Empty;
        LastDeliveryMethod = string.Empty;
    }

    public void PressKeyCombo(string combo)
    {
        if (string.IsNullOrWhiteSpace(combo) || !TryParseCombo(combo, out var keyVk, out var modifierVks))
            return;

        var sent = _getMode() switch
        {
            InputDeliveryMode.LocalPostMessage => TryPostMessage(keyVk, modifierVks),
            _ => TryPostMessage(keyVk, modifierVks) || TrySendInputAttached(keyVk, modifierVks)
        };

        if (sent)
        {
            KeysSentCount++;
            LastKeySent = combo;
        }
    }

    public void KeyDown(string combo) => PressKeyCombo(combo);
    public void KeyUp(string combo) { }

    /// <summary>
    /// Key-down only, held until <see cref="SendKeyUp"/> (FFXIV hold-notes playback).
    /// Modifier down then key down back-to-back, main window only — mirrors BMP/LightAmp FFXIVHook.
    /// </summary>
    public void SendKeyDown(string combo)
    {
        if (string.IsNullOrWhiteSpace(combo) || !TryParseCombo(combo, out var keyVk, out var modifierVks))
            return;

        if (!_gameWindow.TryGetPrimaryWindow(out var hwnd))
            return;

        foreach (var modVk in modifierVks)
            PostKey(hwnd, modVk, true);
        PostKey(hwnd, keyVk, true);

        lock (_heldLock)
            _heldCombos.Add(combo);

        KeysSentCount++;
        LastKeySent = combo;
        LastDeliveryMethod = $"PostMessage(hold)→{_gameWindow.TargetProcessName}";
    }

    /// <summary>Key-up for a held combo: key up then modifier up.</summary>
    public void SendKeyUp(string combo)
    {
        if (string.IsNullOrWhiteSpace(combo) || !TryParseCombo(combo, out var keyVk, out var modifierVks))
            return;

        lock (_heldLock)
            _heldCombos.Remove(combo);

        if (!_gameWindow.TryGetPrimaryWindow(out var hwnd))
            return;

        PostKey(hwnd, keyVk, false);
        for (var i = modifierVks.Count - 1; i >= 0; i--)
            PostKey(hwnd, modifierVks[i], false);
    }

    /// <summary>Releases every still-held combo (stop/pause safety, like BMP's ClearLastPerformanceKeybinds).</summary>
    public void ReleaseAllHeldKeys()
    {
        string[] held;
        lock (_heldLock)
            held = [.. _heldCombos];

        foreach (var combo in held)
            SendKeyUp(combo);
    }

    private bool TryPostMessage(ushort keyVk, List<ushort> modifierVks)
    {
        var targets = _gameWindow.GetMessageTargets();
        if (targets.Count == 0)
            return false;

        // FFXIV (monophonic): post to the main window only, like BMP/LightAmp —
        // child HWNDs would receive the same tap and double-trigger the note.
        var targetCount = GameProfiles.Current.Monophonic ? 1 : targets.Count;
        for (var t = 0; t < targetCount; t++)
        {
            var hwnd = targets[t];
            foreach (var modVk in modifierVks)
                PostKey(hwnd, modVk, true);
            PostKey(hwnd, keyVk, true);
            Thread.Sleep(ModifierDelayMs > 0 ? ModifierDelayMs : 1);
            PostKey(hwnd, keyVk, false);
            for (var i = modifierVks.Count - 1; i >= 0; i--)
                PostKey(hwnd, modifierVks[i], false);
        }

        LastDeliveryMethod = $"PostMessage→{_gameWindow.TargetProcessName} ({targetCount} hwnd)";
        return true;
    }

    private bool TrySendInputAttached(ushort keyVk, List<ushort> modifierVks)
    {
        try
        {
            _gameWindow.WithAttachedInput(() =>
            {
                foreach (var modVk in modifierVks)
                    SendScancode(modVk, false);
                SendScancode(keyVk, false);
                Thread.Sleep(ModifierDelayMs > 0 ? ModifierDelayMs : 2);
                SendScancode(keyVk, true);
                for (var i = modifierVks.Count - 1; i >= 0; i--)
                    SendScancode(modifierVks[i], true);
            });

            LastDeliveryMethod = "SendInput+AttachThread";
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void PostKey(IntPtr hwnd, ushort vk, bool keyDown)
    {
        // Alt travels as a system key (BMP posts WM_SYSKEYDOWN/UP for it), with the context bit set.
        var isAlt = vk == VkMenu;
        var lParam = keyDown ? MakeKeyDownLParam(vk) : MakeKeyUpLParam(vk);
        if (isAlt)
            lParam = (IntPtr)((long)lParam | (1L << 29));
        var msg = keyDown
            ? (isAlt ? WmSyskeydown : WmKeydown)
            : (isAlt ? WmSyskeyup : WmKeyup);
        _ = PostMessage(hwnd, msg, vk, lParam);
    }

    private static void SendScancode(ushort vk, bool keyUp)
    {
        var scan = (ushort)MapVirtualKey(vk, MapvkVkToVsc);
        var input = new INPUT
        {
            type = InputKeyboard,
            U = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = 0,
                    wScan = scan,
                    dwFlags = KeyeventfScancode | (keyUp ? KeyeventfKeyup : 0)
                }
            }
        };

        if (SendInput(1, [input], Marshal.SizeOf<INPUT>()) == 0)
            throw new InvalidOperationException($"SendInput failed: {Marshal.GetLastWin32Error()}");
    }

    private static bool TryParseCombo(string combo, out ushort keyVk, out List<ushort> modifierVks)
    {
        keyVk = 0;
        modifierVks = [];
        ushort? main = null;
        var hasShift = false;
        var hasCtrl = false;
        var hasAlt = false;

        foreach (var part in combo.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (part.Equals("Shift", StringComparison.OrdinalIgnoreCase))
                hasShift = true;
            else if (part.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) || part.Equals("Control", StringComparison.OrdinalIgnoreCase))
                hasCtrl = true;
            else if (part.Equals("Alt", StringComparison.OrdinalIgnoreCase))
                hasAlt = true;
            else if (TryCharToVk(part, out var vk))
                main = vk;
        }

        if (main is null)
            return false;

        keyVk = main.Value;
        // Ctrl → Alt → Shift down-order (BMP FFXIVHook order); released in reverse.
        if (hasCtrl)
            modifierVks.Add((ushort)VkControl);
        if (hasAlt)
            modifierVks.Add((ushort)VkMenu);
        if (hasShift)
            modifierVks.Add((ushort)VkShift);
        return true;
    }

    private static bool TryCharToVk(string token, out ushort vk)
    {
        vk = token.ToUpperInvariant() switch
        {
            "A" => 0x41, "B" => 0x42, "C" => 0x43, "D" => 0x44, "E" => 0x45,
            "F" => 0x46, "G" => 0x47, "H" => 0x48, "I" => 0x49, "J" => 0x4A,
            "K" => 0x4B, "L" => 0x4C, "M" => 0x4D, "N" => 0x4E, "O" => 0x4F,
            "P" => 0x50, "Q" => 0x51, "R" => 0x52, "S" => 0x53, "T" => 0x54,
            "U" => 0x55, "V" => 0x56, "W" => 0x57, "X" => 0x58, "Y" => 0x59,
            "Z" => 0x5A,
            "0" => 0x30, "1" => 0x31, "2" => 0x32, "3" => 0x33, "4" => 0x34,
            "5" => 0x35, "6" => 0x36, "7" => 0x37, "8" => 0x38, "9" => 0x39,
            "NUMPAD0" => 0x60, "NUMPAD1" => 0x61, "NUMPAD2" => 0x62, "NUMPAD3" => 0x63,
            "NUMPAD4" => 0x64, "NUMPAD5" => 0x65, "NUMPAD6" => 0x66, "NUMPAD7" => 0x67,
            "NUMPAD8" => 0x68, "NUMPAD9" => 0x69,
            "SPACE" => 0x20, "TAB" => 0x09,
            _ => 0
        };
        if (vk != 0)
            return true;

        // Exact virtual-key form ("VK186" = VK_OEM_1) — used for OEM keys synced from
        // FFXIV's KEYBIND.DAT, immune to keyboard-layout differences.
        if (token.Length > 2
            && token.StartsWith("VK", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(token[2..], out var code)
            && code is > 0 and < 0xFF)
        {
            vk = (ushort)code;
            return true;
        }

        if (token.Length == 1)
        {
            var scan = VkKeyScan(token[0]);
            if (scan != -1)
            {
                vk = (ushort)(scan & 0xFF);
                return true;
            }
        }

        return false;
    }

    private static IntPtr MakeKeyDownLParam(uint vk)
    {
        var scan = MapVirtualKey(vk, MapvkVkToVsc);
        return (IntPtr)(1u | ((scan & 0xFF) << 16));
    }

    private static IntPtr MakeKeyUpLParam(uint vk)
    {
        var scan = MapVirtualKey(vk, MapvkVkToVsc);
        return (IntPtr)(1u | ((scan & 0xFF) << 16) | (1u << 30) | (1u << 31));
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern short VkKeyScan(char ch);

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKey(uint uCode, uint uMapType);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostMessage(IntPtr hWnd, uint msg, uint wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public int type;
        public InputUnion U;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }
}
