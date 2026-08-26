using System.Text;
using System.IO;

namespace CedroModernDock.Infrastructure.Windows.Native;

/// <summary>
/// Enumerates taskbar/open windows and activates them. Enhanced builds resolve
/// AppUserModelID as well as executable paths so packaged Windows apps and
/// desktop PWAs that share a host executable can keep distinct identities.
/// </summary>
public static class Win32WindowQuery
{
    /// <summary>Minimal info required to activate and label a window.</summary>
    public sealed record WindowInfo(IntPtr Handle, string Title);

    /// <summary>Taskbar-visible window with its owning executable and optional AUMID.</summary>
    public sealed record RunningWindowInfo(
        IntPtr Handle,
        string Title,
        string ExecutablePath,
        string? AppUserModelId = null);

    // For UWP/AppX apps the visible top-level window can be an
    // ApplicationFrameHost frame whose real app process lives in a child
    // CoreWindow. When minimized, that child can be temporarily reparented;
    // cache both path and app identity per frame HWND to keep the dock stable.
    private static readonly Dictionary<IntPtr, string> _frameAppExeCache = new();
    private static readonly Dictionary<IntPtr, string> _frameAppIdCache = new();

    /// <summary>
    /// Enumerates visible taskbar windows and resolves a stable app identity
    /// where Windows exposes one.
    /// </summary>
    public static List<RunningWindowInfo> GetTaskbarWindows()
    {
        var windows = new List<RunningWindowInfo>();

        foreach (var handle in _frameAppExeCache.Keys
                     .Concat(_frameAppIdCache.Keys).Distinct().ToList())
        {
            if (!User32.IsWindow(handle))
            {
                _frameAppExeCache.Remove(handle);
                _frameAppIdCache.Remove(handle);
            }
        }

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

        if (DwmThumbnailInterop.IsCloaked(hWnd))
            return false;

        if (IsProgman(hWnd))
            return false;

        int exStyle = User32.GetWindowLongPtr(hWnd, Win32Constants.GWL_EXSTYLE).ToInt32();
        if ((exStyle & Win32Constants.WS_EX_TOOLWINDOW) != 0)
            return false;

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

        bool isFrameHost = string.Equals(Path.GetFileName(executablePath),
            "ApplicationFrameHost.exe", StringComparison.OrdinalIgnoreCase);

        if (isFrameHost)
        {
            string? appPath = GetChildProcessExePath(hWnd, executablePath);
            if (!string.IsNullOrEmpty(appPath))
            {
                _frameAppExeCache[hWnd] = appPath;
                executablePath = appPath;
            }
            else if (_frameAppExeCache.TryGetValue(hWnd, out string? cached))
            {
                executablePath = cached;
            }
        }

        string? appUserModelId = GetEffectiveAppUserModelId(hWnd, isFrameHost);
        info = new RunningWindowInfo(hWnd, title, executablePath, appUserModelId);
        return true;
    }

    private static string? GetEffectiveAppUserModelId(IntPtr hWnd, bool? knownFrameHost = null)
    {
        string? direct = WindowAppIdentity.TryGetAppUserModelId(hWnd);
        if (!string.IsNullOrWhiteSpace(direct))
        {
            if (knownFrameHost == true)
                _frameAppIdCache[hWnd] = direct;
            return direct;
        }

        bool isFrameHost = knownFrameHost ?? string.Equals(
            Path.GetFileName(GetProcessPath(hWnd)),
            "ApplicationFrameHost.exe", StringComparison.OrdinalIgnoreCase);

        if (!isFrameHost)
            return null;

        string? childId = GetChildAppUserModelId(hWnd);
        if (!string.IsNullOrWhiteSpace(childId))
        {
            _frameAppIdCache[hWnd] = childId;
            return childId;
        }

        return _frameAppIdCache.TryGetValue(hWnd, out string? cachedId)
            ? cachedId : null;
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

    private static string? GetChildAppUserModelId(IntPtr hWnd)
    {
        string? result = null;
        User32.EnumChildWindows(hWnd, (child, _) =>
        {
            string? id = WindowAppIdentity.TryGetAppUserModelId(child);
            if (!string.IsNullOrWhiteSpace(id))
            {
                result = id;
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
    /// Returns all visible top-level windows matching either a stable AUMID
    /// (preferred) or the executable path. AUMID matching is essential for
    /// packaged apps and installed web apps that can share a host executable.
    /// </summary>
    public static List<WindowInfo> GetOpenWindows(
        string? executablePath,
        string? appUserModelId = null)
    {
        var windows = new List<WindowInfo>();
        bool useAppId = !string.IsNullOrWhiteSpace(appUserModelId);
        if (!useAppId && string.IsNullOrEmpty(executablePath))
            return windows;

        string? targetPath = string.IsNullOrEmpty(executablePath)
            ? null : NormalizePath(executablePath);

        User32.EnumWindows((hWnd, _) =>
        {
            if (!User32.IsWindowVisible(hWnd))
                return true;

            var titleBuilder = new StringBuilder(1024);
            User32.GetWindowText(hWnd, titleBuilder, 1024);
            string title = titleBuilder.ToString().Trim();
            if (string.IsNullOrEmpty(title))
                return true;

            if (IsProgman(hWnd))
                return true;

            int exStyle = User32.GetWindowLongPtr(hWnd, Win32Constants.GWL_EXSTYLE).ToInt32();
            if ((exStyle & Win32Constants.WS_EX_TOOLWINDOW) != 0)
                return true;

            var className = new StringBuilder(256);
            User32.GetClassName(hWnd, className, 256);
            string cls = className.ToString().Trim();
            if (string.Equals(cls, "IME", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(cls, "MSCTFIME UI", StringComparison.OrdinalIgnoreCase))
                return true;

            bool matches;
            if (useAppId)
            {
                string? candidateId = GetEffectiveAppUserModelId(hWnd);
                matches = string.Equals(candidateId, appUserModelId,
                    StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                matches = targetPath != null && IsWindowFromExecutable(hWnd, targetPath);
            }

            if (matches)
            {
                IntPtr resolved = ResolveOpenWindow(hWnd, title);
                if (!windows.Any(w => w.Handle == resolved))
                    windows.Add(new WindowInfo(resolved, title));
            }

            return true;
        }, IntPtr.Zero);

        return windows;
    }

    /// <summary>
    /// For UWP/AppX apps the visible top-level window belongs to
    /// ApplicationFrameHost, while a matching CoreWindow may be cloaked. Resolve
    /// activation/preview to the visible frame that hosts the same titled window.
    /// </summary>
    private static IntPtr ResolveOpenWindow(IntPtr hWnd, string title)
    {
        IntPtr frame = FindFrameWithTitle(title);
        if (frame != IntPtr.Zero)
            return frame;
        return hWnd;
    }

    private static IntPtr FindFrameWithTitle(string title)
    {
        IntPtr result = IntPtr.Zero;
        if (string.IsNullOrEmpty(title))
            return result;

        User32.EnumWindows((hWnd, _) =>
        {
            if (!User32.IsWindowVisible(hWnd) || DwmThumbnailInterop.IsCloaked(hWnd))
                return true;

            int exStyle = User32.GetWindowLongPtr(hWnd, Win32Constants.GWL_EXSTYLE).ToInt32();
            if ((exStyle & Win32Constants.WS_EX_TOOLWINDOW) != 0)
                return true;

            string? path = GetProcessPath(hWnd);
            if (!string.Equals(Path.GetFileName(path),
                    "ApplicationFrameHost.exe", StringComparison.OrdinalIgnoreCase))
                return true;

            var titleBuilder = new StringBuilder(1024);
            User32.GetWindowText(hWnd, titleBuilder, 1024);
            if (string.Equals(titleBuilder.ToString().Trim(), title, StringComparison.OrdinalIgnoreCase))
            {
                result = hWnd;
                return false;
            }
            return true;
        }, IntPtr.Zero);

        return result;
    }

    private static bool IsProgman(IntPtr hWnd)
    {
        var classNameBuilder = new StringBuilder(256);
        User32.GetClassName(hWnd, classNameBuilder, 256);
        return string.Equals(classNameBuilder.ToString().Trim(),
            "Progman", StringComparison.OrdinalIgnoreCase);
    }

    public static void ActivateWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            return;

        User32.ShowWindow(hwnd, Win32Constants.SW_RESTORE);
        User32.SetForegroundWindow(hwnd);
    }

    public static void CloseWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            return;
        User32.PostMessage(hwnd, (uint)Win32Constants.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
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

        if (string.Equals(processPath, targetPath, StringComparison.OrdinalIgnoreCase))
            return true;

        string processFile = Path.GetFileName(processPath);
        string targetFile = Path.GetFileName(targetPath);
        return string.Equals(processFile, targetFile, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePath(string path)
    {
        path = Path.GetFullPath(path).Trim();
        if (path.StartsWith(@"\\?\", StringComparison.Ordinal))
            path = path[4..];
        return path;
    }
}
