namespace CedroModernDock.Core.Domain;

using CedroModernDock.Core.Application;

/// <summary>
/// Port for querying open native windows (for running-app indicators
/// and window-preview popups). The WindowInfo record is shared so the
/// application layer never depends on Win32 HWND types directly.
/// </summary>
public interface IWindowQueryGateway
{
    List<WindowInfo> FindOpenWindows(string? executablePath);
    void Activate(WindowInfo windowInfo);
}

/// <summary>Minimal info required to activate and label a window.</summary>
public sealed record WindowInfo(IntPtr Handle, string Title);
