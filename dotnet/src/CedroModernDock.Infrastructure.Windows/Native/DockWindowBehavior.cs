namespace CedroModernDock.Infrastructure.Windows.Native;

using System;

/// <summary>
/// Applies dock-specific Win32 window behavior that Avalonia alone cannot provide:
/// <list type="bullet">
/// <item><b>WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW</b> — the dock never steals focus
/// and never appears in the taskbar or Alt-Tab cycle.</item>
/// <item><b>Independent top-level window</b> — keeps correct DWM alpha composition
/// over desktop icons while allowing topmost to be enabled or disabled at runtime.</item>
/// <item><b>Subclass</b> — defense in depth: intercepts minimize commands
/// (SC_MINIMIZE, SIZE_MINIMIZED) so Win+D does not permanently hide the dock.</item>
/// <item><b>Display environment notification</b> — forwards only genuine display
/// resolution/topology and per-monitor DPI changes. Work-area notifications are
/// intentionally ignored because AppBar itself changes the work area.</item>
/// </list>
/// </summary>
public sealed class DockWindowBehavior : IDisposable
{
    private static readonly IntPtr HwndNotTopmost = new(-2);

    // These are deliberately kept local instead of broadening the shared Win32
    // surface. They are the only native messages Bottom AppBar needs in order to
    // re-negotiate after a monitor/resolution/DPI transition.
    private const uint WM_DISPLAYCHANGE = 0x007E;
    private const uint WM_DPICHANGED = 0x02E0;

    private IntPtr _hwnd;
    private readonly Action<string>? _onStatus;
    private SubclassProc? _subclassProc; // kept alive to prevent GC of the native callback
    private bool _subclassed;
    private bool _topmostEnabled;

    /// <summary>
    /// Raised on the window thread after Windows reports a display topology /
    /// resolution change or a per-monitor DPI change. Consumers should debounce
    /// this event because a physical monitor transition may emit both messages.
    /// </summary>
    public event Action? DisplayEnvironmentChanged;

    public DockWindowBehavior(IntPtr hwnd, Action<string>? onStatus = null)
    {
        _hwnd = hwnd;
        _onStatus = onStatus;
    }

    public IntPtr WindowHandle => _hwnd;
    public bool TopmostEnabled => _topmostEnabled;

    public void Apply()
    {
        if (_hwnd == IntPtr.Zero)
        {
            _onStatus?.Invoke("ERROR: HWND is zero — window not opened yet?");
            return;
        }

        ApplyExtendedStyles();
        SetTopmost(true);

        // Store the delegate in a field so it is not garbage-collected while
        // the native subclass is active (would cause a crash on next message).
        _subclassProc = new SubclassProc(HandleMessage);
        _subclassed = Comctl32.SetWindowSubclass(_hwnd, _subclassProc, UIntPtr.Zero, IntPtr.Zero);

        _onStatus?.Invoke($"HWND=0x{_hwnd:X} | Topmost={(_topmostEnabled ? "ON" : "OFF")} | Subclass={(_subclassed ? "OK" : "FAIL")}");
    }

    private void ApplyExtendedStyles()
    {
        IntPtr exStylePtr = User32.GetWindowLongPtr(_hwnd, Win32Constants.GWL_EXSTYLE);
        int exStyle = exStylePtr.ToInt32();

        exStyle |= Win32Constants.WS_EX_NOACTIVATE | Win32Constants.WS_EX_TOOLWINDOW;
        exStyle &= ~Win32Constants.WS_EX_APPWINDOW; // remove taskbar entry

        User32.SetWindowLongPtr(_hwnd, Win32Constants.GWL_EXSTYLE, new IntPtr(exStyle));
    }

    /// <summary>
    /// Enables or disables the topmost band without activating the dock.
    /// This is intentionally runtime-configurable so user preference and
    /// fullscreen suppression can share the same mechanism.
    /// </summary>
    public void SetTopmost(bool enabled)
    {
        if (_hwnd == IntPtr.Zero) return;
        if (_topmostEnabled == enabled) return;

        _topmostEnabled = enabled;
        ApplyTopmostState();
        _onStatus?.Invoke(enabled ? "Topmost enabled" : "Topmost disabled");
    }

    private void ApplyTopmostState()
    {
        if (_hwnd == IntPtr.Zero) return;

        IntPtr exStylePtr = User32.GetWindowLongPtr(_hwnd, Win32Constants.GWL_EXSTYLE);
        int exStyle = exStylePtr.ToInt32();
        if (_topmostEnabled)
            exStyle |= Win32Constants.WS_EX_TOPMOST;
        else
            exStyle &= ~Win32Constants.WS_EX_TOPMOST;
        User32.SetWindowLongPtr(_hwnd, Win32Constants.GWL_EXSTYLE, new IntPtr(exStyle));

        bool ok = User32.SetWindowPos(
            _hwnd,
            _topmostEnabled ? Win32Constants.HWND_TOPMOST : HwndNotTopmost,
            0, 0, 0, 0,
            Win32Constants.SWP_NOMOVE |
            Win32Constants.SWP_NOSIZE |
            Win32Constants.SWP_NOACTIVATE |
            Win32Constants.SWP_SHOWWINDOW);

        if (!ok)
            _onStatus?.Invoke("WARNING: SetWindowPos(topmost state) failed");
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
        //    dock immediately and reassert the currently requested z-order
        //    without activating it.
        if (uMsg == Win32Constants.WM_SIZE && wParam.ToInt32() == Win32Constants.SIZE_MINIMIZED)
        {
            User32.ShowWindow(_hwnd, Win32Constants.SW_RESTORE);
            ApplyTopmostState();
            _onStatus?.Invoke("Restored from SIZE_MINIMIZED (Win+D defense)");
        }

        // 3. A Bottom AppBar negotiated on one monitor cannot safely reuse that
        //    monitor's fixed boundary after resolution/DPI/topology changes.
        //    Notify the UI layer, which performs a deliberately debounced full
        //    remove-and-register cycle. Do NOT listen to WM_SETTINGCHANGE here:
        //    AppBar's own work-area changes can generate it and recreate the old
        //    recursive feedback problem.
        if (uMsg == WM_DISPLAYCHANGE || uMsg == WM_DPICHANGED)
        {
            try
            {
                DisplayEnvironmentChanged?.Invoke();
            }
            catch (Exception ex)
            {
                // Never let managed subscriber failures escape a native window
                // subclass callback; doing so could destabilize Explorer/windowing.
                _onStatus?.Invoke($"WARNING: display-change handler failed: {ex.Message}");
            }
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

        DisplayEnvironmentChanged = null;
        _subclassProc = null;
        _hwnd = IntPtr.Zero;
    }
}
