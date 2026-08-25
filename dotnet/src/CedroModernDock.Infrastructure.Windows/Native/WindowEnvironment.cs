namespace CedroModernDock.Infrastructure.Windows.Native;

using System;
using System.Runtime.InteropServices;
using System.Text;

/// <summary>
/// Small Win32 environment helper used by dock window policies: foreground
/// fullscreen detection and monitor/window geometry queries.
/// </summary>
public static class WindowEnvironment
{
    private const uint MONITOR_DEFAULTTONEAREST = 0x00000002;
    private const int FullscreenTolerance = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public uint cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    public static bool TryGetWindowRect(IntPtr hwnd, out RECT rect)
    {
        rect = default;
        return hwnd != IntPtr.Zero && GetWindowRect(hwnd, out rect);
    }

    public static bool TryGetMonitorRect(IntPtr hwnd, out RECT rect)
    {
        rect = default;
        if (hwnd == IntPtr.Zero) return false;

        IntPtr monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
        if (monitor == IntPtr.Zero) return false;

        var info = new MONITORINFO { cbSize = (uint)Marshal.SizeOf<MONITORINFO>() };
        if (!GetMonitorInfo(monitor, ref info)) return false;

        rect = info.rcMonitor;
        return true;
    }

    /// <summary>
    /// Returns true when the foreground top-level window covers its monitor.
    /// This catches exclusive/borderless games as well as other true fullscreen
    /// applications. Shell desktop/taskbar surfaces are explicitly ignored.
    /// </summary>
    public static bool IsForegroundFullscreen(IntPtr excludedHwnd)
    {
        IntPtr foreground = GetForegroundWindow();
        if (foreground == IntPtr.Zero || foreground == excludedHwnd)
            return false;
        if (!User32.IsWindowVisible(foreground) || IsIconic(foreground))
            return false;

        string className = GetClassNameSafe(foreground);
        if (className is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd")
            return false;

        if (!GetWindowRect(foreground, out RECT windowRect))
            return false;
        if (!TryGetMonitorRect(foreground, out RECT monitorRect))
            return false;

        int windowWidth = windowRect.Right - windowRect.Left;
        int windowHeight = windowRect.Bottom - windowRect.Top;
        int monitorWidth = monitorRect.Right - monitorRect.Left;
        int monitorHeight = monitorRect.Bottom - monitorRect.Top;

        return windowRect.Left <= monitorRect.Left + FullscreenTolerance &&
               windowRect.Top <= monitorRect.Top + FullscreenTolerance &&
               windowRect.Right >= monitorRect.Right - FullscreenTolerance &&
               windowRect.Bottom >= monitorRect.Bottom - FullscreenTolerance &&
               windowWidth >= monitorWidth - (FullscreenTolerance * 2) &&
               windowHeight >= monitorHeight - (FullscreenTolerance * 2);
    }

    private static string GetClassNameSafe(IntPtr hwnd)
    {
        var buffer = new StringBuilder(128);
        return User32.GetClassName(hwnd, buffer, buffer.Capacity) > 0
            ? buffer.ToString()
            : string.Empty;
    }
}
