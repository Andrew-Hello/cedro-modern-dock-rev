namespace CedroModernDock.Infrastructure.Windows.Adapters;

using CedroModernDock.Core.Domain;
using CedroModernDock.Infrastructure.Windows.Native;

/// <summary>
/// Adapter wrapping Windows icon extraction to implement IIconGateway.
/// Executables use embedded resources while script launchers use the Windows
/// Shell file-type icon so they remain visible in the Dock before a custom icon
/// is assigned.
/// </summary>
public class CachedWindowsIconGateway : IIconGateway
{
    public string? ResolveProgramIcon(string executablePath)
        => WindowsIconExtractor.GetCachedIconPath(executablePath);

    public string? ResolveFolderIcon(string folderPath)
        => WindowsIconExtractor.GetCachedFolderIconPath(folderPath);

    public void CacheProgramIcon(string executablePath)
    {
        if (IsScriptLauncher(executablePath))
            WindowsShellFileIconExtractor.ExtractAndCacheIcon(executablePath);
        else
            WindowsIconExtractor.ExtractAndCacheIcon(executablePath);
    }

    public void CacheFolderIcon(string folderPath)
        => WindowsIconExtractor.ExtractAndCacheFolderIcon(folderPath);

    private static bool IsScriptLauncher(string path)
    {
        string extension = Path.GetExtension(path);
        return extension.Equals(".bat", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".cmd", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".vbs", StringComparison.OrdinalIgnoreCase);
    }
}
