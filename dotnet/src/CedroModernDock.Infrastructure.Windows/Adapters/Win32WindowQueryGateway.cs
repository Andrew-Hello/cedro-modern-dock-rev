namespace CedroModernDock.Infrastructure.Windows.Adapters;

using CedroModernDock.Core.Application;
using CedroModernDock.Core.Domain;
using CedroModernDock.Infrastructure.Windows.Native;

/// <summary>
/// Adapter wrapping Win32WindowQuery to implement IWindowQueryGateway.
/// Direct port of JnaWindowQueryGateway.java.
/// </summary>
public class Win32WindowQueryGateway : IWindowQueryGateway
{
    public List<WindowInfo> FindOpenWindows(string? executablePath)
    {
        var windows = Win32WindowQuery.GetOpenWindows(executablePath);
        return windows.Select(w => new WindowInfo(w.Handle, w.Title)).ToList();
    }

    public List<RunningWindowInfo> FindTaskbarWindows()
    {
        return Win32WindowQuery.GetTaskbarWindows()
            .Select(w => new RunningWindowInfo(w.Handle, w.Title, w.ExecutablePath))
            .ToList();
    }

    public void Activate(WindowInfo windowInfo)
    {
        Win32WindowQuery.ActivateWindow(windowInfo.Handle);
    }
}
