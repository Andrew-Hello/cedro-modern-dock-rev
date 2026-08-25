namespace CedroModernDock.Infrastructure.Windows.Native;

using System;

/// <summary>
/// Applies dock-specific Win32 window behavior that Avalonia alone cannot provide:
/// <list type="bullet">
/// <item><b>WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW</b> — the dock never steals focus
/// and never appears in the taskbar or Alt-Tab cycle.</item>
/// <item><b>Topmost top-level window</b> — the dock remains above normal application
/// windows without being parented to the desktop WorkerW. Keeping it top-level is
/// also important for correct DWM alpha composition over desktop icons.</item>
/// <item><b>Subclass</b> — defense in depth: intercepts minimize commands
/// (SC_MINIMIZE, SIZE_MINIMIZED) so Win+D does not permanently hide the dock.</item>
/// </list>
/// </summary>
public sealed class DockWindowBehavior : IDisposable
{
    private IntPtr _hwnd;
    private readonly Action<string>? _onStatus;
    private SubclassProc? _subclassProc; // kept alive to prevent GC of the native callback
    private bool _subclassed;

    public DockWindowBehavior(IntPtr hwnd, Action<string>? onStatus = null)
    {
        _hwnd = hwnd;
        _onStatus = onStatus;
    }

    public void Apply()
    {
        if (_hwnd == IntPtr.Zero)
        {
            _onStatus?.Invoke("ERROR: HWND is zero — window not opened yet?");
            return;
        }

        ApplyExtendedStyles();
        ApplyTopmost();

        // Store the delegate in a field so it is not garbage-collected while
        // the native subclass is active (would cause a crash on next message).
        _subclassProc = new SubclassProc(HandleMessage);
        _subclassed = Comctl32.SetWindowSubclass(_hwnd, _subclassProc, UIntPtr.Zero, IntPtr.Zero);

        _onStatus?.Invoke($"HWND=0x{_hwnd:X} | Topmost=ON | Subclass={(_subclassed ? "OK" : "FAIL")}");
    }

    private void ApplyExtendedStyles()
    {
        IntPtr exStylePtr = User32.GetWindowLongPtr(_hwnd, Win32Constants.GWL_EXSTYLE);
        int exStyle = exStylePtr.ToInt32();

        exStyle |= Win32Constants.WS_EX_NOACTIVATE |
                   Win32Constants.WS_EX_TOOLWINDOW |
                   Win32Constants.WS_EX_TOPMOST;
        exStyle &= ~Win32Constants.WS_EX_APPWINDOW; // remove taskbar entry

        User32.SetWindowLongPtr(_hwnd, Win32Constants.GWL_EXSTYLE, new IntPtr(exStyle));
    }

    /// <summary>
    /// Keeps the dock as an independent top-level popup and places it in the
    /// topmost band. Do not SetParent() it to Progman/WorkerW: doing so changes
    /// the DWM composition relationship and can make alpha transparency show
    /// the wallpaper while hiding Explorer desktop icons underneath.
    /// </summary>
    private void ApplyTopmost()
    {
        bool ok = User32.SetWindowPos(
            _hwnd,
            Win32Constants.HWND_TOPMOST,
            0, 0, 0, 0,
            Win32Constants.SWP_NOMOVE |
            Win32Constants.SWP_NOSIZE |
            Win32Constants.SWP_NOACTIVATE |
            Win32Constants.SWP_SHOWWINDOW);

        _onStatus?.Invoke(ok ? "Topmost enabled" : "WARNING: SetWindowPos(HWND_TOPMOST) failed");
    }

    private IntPtr HandleMessage(
        IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam,
        UIntPtr uIdSubclass, IntPtr dwRefData)
    {
        // 1. Block explicit minimize (SC_MINIMIZE) — covers normal "Minimize"
        //    clicks and some Win+D / Show Desktop paths.
        if (uMsg == Win32Constants.WM_SYSCOMMAND)
        {
            int command = wParam.ToInt32() & 0xFFF0;
            if (command == Win32Constants.SC_MINIMIZE)
            {
                _onStatus?.Invoke("Blocked SC_MINIMIZE");
                return IntPtr.Zero; // swallow the message; do not minimize
            }
        }

        // 2. If another shell path still produces SIZE_MINIMIZED, restore the
        //    dock immediately and reassert topmost without activating it.
        if (uMsg == Win32Constants.WM_SIZE && wParam.ToInt32() == Win32Constants.SIZE_MINIMIZED)
        {
            User32.ShowWindow(_hwnd, Win32Constants.SW_RESTORE);
            ApplyTopmost();
            _onStatus?.Invoke("Restored from SIZE_MINIMIZED (Win+D defense)");
        }

        return Comctl32.DefSubclassProc(hWnd, uMsg, wParam, lParam);
    }

    public void Dispose()
    {
        if (_subclassed && _subclassProc != null && _hwnd != IntPtr.Zero)
        {
            Comctl32.RemoveWindowSubclass(_hwnd, _subclassProc, UIntPtr.Zero);
            _subclassed = false;
        }

        _subclassProc = null;
        _hwnd = IntPtr.Zero;
    }
}
