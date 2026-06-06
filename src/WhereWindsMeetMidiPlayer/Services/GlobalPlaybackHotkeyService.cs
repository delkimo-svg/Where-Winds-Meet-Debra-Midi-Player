using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using WhereWindsMeetMidiPlayer.Infrastructure;
using WhereWindsMeetMidiPlayer.Models;

namespace WhereWindsMeetMidiPlayer.Services;

/// <summary>
/// Global playback hotkeys — active while the game or Debra window is focused.
/// </summary>
public sealed class GlobalPlaybackHotkeyService : IDisposable
{
    private const int WhKeyboardLl = 13;
    private const int WmKeyDown = 0x0100;

    private uint _vkPlayPause = PlaybackHotkeyDefaults.PlayPause;
    private uint _vkStop = PlaybackHotkeyDefaults.Stop;
    private uint _vkPrevious = PlaybackHotkeyDefaults.Previous;
    private uint _vkNext = PlaybackHotkeyDefaults.Next;

    private readonly GameWindowService _gameWindow;
    private readonly Func<bool> _isHotkeyContextActive;
    private readonly Func<bool> _hasActivePlayback;
    private readonly Action _onTogglePlayPause;
    private readonly Action _onStop;
    private readonly Action _onPrevious;
    private readonly Action _onNext;
    private readonly Dispatcher _dispatcher;

    private IntPtr _hookId = IntPtr.Zero;
    private LowLevelKeyboardProc? _proc;

    public GlobalPlaybackHotkeyService(
        GameWindowService gameWindow,
        Func<bool> isHotkeyContextActive,
        Func<bool> hasActivePlayback,
        Action onTogglePlayPause,
        Action onStop,
        Action onPrevious,
        Action onNext,
        Dispatcher dispatcher)
    {
        _gameWindow = gameWindow;
        _isHotkeyContextActive = isHotkeyContextActive;
        _hasActivePlayback = hasActivePlayback;
        _onTogglePlayPause = onTogglePlayPause;
        _onStop = onStop;
        _onPrevious = onPrevious;
        _onNext = onNext;
        _dispatcher = dispatcher;
    }

    public void SetVirtualKeys(int playPause, int stop, int previous, int next)
    {
        _vkPlayPause = (uint)playPause;
        _vkStop = (uint)stop;
        _vkPrevious = (uint)previous;
        _vkNext = (uint)next;
    }

    public void Start()
    {
        if (_hookId != IntPtr.Zero)
            return;

        _proc = HookCallback;
        var moduleHandle = ResolveModuleHandle();
        _hookId = SetWindowsHookEx(WhKeyboardLl, _proc, moduleHandle, 0);
        if (_hookId == IntPtr.Zero)
            AppPaths.WriteDiagnosticLog("global-hotkey", new InvalidOperationException(
                $"SetWindowsHookEx failed (error {Marshal.GetLastWin32Error()}). Playback hotkeys disabled."));
    }

    /// <summary>MainModule throws on single-file publish; fall back to the process module handle.</summary>
    private static IntPtr ResolveModuleHandle()
    {
        try
        {
            var exeName = Path.GetFileName(Environment.ProcessPath);
            if (!string.IsNullOrEmpty(exeName))
            {
                var fromPath = GetModuleHandle(exeName);
                if (fromPath != IntPtr.Zero)
                    return fromPath;
            }
        }
        catch
        {
            // ignored
        }

        try
        {
            using var process = Process.GetCurrentProcess();
            using var mod = process.MainModule;
            if (mod?.ModuleName is { } name)
            {
                var fromModule = GetModuleHandle(name);
                if (fromModule != IntPtr.Zero)
                    return fromModule;
            }
        }
        catch
        {
            // ignored — common for single-file bundles
        }

        return GetModuleHandle(null);
    }

    public void Dispose()
    {
        if (_hookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode < 0 || wParam != (IntPtr)WmKeyDown || !_isHotkeyContextActive())
            return CallNextHookEx(_hookId, nCode, wParam, lParam);

        var hook = Marshal.PtrToStructure<KbdLlHookStruct>(lParam);
        var vk = hook.vkCode;
        if (vk == _vkPlayPause)
        {
            _dispatcher.BeginInvoke(_onTogglePlayPause);
            return (IntPtr)1;
        }

        if (vk == _vkStop && _hasActivePlayback())
        {
            _dispatcher.BeginInvoke(_onStop);
            return (IntPtr)1;
        }

        if (vk == _vkPrevious)
        {
            _dispatcher.BeginInvoke(_onPrevious);
            return (IntPtr)1;
        }

        if (vk == _vkNext)
        {
            _dispatcher.BeginInvoke(_onNext);
            return (IntPtr)1;
        }

        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KbdLlHookStruct
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);
}
