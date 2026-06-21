using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace WhereWindsMeetMidiPlayer.Infrastructure;

/// <summary>
/// Keeps borderless (WindowStyle=None) windows inside the monitor work area when maximized,
/// so the Windows taskbar does not cover the bottom chrome.
/// </summary>
internal static class BorderlessWindowMaximizeHelper
{
    private const int WM_GETMINMAXINFO = 0x0024;
    private const uint MonitorDefaultToNearest = 0x00000002;

    [StructLayout(LayoutKind.Sequential)]
    private struct PointInterop
    {
        public int x;
        public int y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MinMaxInfo
    {
        public PointInterop ptReserved;
        public PointInterop ptMaxSize;
        public PointInterop ptMaxPosition;
        public PointInterop ptMinTrackSize;
        public PointInterop ptMaxTrackSize;
    }

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
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfo lpmi);

    public static void Attach(Window window)
    {
        window.SourceInitialized += OnSourceInitialized;
        window.StateChanged += OnStateChanged;
    }

    private static void OnSourceInitialized(object? sender, EventArgs e)
    {
        if (sender is not Window window)
            return;

        window.SourceInitialized -= OnSourceInitialized;
        var source = PresentationSource.FromVisual(window) as HwndSource;
        source?.AddHook(WndProc);
    }

    private static void OnStateChanged(object? sender, EventArgs e)
    {
        if (sender is not Window window || window.WindowState != WindowState.Maximized)
            return;

        // Backup for DPI / taskbar changes: snap to WPF work area in device-independent units.
        var area = SystemParameters.WorkArea;
        window.MaxWidth = area.Width;
        window.MaxHeight = area.Height;
    }

    private static IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_GETMINMAXINFO)
        {
            ApplyMonitorWorkArea(hwnd, lParam);
            handled = true;
        }

        return IntPtr.Zero;
    }

    private static void ApplyMonitorWorkArea(IntPtr hwnd, IntPtr lParam)
    {
        var mmi = Marshal.PtrToStructure<MinMaxInfo>(lParam);
        var monitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
        if (monitor == IntPtr.Zero)
            return;

        var monitorInfo = new MonitorInfo { cbSize = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(monitor, ref monitorInfo))
            return;

        var work = monitorInfo.rcWork;
        var monitorRect = monitorInfo.rcMonitor;

        mmi.ptMaxPosition.x = work.Left - monitorRect.Left;
        mmi.ptMaxPosition.y = work.Top - monitorRect.Top;
        mmi.ptMaxSize.x = work.Right - work.Left;
        mmi.ptMaxSize.y = work.Bottom - work.Top;

        Marshal.StructureToPtr(mmi, lParam, true);
    }
}
