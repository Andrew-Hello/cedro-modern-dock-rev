namespace CedroModernDock.Infrastructure.Windows.Adapters;

using CedroModernDock.Core.Application;
using CedroModernDock.Core.Domain;
using CedroModernDock.Infrastructure.Windows.Native;

/// <summary>Adapter wrapping Win32WindowQuery to implement IWindowQueryGateway.</summary>
public class Win32WindowQueryGateway : IWindowQueryGateway
{
    public List<WindowInfo> FindOpenWindows(string? executablePath, string? appUserModelId = null)
    {
        var windows = Win32WindowQuery.GetOpenWindows(executablePath, appUserModelId);
        return windows.Select(w => new WindowInfo(w.Handle, w.Title)).ToList();
    }

    public List<RunningWindowInfo> FindTaskbarWindows()
    {
        return Win32WindowQuery.GetTaskbarWindows()
            .Select(w => new RunningWindowInfo(
                w.Handle, w.Title, w.ExecutablePath, w.AppUserModelId))
            .ToList();
    }

    public void Activate(WindowInfo windowInfo)
    {
        Win32WindowQuery.ActivateWindow(windowInfo.Handle);
    }

    public void Close(WindowInfo windowInfo)
    {
        Win32WindowQuery.CloseWindow(windowInfo.Handle);
    }
}
