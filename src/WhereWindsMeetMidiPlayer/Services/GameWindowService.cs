using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace WhereWindsMeetMidiPlayer.Services;

/// <summary>
/// Finds Where Winds Meet windows by process name (e.g. wwm.exe) and/or title keywords.
/// Sends keys via PostMessage to those HWNDs (background play, like other WWM MIDI tools).
/// </summary>
public sealed class GameWindowService
{
    private const int AsfwAny = -1;
    private const int SwRestore = 9;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(3);

    private static readonly string[] BuiltInKeywords =
    [
        "where winds meet", "where winds", "winds meet", "wwm", "燕云", "燕云十六声",
        "연운", "guqin", "古琴", "geforce now", "geforcenow", "nvidia geforce", "netease",
        "final fantasy xiv", "ffxiv"
    ];

    private static readonly string[] ExcludedTitleFragments =
    [
        "midi player", "debra", "overlay", "discord", "notepad", "visual studio", "vscode",
        "google chrome", "mozilla firefox", "microsoft edge", "youtube", "twitch"
    ];

    private IntPtr _cachedHwnd;
    private uint _cachedProcessId;
    private DateTime _cacheExpiry = DateTime.MinValue;
    private List<string> _customKeywords = [];
    private string _targetProcessName = "wwm.exe";

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumChildWindows(IntPtr hwndParent, EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool AllowSetForegroundWindow(int dwProcessId);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
        public long Area => Math.Max(0, Right - Left) * Math.Max(0, Bottom - Top);
    }

    public void SetTargetProcessName(string? processName)
    {
        _targetProcessName = string.IsNullOrWhiteSpace(processName) ? "wwm.exe" : processName.Trim();
        ClearCache();
    }

    public string TargetProcessName => _targetProcessName;

    public void SetCustomKeywords(IEnumerable<string> keywords) =>
        _customKeywords = keywords.Where(k => !string.IsNullOrWhiteSpace(k)).Select(k => k.Trim().ToLowerInvariant()).ToList();

    public void ClearCache()
    {
        _cachedHwnd = IntPtr.Zero;
        _cachedProcessId = 0;
        _cacheExpiry = DateTime.MinValue;
    }

    public bool IsProcessRunning()
    {
        var name = NormalizeProcessName(_targetProcessName);
        return Process.GetProcessesByName(name).Length > 0;
    }

    public bool IsGameWindowFound() => TryGetPrimaryWindow(out _);

    public bool TryGetPrimaryWindow(out IntPtr hwnd)
    {
        if (_cachedHwnd != IntPtr.Zero && DateTime.UtcNow < _cacheExpiry)
        {
            hwnd = _cachedHwnd;
            return true;
        }

        var targets = DiscoverTargets();
        if (targets.Count == 0)
        {
            hwnd = IntPtr.Zero;
            return false;
        }

        var best = targets.OrderByDescending(t => t.Area).First();
        _cachedHwnd = best.Handle;
        _cachedProcessId = best.ProcessId;
        _cacheExpiry = DateTime.UtcNow.Add(CacheDuration);
        hwnd = best.Handle;
        return true;
    }

    /// <summary>HWNDs to receive PostMessage (primary + large children).</summary>
    public IReadOnlyList<IntPtr> GetMessageTargets()
    {
        if (!TryGetPrimaryWindow(out var primary))
            return [];

        var list = new List<IntPtr> { primary };
        CollectChildTargets(primary, list, maxDepth: 2);
        return list.Distinct().Where(h => h != IntPtr.Zero).ToList();
    }

    public IReadOnlyList<GameWindowInfo> FindWindows(string? titleContains = null)
    {
        var targets = DiscoverTargets();
        if (!string.IsNullOrWhiteSpace(titleContains))
        {
            var needle = titleContains.Trim();
            targets = targets.Where(t => t.Title.Contains(needle, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        return targets
            .OrderByDescending(t => t.Area)
            .Select(t => new GameWindowInfo(t.Handle, $"{t.Title} [{t.ClassName}] pid={t.ProcessId}"))
            .ToList();
    }

    public bool TryFocusGame(string? titleContains, out string message)
    {
        ClearCache();
        if (!TryGetPrimaryWindow(out var hwnd))
        {
            var proc = NormalizeProcessName(_targetProcessName);
            message = $"Process \"{proc}\" not running or no visible window. Start the game and open the instrument.";
            return false;
        }

        ShowWindow(hwnd, SwRestore);
        AllowSetForegroundWindow(AsfwAny);
        if (!SetForegroundWindow(hwnd))
        {
            message = "Game found but could not focus. Background PostMessage should still work.";
            return false;
        }

        message = $"Focused game window (pid {_cachedProcessId}).";
        return true;
    }

    public bool IsGameFocused()
    {
        var fg = GetForegroundWindow();
        if (fg == IntPtr.Zero)
            return false;

        GetWindowThreadProcessId(fg, out var pid);
        if (pid == 0 || pid == (uint)Environment.ProcessId)
            return false;

        _ = TryGetPrimaryWindow(out _);

        if (_cachedProcessId != 0 && pid == _cachedProcessId)
            return true;

        if (ResolveTargetProcessIds().Contains(pid))
            return true;

        return MatchesTitleWindow(fg);
    }

    /// <summary>Route SendInput through the game's input thread (helps when PostMessage is ignored).</summary>
    public void WithAttachedInput(Action action)
    {
        if (!TryGetPrimaryWindow(out var hwnd))
        {
            action();
            return;
        }

        var gameThread = GetWindowThreadProcessId(hwnd, out _);
        var ourThread = GetCurrentThreadId();
        var attached = AttachThreadInput(ourThread, gameThread, true);
        try
        {
            action();
        }
        finally
        {
            if (attached)
                AttachThreadInput(ourThread, gameThread, false);
        }
    }

    private List<WindowTarget> DiscoverTargets()
    {
        var results = new List<WindowTarget>();
        var processIds = ResolveTargetProcessIds();

        foreach (var pid in processIds)
            EnumProcessWindows(pid, results);

        if (results.Count == 0)
            EnumTitleMatchedWindows(results);

        return results.Where(r => r.Area > 10000).ToList();
    }

    private HashSet<uint> ResolveTargetProcessIds()
    {
        var ids = new HashSet<uint>();
        var name = NormalizeProcessName(_targetProcessName);
        if (name.Length > 0)
        {
            foreach (var p in Process.GetProcessesByName(name))
            {
                try { ids.Add((uint)p.Id); }
                finally { p.Dispose(); }
            }
        }

        return ids;
    }

    private void EnumProcessWindows(uint processId, List<WindowTarget> results)
    {
        EnumWindows((hWnd, _) =>
        {
            GetWindowThreadProcessId(hWnd, out var pid);
            if (pid != processId || !IsWindowVisible(hWnd))
                return true;

            // No title-based exclusion here: the process id match is authoritative.
            // (FFXIV titles its window with the character name — a character called
            // "Debra" must not knock the game window out of discovery.)
            GetWindowRect(hWnd, out var rect);
            results.Add(new WindowTarget(hWnd, pid, GetTitle(hWnd), GetClass(hWnd), rect.Area));
            return true;
        }, IntPtr.Zero);
    }

    private void EnumTitleMatchedWindows(List<WindowTarget> results)
    {
        EnumWindows((hWnd, _) =>
        {
            if (!IsWindowVisible(hWnd) || !MatchesTitleWindow(hWnd))
                return true;

            GetWindowThreadProcessId(hWnd, out var pid);
            if (pid == (uint)Environment.ProcessId)
                return true;

            GetWindowRect(hWnd, out var rect);
            results.Add(new WindowTarget(hWnd, pid, GetTitle(hWnd), GetClass(hWnd), rect.Area));
            return true;
        }, IntPtr.Zero);
    }

    private void CollectChildTargets(IntPtr parent, List<IntPtr> list, int maxDepth)
    {
        if (maxDepth <= 0)
            return;

        EnumChildWindows(parent, (hWnd, _) =>
        {
            if (!IsWindowVisible(hWnd))
                return true;

            GetWindowRect(hWnd, out var rect);
            if (rect.Area > 5000)
                list.Add(hWnd);

            CollectChildTargets(hWnd, list, maxDepth - 1);
            return true;
        }, IntPtr.Zero);
    }

    private bool MatchesTitleWindow(IntPtr hWnd) => !IsExcludedTitle(GetTitle(hWnd)) && MatchesTitle(GetTitle(hWnd));

    private bool MatchesTitle(string title)
    {
        var lower = title.ToLowerInvariant();
        foreach (var keyword in BuiltInKeywords)
        {
            if (lower.Contains(keyword, StringComparison.Ordinal))
                return true;
        }

        foreach (var keyword in _customKeywords)
        {
            if (lower.Contains(keyword, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static bool IsExcludedTitle(string title)
    {
        var lower = title.ToLowerInvariant();
        return ExcludedTitleFragments.Any(ex => lower.Contains(ex, StringComparison.Ordinal));
    }

    private static string NormalizeProcessName(string name)
    {
        name = name.Trim();
        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            name = name[..^4];
        return name;
    }

    private static string GetTitle(IntPtr hWnd)
    {
        var sb = new StringBuilder(512);
        _ = GetWindowText(hWnd, sb, sb.Capacity);
        return sb.ToString();
    }

    private static string GetClass(IntPtr hWnd)
    {
        var sb = new StringBuilder(256);
        _ = GetClassName(hWnd, sb, sb.Capacity);
        return sb.ToString();
    }

    private sealed record WindowTarget(IntPtr Handle, uint ProcessId, string Title, string ClassName, long Area);
}

public sealed record GameWindowInfo(IntPtr Handle, string Title);
