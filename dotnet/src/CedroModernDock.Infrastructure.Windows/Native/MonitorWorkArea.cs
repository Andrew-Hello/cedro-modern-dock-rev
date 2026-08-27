namespace CedroModernDock.Infrastructure.Windows.Native;

using System;
using System.Runtime.InteropServices;

/// <summary>
/// Returns both the physical monitor rectangle and its usable work area.
/// The work-area bottom is the taskbar's top edge when a normal bottom taskbar
/// reserves screen space; otherwise it naturally matches the physical bottom.
///
/// When Cedro itself is registered as an AppBar, Windows includes Cedro's own
/// reservation in rcWork. Before returning, this helper expands only Cedro's
/// strip back out so existing edge geometry still sees the taskbar/other AppBars
/// but never consumes Cedro's reservation twice.
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

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern bool DeleteObject(IntPtr hObject);

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
        AppBarReservationManager.ExpandWorkAreaForOwnReservation(hwnd, monitor, ref work);
        return true;
    }

    /// <summary>
    /// Bottom docking follows the taskbar-aware work area. With AppBar mode the
    /// returned work area has already had Cedro's own reservation removed from
    /// the calculation, so this remains the top edge of a conventional bottom
    /// taskbar rather than the top edge of Cedro itself.
    /// </summary>
    public static int GetEffectiveBottom(RECT monitor, RECT work)
        => work.Bottom < monitor.Bottom ? work.Bottom : monitor.Bottom;

    public static void DeleteRegionIfOwned(IntPtr region)
    {
        if (region != IntPtr.Zero)
            DeleteObject(region);
    }
}
