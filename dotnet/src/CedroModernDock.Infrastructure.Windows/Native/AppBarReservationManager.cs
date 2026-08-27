using System.Runtime.InteropServices;

namespace CedroModernDock.Infrastructure.Windows.Native;

/// <summary>
/// Very small, deliberately conservative AppBar wrapper used only by Cedro's
/// dedicated BOTTOM_APPBAR positioning mode.
///
/// Safety rules:
/// - ABM_NEW is sent at most once per mode activation.
/// - The legal bottom boundary is negotiated once, before Cedro changes rcWork.
/// - Height updates reuse that fixed boundary and never query the already-shrunk
///   work area again, preventing recursive/nested reservations.
/// - No Shell callback/subclass loop and no periodic SHAppBarMessage polling.
/// - ABM_REMOVE is sent exactly once when leaving the mode or exiting Cedro.
/// </summary>
public sealed class AppBarReservationManager : IDisposable
{
    private const uint ABM_NEW = 0x00000000;
    private const uint ABM_REMOVE = 0x00000001;
    private const uint ABM_QUERYPOS = 0x00000002;
    private const uint ABM_SETPOS = 0x00000003;
    private const uint ABE_BOTTOM = 3;

    private readonly IntPtr _hwnd;
    private bool _registered;
    private uint _callbackMessage;
    private RECT _monitor;
    private RECT _baselineWork;
    private RECT _reservation;
    private int _fixedBottom;
    private int _thickness;

    public bool IsRegistered => _registered;
    public RECT MonitorRect => _monitor;
    public RECT BaselineWorkRect => _baselineWork;
    public RECT ReservationRect => _reservation;
    public int Thickness => _thickness;

    public AppBarReservationManager(IntPtr hwnd)
    {
        _hwnd = hwnd;
    }

    /// <summary>
    /// Registers one bottom AppBar and negotiates its legal boundary against the
    /// Windows taskbar/other AppBars. Call only when entering BOTTOM_APPBAR mode.
    /// </summary>
    public bool RegisterBottom(RECT monitor, RECT baselineWork, int thickness)
    {
        if (_hwnd == IntPtr.Zero || thickness <= 0)
            return false;

        if (_registered)
            return UpdateHeight(thickness);

        _callbackMessage = User32.RegisterWindowMessage(
            $"CedroModernDock.BottomAppBar.{Environment.ProcessId}.{_hwnd.ToInt64():X}");

        var newData = CreateData();
        UIntPtr newResult = Shell32AppBar.SHAppBarMessage(ABM_NEW, ref newData);
        if (newResult == UIntPtr.Zero)
            return false;

        _registered = true;
        _monitor = monitor;
        _baselineWork = baselineWork;

        // Negotiate exactly once while baselineWork still represents the Windows
        // taskbar/other AppBars but not Cedro itself.
        var query = CreateData();
        query.uEdge = ABE_BOTTOM;
        query.rc = monitor;
        Shell32AppBar.SHAppBarMessage(ABM_QUERYPOS, ref query);

        int baselineBottom = Math.Clamp(baselineWork.Bottom, monitor.Top + 1, monitor.Bottom);
        int queriedBottom = Math.Clamp(query.rc.Bottom, monitor.Top + 1, monitor.Bottom);
        _fixedBottom = Math.Min(baselineBottom, queriedBottom);

        if (_fixedBottom <= monitor.Top)
        {
            Remove();
            return false;
        }

        return ApplyHeight(thickness);
    }

    /// <summary>
    /// Changes only the height of the already-registered AppBar. Crucially this
    /// does NOT call ABM_QUERYPOS and therefore cannot stack Cedro on top of its
    /// own previous reservation.
    /// </summary>
    public bool UpdateHeight(int thickness)
    {
        if (!_registered)
            return false;

        int maxHeight = Math.Max(1, _fixedBottom - _monitor.Top);
        thickness = Math.Clamp(thickness, 1, maxHeight);
        if (thickness == _thickness)
            return true;

        return ApplyHeight(thickness);
    }

    private bool ApplyHeight(int thickness)
    {
        if (!_registered)
            return false;

        int maxHeight = Math.Max(1, _fixedBottom - _monitor.Top);
        thickness = Math.Clamp(thickness, 1, maxHeight);

        var data = CreateData();
        data.uEdge = ABE_BOTTOM;
        data.rc = new RECT
        {
            Left = _monitor.Left,
            Top = _fixedBottom - thickness,
            Right = _monitor.Right,
            Bottom = _fixedBottom
        };

        Shell32AppBar.SHAppBarMessage(ABM_SETPOS, ref data);

        // Keep our fixed lower boundary even if Shell rewrites unrelated fields.
        // This is the invariant that prevents recursive inward drift.
        _thickness = thickness;
        _reservation = new RECT
        {
            Left = _monitor.Left,
            Top = _fixedBottom - thickness,
            Right = _monitor.Right,
            Bottom = _fixedBottom
        };
        return true;
    }

    public void Remove()
    {
        if (!_registered)
            return;

        var data = CreateData();
        Shell32AppBar.SHAppBarMessage(ABM_REMOVE, ref data);
        _registered = false;
        _reservation = default;
        _thickness = 0;
        _fixedBottom = 0;
    }

    private APPBARDATA CreateData() => new()
    {
        cbSize = (uint)Marshal.SizeOf<APPBARDATA>(),
        hWnd = _hwnd,
        uCallbackMessage = _callbackMessage
    };

    public void Dispose() => Remove();
}

[StructLayout(LayoutKind.Sequential)]
internal struct APPBARDATA
{
    public uint cbSize;
    public IntPtr hWnd;
    public uint uCallbackMessage;
    public uint uEdge;
    public RECT rc;
    public IntPtr lParam;
}

internal static class Shell32AppBar
{
    [DllImport("shell32.dll", SetLastError = true)]
    internal static extern UIntPtr SHAppBarMessage(uint dwMessage, ref APPBARDATA pData);
}
