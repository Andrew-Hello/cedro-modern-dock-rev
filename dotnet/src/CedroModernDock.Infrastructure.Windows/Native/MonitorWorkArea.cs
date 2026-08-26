namespace CedroModernDock.Infrastructure.Windows.Native;

using System;
using System.Runtime.InteropServices;

/// <summary>
/// Returns both the physical monitor rectangle and its usable work area.
/// The work-area bottom is the taskbar's top edge when a normal bottom taskbar
/// reserves screen space; otherwise it naturally matches the physical bottom.
/// </summary>
public static class MonitorWorkArea
{
    private const uint MONITOR_DEFAULTTONEAREST = 0x00000002;

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public uint cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    public static bool TryGet(IntPtr hwnd, out RECT monitor, out RECT work)
    {
        monitor = default;
        work = default;
        if (hwnd == IntPtr.Zero) return false;

        IntPtr hMonitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
        if (hMonitor == IntPtr.Zero) return false;

        var info = new MONITORINFO { cbSize = (uint)Marshal.SizeOf<MONITORINFO>() };
        if (!GetMonitorInfo(hMonitor, ref info)) return false;

        monitor = info.rcMonitor;
        work = info.rcWork;
        return true;
    }

    /// <summary>
    /// Bottom docking is the only edge that follows the work area. This avoids
    /// covering a conventional bottom taskbar while leaving top/left/right
    /// snapping tied to the physical monitor edges exactly as before.
    /// </summary>
    public static int GetEffectiveBottom(RECT monitor, RECT work)
    {
        return work.Bottom < monitor.Bottom ? work.Bottom : monitor.Bottom;
    }
}
