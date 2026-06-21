using System.Runtime.InteropServices;
using WhereWindsMeetMidiPlayer.Models;

namespace WhereWindsMeetMidiPlayer.Services;

public sealed class InputService
{
    private const int InputKeyboard = 1;
    private const uint KeyeventfKeyup = 0x0002;
    private const uint KeyeventfScancode = 0x0008;
    private const uint WmKeydown = 0x0100;
    private const uint WmKeyup = 0x0101;
    private const uint VkShift = 0x10;
    private const uint VkControl = 0x11;
    private const uint MapvkVkToVsc = 0;

    private readonly GameWindowService _gameWindow;
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
        if (string.IsNullOrWhiteSpace(combo) || !TryParseCombo(combo, out var keyVk, out var modifierVk))
            return;

        var sent = _getMode() switch
        {
            InputDeliveryMode.LocalPostMessage => TryPostMessage(keyVk, modifierVk),
            _ => TryPostMessage(keyVk, modifierVk) || TrySendInputAttached(keyVk, modifierVk)
        };

        if (sent)
        {
            KeysSentCount++;
            LastKeySent = combo;
        }
    }

    public void KeyDown(string combo) => PressKeyCombo(combo);
    public void KeyUp(string combo) { }

    private bool TryPostMessage(ushort keyVk, ushort? modifierVk)
    {
        var targets = _gameWindow.GetMessageTargets();
        if (targets.Count == 0)
            return false;

        foreach (var hwnd in targets)
        {
            if (modifierVk is { } modVk)
            {
                PostKey(hwnd, modVk, true);
                PostKey(hwnd, keyVk, true);
                Thread.Sleep(ModifierDelayMs > 0 ? ModifierDelayMs : 1);
                PostKey(hwnd, keyVk, false);
                PostKey(hwnd, modVk, false);
            }
            else
            {
                PostKey(hwnd, keyVk, true);
                Thread.Sleep(ModifierDelayMs > 0 ? ModifierDelayMs : 1);
                PostKey(hwnd, keyVk, false);
            }
        }

        LastDeliveryMethod = $"PostMessage→{_gameWindow.TargetProcessName} ({targets.Count} hwnd)";
        return true;
    }

    private bool TrySendInputAttached(ushort keyVk, ushort? modifierVk)
    {
        try
        {
            _gameWindow.WithAttachedInput(() =>
            {
                if (modifierVk is { } modVk)
                {
                    SendScancode(modVk, false);
                    SendScancode(keyVk, false);
                    Thread.Sleep(ModifierDelayMs > 0 ? ModifierDelayMs : 2);
                    SendScancode(keyVk, true);
                    SendScancode(modVk, true);
                }
                else
                {
                    SendScancode(keyVk, false);
                    Thread.Sleep(ModifierDelayMs > 0 ? ModifierDelayMs : 2);
                    SendScancode(keyVk, true);
                }
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
        var lParam = keyDown ? MakeKeyDownLParam(vk) : MakeKeyUpLParam(vk);
        var msg = keyDown ? WmKeydown : WmKeyup;
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

    private static bool TryParseCombo(string combo, out ushort keyVk, out ushort? modifierVk)
    {
        keyVk = 0;
        modifierVk = null;
        ushort? main = null;
        ushort? mod = null;

        foreach (var part in combo.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (part.Equals("Shift", StringComparison.OrdinalIgnoreCase))
                mod = (ushort)VkShift;
            else if (part.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) || part.Equals("Control", StringComparison.OrdinalIgnoreCase))
                mod = (ushort)VkControl;
            else if (TryCharToVk(part, out var vk))
                main = vk;
        }

        if (main is null)
            return false;

        keyVk = main.Value;
        modifierVk = mod;
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
            _ => 0
        };
        if (vk != 0)
            return true;

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
