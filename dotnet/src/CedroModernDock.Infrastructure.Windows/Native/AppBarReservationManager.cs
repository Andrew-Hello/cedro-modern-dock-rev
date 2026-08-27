using System.Runtime.InteropServices;

namespace CedroModernDock.Infrastructure.Windows.Native;

/// <summary>Windows AppBar edges, matching the ABE_* constants.</summary>
public enum AppBarEdge : uint
{
    Left = 0,
    Top = 1,
    Right = 2,
    Bottom = 3
}

/// <summary>
/// Reserves a full monitor edge through the Windows Shell AppBar protocol while
/// allowing Cedro's actual HWND to remain a compact dock positioned anywhere
/// along that strip. The Shell decides the legal rectangle first, so an existing
/// Windows taskbar or another AppBar always wins and Cedro is placed beside it,
/// never on top of it.
/// </summary>
public sealed class AppBarReservationManager : IDisposable
{
    private const uint ABM_NEW = 0x00000000;
    private const uint ABM_REMOVE = 0x00000001;
    private const uint ABM_QUERYPOS = 0x00000002;
    private const uint ABM_SETPOS = 0x00000003;
    private const uint ABN_POSCHANGED = 0x00000001;

    private static readonly UIntPtr SubclassId = new(0x43454452); // "CEDR"
    private static readonly object SnapshotSync = new();
    private static readonly Dictionary<IntPtr, ReservationSnapshot> ActiveReservations = new();

    private readonly IntPtr _hwnd;
    private SubclassProc? _subclassProc;
    private bool _subclassed;
    private bool _registered;
    private uint _callbackMessage;
    private AppBarEdge _edge;
    private RECT _monitor;
    private RECT _safeWork;
    private int _thickness;
    private RECT _reservation;

    public event Action? ShellPositionChanged;

    public bool IsRegistered => _registered;
    public AppBarEdge Edge => _edge;
    public RECT ReservationRect => _reservation;

    public AppBarReservationManager(IntPtr hwnd)
    {
        _hwnd = hwnd;
    }

    /// <summary>
    /// Registers/updates the reservation. <paramref name="safeWork"/> is the
    /// monitor work area with Cedro's own previous reservation expanded back out
    /// but with the Windows taskbar and any other AppBars still excluded.
    /// </summary>
    public bool Update(
        AppBarEdge edge,
        RECT monitor,
        RECT safeWork,
        int thickness,
        bool force = false)
    {
        if (_hwnd == IntPtr.Zero || thickness <= 0)
            return false;

        int maxThickness = edge is AppBarEdge.Top or AppBarEdge.Bottom
            ? Math.Max(1, monitor.Bottom - monitor.Top)
            : Math.Max(1, monitor.Right - monitor.Left);
        thickness = Math.Clamp(thickness, 1, maxThickness);

        if (!EnsureRegistered())
            return false;

        if (!force && edge == _edge && thickness == _thickness &&
            RectEquals(monitor, _monitor) && RectEquals(safeWork, _safeWork))
            return true;

        var data = CreateData();
        data.uEdge = (uint)edge;
        data.rc = monitor;

        // Ask Explorer/Shell for a rectangle that does not collide with the
        // Windows taskbar or other registered AppBars.
        Shell32AppBar.SHAppBarMessage(ABM_QUERYPOS, ref data);

        // Clamp once more to the known work-area boundary. This is deliberately
        // defensive: even if a shell implementation returns an unexpected
        // rectangle, Cedro will never claim pixels occupied by the taskbar.
        switch (edge)
        {
            case AppBarEdge.Top:
            {
                int top = Math.Max(data.rc.Top, safeWork.Top);
                data.rc.Top = top;
                data.rc.Bottom = Math.Min(data.rc.Bottom, top + thickness);
                break;
            }
            case AppBarEdge.Bottom:
            {
                int bottom = Math.Min(data.rc.Bottom, safeWork.Bottom);
                data.rc.Bottom = bottom;
                data.rc.Top = Math.Max(data.rc.Top, bottom - thickness);
                break;
            }
            case AppBarEdge.Left:
            {
                int left = Math.Max(data.rc.Left, safeWork.Left);
                data.rc.Left = left;
                data.rc.Right = Math.Min(data.rc.Right, left + thickness);
                break;
            }
            case AppBarEdge.Right:
            {
                int right = Math.Min(data.rc.Right, safeWork.Right);
                data.rc.Right = right;
                data.rc.Left = Math.Max(data.rc.Left, right - thickness);
                break;
            }
        }

        if (data.rc.Right <= data.rc.Left || data.rc.Bottom <= data.rc.Top)
            return false;

        Shell32AppBar.SHAppBarMessage(ABM_SETPOS, ref data);

        _edge = edge;
        _monitor = monitor;
        _safeWork = safeWork;
        _thickness = thickness;
        _reservation = data.rc;
        PublishSnapshot();
        return true;
    }

    public void Remove()
    {
        if (_registered)
        {
            var data = CreateData();
            Shell32AppBar.SHAppBarMessage(ABM_REMOVE, ref data);
            _registered = false;
        }

        lock (SnapshotSync)
            ActiveReservations.Remove(_hwnd);

        _reservation = default;
        _thickness = 0;
    }

    /// <summary>
    /// GetMonitorInfo.rcWork includes Cedro after ABM_SETPOS. Existing Dock edge
    /// geometry must not consume that already-reduced rectangle again or the
    /// Dock would recursively move inward. This expands only Cedro's own strip
    /// back out while preserving the taskbar/other AppBars.
    /// </summary>
    public static void ExpandWorkAreaForOwnReservation(
        IntPtr hwnd, RECT monitor, ref RECT work)
    {
        ReservationSnapshot snapshot;
        lock (SnapshotSync)
        {
            if (!ActiveReservations.TryGetValue(hwnd, out snapshot))
                return;
        }

        if (!RectEquals(snapshot.Monitor, monitor))
            return;

        RECT reserved = snapshot.Reservation;
        switch (snapshot.Edge)
        {
            case AppBarEdge.Top:
                work.Top = Math.Min(work.Top, reserved.Top);
                break;
            case AppBarEdge.Bottom:
                work.Bottom = Math.Max(work.Bottom, reserved.Bottom);
                break;
            case AppBarEdge.Left:
                work.Left = Math.Min(work.Left, reserved.Left);
                break;
            case AppBarEdge.Right:
                work.Right = Math.Max(work.Right, reserved.Right);
                break;
        }

        work.Left = Math.Clamp(work.Left, monitor.Left, monitor.Right);
        work.Right = Math.Clamp(work.Right, monitor.Left, monitor.Right);
        work.Top = Math.Clamp(work.Top, monitor.Top, monitor.Bottom);
        work.Bottom = Math.Clamp(work.Bottom, monitor.Top, monitor.Bottom);
    }

    private bool EnsureRegistered()
    {
        if (_registered)
            return true;

        _callbackMessage = User32.RegisterWindowMessage(
            $"CedroModernDock.AppBar.{Environment.ProcessId}.{_hwnd.ToInt64():X}");

        if (_callbackMessage != 0)
        {
            _subclassProc = new SubclassProc(HandleMessage);
            _subclassed = Comctl32.SetWindowSubclass(
                _hwnd, _subclassProc, SubclassId, IntPtr.Zero);
        }

        var data = CreateData();
        UIntPtr result = Shell32AppBar.SHAppBarMessage(ABM_NEW, ref data);
        _registered = result != UIntPtr.Zero;
        if (!_registered)
            RemoveSubclass();
        return _registered;
    }

    private APPBARDATA CreateData() => new()
    {
        cbSize = (uint)Marshal.SizeOf<APPBARDATA>(),
        hWnd = _hwnd,
        uCallbackMessage = _callbackMessage
    };

    private IntPtr HandleMessage(
        IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam,
        UIntPtr uIdSubclass, IntPtr dwRefData)
    {
        if (_callbackMessage != 0 && uMsg == _callbackMessage &&
            unchecked((uint)wParam.ToInt64()) == ABN_POSCHANGED)
        {
            ShellPositionChanged?.Invoke();
        }

        return Comctl32.DefSubclassProc(hWnd, uMsg, wParam, lParam);
    }

    private void PublishSnapshot()
    {
        lock (SnapshotSync)
            ActiveReservations[_hwnd] = new ReservationSnapshot(_edge, _monitor, _reservation);
    }

    private void RemoveSubclass()
    {
        if (_subclassed && _subclassProc != null && _hwnd != IntPtr.Zero)
            Comctl32.RemoveWindowSubclass(_hwnd, _subclassProc, SubclassId);
        _subclassed = false;
        _subclassProc = null;
    }

    public void Dispose()
    {
        Remove();
        RemoveSubclass();
        ShellPositionChanged = null;
    }

    private static bool RectEquals(RECT a, RECT b)
        => a.Left == b.Left && a.Top == b.Top &&
           a.Right == b.Right && a.Bottom == b.Bottom;

    private readonly record struct ReservationSnapshot(
        AppBarEdge Edge, RECT Monitor, RECT Reservation);
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
