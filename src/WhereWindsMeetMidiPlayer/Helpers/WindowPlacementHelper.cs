using System.Runtime.InteropServices;
using System.Windows;
using WhereWindsMeetMidiPlayer.Services;

namespace WhereWindsMeetMidiPlayer.Helpers;

/// <summary>Places the app window centered over the launch-time foreground window (usually the game).</summary>
internal static class WindowPlacementHelper
{
    private const uint MonitorDefaultToNearest = 2;
    private static IntPtr _launchAnchorHwnd;

    [StructLayout(LayoutKind.Sequential)]
    private struct RectInterop
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MonitorInfo
    {
        public int cbSize;
        public RectInterop rcMonitor;
        public RectInterop rcWork;
        public int dwFlags;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RectInterop rect);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfo lpmi);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int x;
        public int y;
    }

    public static void RememberLaunchForegroundWindow() =>
        _launchAnchorHwnd = GetForegroundWindow();

    public static void CenterOnLaunchAnchor(Window window, GameWindowService? gameWindow = null)
    {
        var anchor = ResolveAnchorHwnd(gameWindow);
        if (anchor == IntPtr.Zero)
            CenterOnPrimaryWorkArea(window);
        else
            CenterOnWindow(window, anchor);
    }

    private static IntPtr ResolveAnchorHwnd(GameWindowService? gameWindow)
    {
        if (IsUsableExternalWindow(_launchAnchorHwnd))
            return _launchAnchorHwnd;

        if (gameWindow?.TryGetPrimaryWindow(out var gameHwnd) == true && IsUsableExternalWindow(gameHwnd))
            return gameHwnd;

        return IntPtr.Zero;
    }

    private static bool IsUsableExternalWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || !IsWindow(hwnd) || !IsWindowVisible(hwnd))
            return false;

        GetWindowThreadProcessId(hwnd, out var pid);
        return pid != 0 && pid != (uint)Environment.ProcessId;
    }

    private static void CenterOnWindow(Window window, IntPtr hwnd)
    {
        if (!GetWindowRect(hwnd, out var rect))
        {
            CenterOnPrimaryWorkArea(window);
            return;
        }

        var widthPx = rect.Right - rect.Left;
        var heightPx = rect.Bottom - rect.Top;
        if (widthPx < 200 || heightPx < 200)
        {
            CenterOnMonitorWorkArea(window, MonitorFromWindow(hwnd, MonitorDefaultToNearest));
            return;
        }

        var dpi = GetDpiForWindow(hwnd);
        if (dpi == 0)
            dpi = 96;

        var scale = dpi / 96.0;
        var left = rect.Left / scale;
        var top = rect.Top / scale;
        var frameWidth = widthPx / scale;
        var frameHeight = heightPx / scale;

        window.Left = left + (frameWidth - window.Width) / 2.0;
        window.Top = top + (frameHeight - window.Height) / 2.0;

        ClampToMonitorWorkArea(window, MonitorFromWindow(hwnd, MonitorDefaultToNearest), scale);
    }

    private static void CenterOnPrimaryWorkArea(Window window)
    {
        var point = new POINT { x = 0, y = 0 };
        var monitor = MonitorFromPoint(point, MonitorDefaultToNearest);
        CenterOnMonitorWorkArea(window, monitor);
    }

    private static void CenterOnMonitorWorkArea(Window window, IntPtr monitor)
    {
        if (monitor == IntPtr.Zero)
            return;

        var monitorInfo = new MonitorInfo { cbSize = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(monitor, ref monitorInfo))
            return;

        var dpi = 96u;
        var scale = dpi / 96.0;
        var work = monitorInfo.rcWork;
        var workWidth = (work.Right - work.Left) / scale;
        var workHeight = (work.Bottom - work.Top) / scale;
        var workLeft = work.Left / scale;
        var workTop = work.Top / scale;

        window.Left = workLeft + (workWidth - window.Width) / 2.0;
        window.Top = workTop + (workHeight - window.Height) / 2.0;

        ClampToWorkRect(window, workLeft, workTop, workWidth, workHeight);
    }

    private static void ClampToMonitorWorkArea(Window window, IntPtr monitor, double scale)
    {
        if (monitor == IntPtr.Zero)
            return;

        var monitorInfo = new MonitorInfo { cbSize = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(monitor, ref monitorInfo))
            return;

        var work = monitorInfo.rcWork;
        ClampToWorkRect(
            window,
            work.Left / scale,
            work.Top / scale,
            (work.Right - work.Left) / scale,
            (work.Bottom - work.Top) / scale);
    }

    private static void ClampToWorkRect(Window window, double workLeft, double workTop, double workWidth, double workHeight)
    {
        if (window.Width > workWidth)
            window.Width = Math.Max(window.MinWidth, workWidth);

        if (window.Height > workHeight)
            window.Height = Math.Max(window.MinHeight, workHeight);

        var maxLeft = workLeft + workWidth - window.Width;
        var maxTop = workTop + workHeight - window.Height;
        window.Left = Math.Clamp(window.Left, workLeft, Math.Max(workLeft, maxLeft));
        window.Top = Math.Clamp(window.Top, workTop, Math.Max(workTop, maxTop));
    }
}
