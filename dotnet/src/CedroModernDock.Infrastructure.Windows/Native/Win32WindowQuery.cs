using System.Text;
using System.IO;

namespace CedroModernDock.Infrastructure.Windows.Native;

/// <summary>
/// Direct port of the original Java NativeWindowUtils.
/// Enumerates open top-level windows matching a given executable path,
/// and activates (restores + foregrounds) a specific window.
/// </summary>
public static class Win32WindowQuery
{
    /// <summary>Minimal info required to activate and label a window.</summary>
    public sealed record WindowInfo(IntPtr Handle, string Title);

    /// <summary>Taskbar-visible window with its owning executable path.</summary>
    public sealed record RunningWindowInfo(IntPtr Handle, string Title, string ExecutablePath);

    /// <summary>
    /// Enumerates all visible top-level windows that would appear in the Windows
    /// taskbar, returning each with its owning executable path. Mirrors the
    /// taskbar's own filter: visible, titled, not a tool window, and either an
    /// app window or unowned. Excludes cloaked (Win+D hidden) windows.
    /// </summary>
    public static List<RunningWindowInfo> GetTaskbarWindows()
    {
        var windows = new List<RunningWindowInfo>();

        User32.EnumWindows((hWnd, _) =>
        {
            if (TryGetTaskbarWindow(hWnd, out var info))
                windows.Add(info!);
            return true;
        }, IntPtr.Zero);

        return windows;
    }

    private static bool TryGetTaskbarWindow(IntPtr hWnd, out RunningWindowInfo? info)
    {
        info = null;

        if (!User32.IsWindowVisible(hWnd))
            return false;

        // Skip cloaked windows (hidden by Win+D / minimized to taskbar).
        if (DwmThumbnailInterop.IsCloaked(hWnd))
            return false;

        // Skip the desktop window ("Program Manager", class Progman) and other
        // explorer-owned shell surfaces that never appear in the real taskbar.
        if (IsProgman(hWnd))
            return false;

        int exStyle = User32.GetWindowLongPtr(hWnd, Win32Constants.GWL_EXSTYLE).ToInt32();
        if ((exStyle & Win32Constants.WS_EX_TOOLWINDOW) != 0)
            return false;

        // Taskbar apps: either explicitly an app window or an unowned window.
        IntPtr owner = User32.GetWindow(hWnd, Win32Constants.GW_OWNER);
        if ((exStyle & Win32Constants.WS_EX_APPWINDOW) == 0 && owner != IntPtr.Zero)
            return false;

        var titleBuilder = new StringBuilder(1024);
        User32.GetWindowText(hWnd, titleBuilder, 1024);
        string title = titleBuilder.ToString().Trim();
        if (string.IsNullOrEmpty(title))
            return false;

        string? executablePath = GetProcessPath(hWnd);
        if (string.IsNullOrEmpty(executablePath))
            return false;

        // UWP/AppX apps: the visible window belongs to ApplicationFrameHost,
        // but the actual app runs in a child CoreWindow. Use the child's
        // executable so the dock groups, labels and icons it as the real app.
        if (string.Equals(System.IO.Path.GetFileName(executablePath),
                "ApplicationFrameHost.exe", StringComparison.OrdinalIgnoreCase))
        {
            string? appPath = GetChildProcessExePath(hWnd, executablePath);
            if (!string.IsNullOrEmpty(appPath))
                executablePath = appPath;
        }

        info = new RunningWindowInfo(hWnd, title, executablePath);
        return true;
    }

    /// <summary>
    /// Finds the first child window running a different executable than the
    /// frame host — for UWP apps that is the CoreWindow hosting the app UI.
    /// </summary>
    private static string? GetChildProcessExePath(IntPtr hWnd, string framePath)
    {
        string? result = null;
        User32.EnumChildWindows(hWnd, (child, _) =>
        {
            string? path = GetProcessPath(child);
            if (!string.IsNullOrEmpty(path) &&
                !string.Equals(path, framePath, StringComparison.OrdinalIgnoreCase))
            {
                result = path;
                return false;
            }
            return true;
        }, IntPtr.Zero);
        return result;
    }

    private static string? GetProcessPath(IntPtr hWnd)
    {
        User32.GetWindowThreadProcessId(hWnd, out uint pid);
        if (pid == 0) return null;

        IntPtr process = Kernel32.OpenProcess(
            Win32Constants.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (process == IntPtr.Zero) return null;

        try
        {
            var pathBuffer = new StringBuilder(1024);
            uint size = (uint)pathBuffer.Capacity;
            if (Kernel32.QueryFullProcessImageName(process, 0, pathBuffer, ref size))
                return NormalizePath(pathBuffer.ToString(0, (int)size));
            return null;
        }
        finally
        {
            Kernel32.CloseHandle(process);
        }
    }

    /// <summary>
    /// Returns all visible top-level windows whose owning process image path
    /// matches <paramref name="executablePath"/>.
    /// </summary>
    public static List<WindowInfo> GetOpenWindows(string? executablePath)
    {
        var windows = new List<WindowInfo>();
        if (string.IsNullOrEmpty(executablePath))
            return windows;

        var targetPath = NormalizePath(executablePath);

        User32.EnumWindows((hWnd, _) =>
        {
            if (!User32.IsWindowVisible(hWnd))
                return true;

            var titleBuilder = new StringBuilder(1024);
            User32.GetWindowText(hWnd, titleBuilder, 1024);
            string title = titleBuilder.ToString().Trim();

            // Skip hidden/invisible windows that report visible but have empty titles.
            if (string.IsNullOrEmpty(title))
                return true;

            // Skip the desktop window ("Program Manager", class Progman): it belongs
            // to explorer.exe but must never be listed as an open window.
            if (IsProgman(hWnd))
                return true;

            // Skip tool windows: they never appear in the taskbar, and the dock's
            // own windows (the dock bar and the preview popup) are tool windows.
            int exStyle = User32.GetWindowLongPtr(hWnd, Win32Constants.GWL_EXSTYLE).ToInt32();
            if ((exStyle & Win32Constants.WS_EX_TOOLWINDOW) != 0)
                return true;

            if (IsWindowFromExecutable(hWnd, targetPath))
                windows.Add(new WindowInfo(hWnd, title));

            return true;
        }, IntPtr.Zero);

        return windows;
    }

    private static bool IsProgman(IntPtr hWnd)
    {
        var classNameBuilder = new StringBuilder(256);
        User32.GetClassName(hWnd, classNameBuilder, 256);
        return string.Equals(classNameBuilder.ToString().Trim(),
            "Progman", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Restores the window if minimized and brings it to the foreground.</summary>
    public static void ActivateWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            return;

        User32.ShowWindow(hwnd, Win32Constants.SW_RESTORE);
        User32.SetForegroundWindow(hwnd);
    }

    private static bool IsWindowFromExecutable(IntPtr hWnd, string targetPath)
    {
        User32.GetWindowThreadProcessId(hWnd, out uint pid);

        IntPtr process = Kernel32.OpenProcess(
            Win32Constants.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);

        if (process == IntPtr.Zero)
            return false;

        try
        {
            var pathBuffer = new StringBuilder(1024);
            uint size = (uint)pathBuffer.Capacity;

            if (Kernel32.QueryFullProcessImageName(process, 0, pathBuffer, ref size))
            {
                string processPath = NormalizePath(pathBuffer.ToString(0, (int)size));
                return IsSameExecutable(processPath, targetPath);
            }
            return false;
        }
        finally
        {
            Kernel32.CloseHandle(process);
        }
    }

    private static bool IsSameExecutable(string processPath, string targetPath)
    {
        if (string.IsNullOrEmpty(processPath) || string.IsNullOrEmpty(targetPath))
            return false;

        // Exact, case-insensitive path match (Windows paths are case-insensitive).
        if (string.Equals(processPath, targetPath, StringComparison.OrdinalIgnoreCase))
            return true;

        // Fallback: match only the filename when the full path isn't comparable.
        string processFile = Path.GetFileName(processPath);
        string targetFile = Path.GetFileName(targetPath);
        return string.Equals(processFile, targetFile, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePath(string path)
    {
        path = Path.GetFullPath(path).Trim();
        // Strip Windows extended-length path prefix if present.
        if (path.StartsWith(@"\\?\", StringComparison.Ordinal))
            path = path[4..];
        return path;
    }
}
