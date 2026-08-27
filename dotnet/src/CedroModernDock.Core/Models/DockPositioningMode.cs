namespace CedroModernDock.Core.Models;

/// <summary>
/// Dock positioning strategy. BOTTOM_APPBAR is intentionally a separate mode
/// rather than a modifier on STATIC/DYNAMIC positioning: it has a much stricter
/// lifecycle and geometry contract with the Windows Shell.
/// </summary>
public enum DockPositioningMode
{
    STATIC,
    DYNAMIC,
    BOTTOM_APPBAR
}
