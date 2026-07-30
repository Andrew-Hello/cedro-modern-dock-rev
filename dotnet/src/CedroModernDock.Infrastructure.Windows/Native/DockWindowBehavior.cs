namespace CedroModernDock.Infrastructure.Windows.Native;

/// <summary>
/// Applies dock-specific Win32 window behavior that JavaFX/Avalonia alone cannot provide:
/// <list type="bullet">
/// <item><b>WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW</b> — the dock never steals focus
/// and never appears in the taskbar or Alt-Tab cycle.</item>
/// <item><b>Subclass + shell hook</b> — intercepts minimize commands (including the
/// ones Win+D "Show Desktop" sends) and re-asserts topmost Z-order after desktop
/// activation events, so the dock survives Win+D.</item>
/// </list>
/// This is the crux of the port: the exact behavior that was unreachable under JavaFX.
/// </summary>
public sealed class DockWindowBehavior : IDisposable
{
    private IntPtr _hwnd;
    private readonly Action<string>? _onStatus;
    private SubclassProc? _subclassProc; // kept alive to prevent GC of the native callback
    private uint _shellHookMessage;
    private bool _shellHookRegistered;
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

        _shellHookMessage = User32.RegisterWindowMessage("SHELLHOOK");
        _shellHookRegistered = User32.RegisterShellHookWindow(_hwnd);

        // Store the delegate in a field so it is not garbage-collected while
        // the native subclass is active (would cause a crash on next message).
        _subclassProc = new SubclassProc(HandleMessage);
        _subclassed = Comctl32.SetWindowSubclass(_hwnd, _subclassProc, UIntPtr.Zero, IntPtr.Zero);

        _onStatus?.Invoke(
            $"HWND=0x{_hwnd:X} | ShellHook={(_shellHookRegistered ? "OK" : "FAIL")} " +
            $"| Subclass={(_subclassed ? "OK" : "FAIL")} | msg=0x{_shellHookMessage:X}");
    }

    private void ApplyExtendedStyles()
    {
        IntPtr exStylePtr = User32.GetWindowLongPtr(_hwnd, Win32Constants.GWL_EXSTYLE);
        int exStyle = exStylePtr.ToInt32();

        exStyle |= Win32Constants.WS_EX_NOACTIVATE | Win32Constants.WS_EX_TOOLWINDOW;
        exStyle &= ~Win32Constants.WS_EX_APPWINDOW; // remove taskbar entry

        User32.SetWindowLongPtr(_hwnd, Win32Constants.GWL_EXSTYLE, new IntPtr(exStyle));
    }

    private IntPtr HandleMessage(
        IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam,
        UIntPtr uIdSubclass, IntPtr dwRefData)
    {
        // 1. Block explicit minimize (SC_MINIMIZE) — covers normal "Minimize" clicks.
        if (uMsg == Win32Constants.WM_SYSCOMMAND)
        {
            int command = wParam.ToInt32() & 0xFFF0;
            if (command == Win32Constants.SC_MINIMIZE)
            {
                _onStatus?.Invoke("Blocked SC_MINIMIZE");
                return IntPtr.Zero; // swallow the message; do not minimize
            }
        }

        // 2. Win+D ("Show Desktop") may minimize windows without SC_MINIMIZE.
        //    If we receive WM_SIZE with SIZE_MINIMIZED, undo it immediately.
        if (uMsg == Win32Constants.WM_SIZE && wParam.ToInt32() == Win32Constants.SIZE_MINIMIZED)
        {
            User32.ShowWindow(_hwnd, Win32Constants.SW_RESTORE);
            ReassertTopmost();
            _onStatus?.Invoke("Restored from SIZE_MINIMIZED (Win+D defense)");
        }

        // 3. Shell hook: when another window is activated (including after Show Desktop),
        //    re-assert our topmost Z-order so the dock stays on top.
        if (uMsg == _shellHookMessage && _shellHookRegistered)
        {
            int shellCode = wParam.ToInt32();
            if (shellCode == Win32Constants.HSHELL_WINDOWACTIVATED ||
                shellCode == Win32Constants.HSHELL_RUDEAPPACTIVATED)
            {
                ReassertTopmost();
                _onStatus?.Invoke($"ShellHook code={shellCode} → re-asserted topmost");
            }
        }

        return Comctl32.DefSubclassProc(hWnd, uMsg, wParam, lParam);
    }

    private void ReassertTopmost()
    {
        User32.SetWindowPos(
            _hwnd, Win32Constants.HWND_TOPMOST,
            0, 0, 0, 0,
            Win32Constants.SWP_NOMOVE | Win32Constants.SWP_NOSIZE |
            Win32Constants.SWP_NOACTIVATE | Win32Constants.SWP_SHOWWINDOW);
    }

    public void Dispose()
    {
        if (_subclassed && _subclassProc != null && _hwnd != IntPtr.Zero)
        {
            Comctl32.RemoveWindowSubclass(_hwnd, _subclassProc, UIntPtr.Zero);
            _subclassed = false;
        }

        // No DeregisterShellHookWindow exists — the shell hook is cleaned up
        // automatically when the window is destroyed.
        _subclassProc = null;
        _shellHookRegistered = false;
        _hwnd = IntPtr.Zero;
    }
}
