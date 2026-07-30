namespace CedroModernDock.Core.Application;

using CedroModernDock.Core.Domain;
using CedroModernDock.Core.Models;

/// <summary>Direct port of WindowPreviewService.</summary>
public class WindowPreviewService
{
    private readonly IWindowQueryGateway _windowQueryGateway;

    public WindowPreviewService(IWindowQueryGateway windowQueryGateway)
    {
        _windowQueryGateway = windowQueryGateway;
    }

    public List<WindowInfo> LoadPreview(DockProgramItemModel item) =>
        _windowQueryGateway.FindOpenWindows(item.ExecutablePath);

    public bool HasOpenWindows(string? executablePath) =>
        _windowQueryGateway.FindOpenWindows(executablePath).Count > 0;

    public void Activate(WindowInfo windowInfo) => _windowQueryGateway.Activate(windowInfo);
}
