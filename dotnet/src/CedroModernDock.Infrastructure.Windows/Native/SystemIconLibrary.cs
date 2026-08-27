using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace CedroModernDock.Infrastructure.Windows.Native;

/// <summary>One known Windows icon-bearing resource library.</summary>
public sealed record SystemIconLibraryDescriptor(
    string Category,
    string FileName,
    string SourceExpression)
{
    public string ResolvedPath => SystemIconLibrary.ResolveSourcePath(SourceExpression);
    public bool IsAvailable => File.Exists(ResolvedPath);
}

/// <summary>Persistable system-icon selection: resource source + zero-based icon ordinal.</summary>
public sealed record SystemIconSelection(string SourceExpression, int IconIndex);

/// <summary>
/// Reads icon resources from a curated set of Windows DLL/EXE icon libraries.
/// Unlike imported PNG/ICO overrides, a system icon is persisted as
/// SourceExpression + IconIndex and extracted dynamically at runtime. This keeps
/// config.json compact and does not copy Microsoft system icon bytes into it.
/// </summary>
public static class SystemIconLibrary
{
    /// <summary>
    /// Curated catalog grouped by the use cases exposed in the picker. Some
    /// resource files are not shipped by every Windows edition/version; the UI
    /// automatically skips unavailable entries while keeping the catalog stable.
    /// </summary>
    public static IReadOnlyList<SystemIconLibraryDescriptor> Libraries { get; } =
        new[]
        {
            // Common
            new SystemIconLibraryDescriptor("common", "SHELL32.dll", @"%SystemRoot%\System32\SHELL32.dll"),
            new SystemIconLibraryDescriptor("common", "imageres.dll", @"%SystemRoot%\System32\imageres.dll"),

            // Devices
            new SystemIconLibraryDescriptor("devices", "DDORes.dll", @"%SystemRoot%\System32\DDORes.dll"),
            new SystemIconLibraryDescriptor("devices", "setupapi.dll", @"%SystemRoot%\System32\setupapi.dll"),
            new SystemIconLibraryDescriptor("devices", "compstui.dll", @"%SystemRoot%\System32\compstui.dll"),

            // Network
            new SystemIconLibraryDescriptor("network", "netshell.dll", @"%SystemRoot%\System32\netshell.dll"),
            new SystemIconLibraryDescriptor("network", "netcenter.dll", @"%SystemRoot%\System32\netcenter.dll"),
            new SystemIconLibraryDescriptor("network", "networkexplorer.dll", @"%SystemRoot%\System32\networkexplorer.dll"),

            // Classic
            new SystemIconLibraryDescriptor("classic", "moricons.dll", @"%SystemRoot%\System32\moricons.dll"),
            new SystemIconLibraryDescriptor("classic", "pifmgr.dll", @"%SystemRoot%\System32\pifmgr.dll"),

            // Other useful Windows resources
            new SystemIconLibraryDescriptor("other", "explorer.exe", @"%SystemRoot%\explorer.exe"),
            new SystemIconLibraryDescriptor("other", "mmres.dll", @"%SystemRoot%\System32\mmres.dll"),
            new SystemIconLibraryDescriptor("other", "wmploc.dll", @"%SystemRoot%\System32\wmploc.dll")
        };

    public static IReadOnlyList<SystemIconLibraryDescriptor> AvailableLibraries
        => Libraries.Where(library => library.IsAvailable).ToArray();

    public static string DefaultShell32Path
        => ResolveSourcePath(@"%SystemRoot%\System32\SHELL32.dll");

    /// <summary>
    /// Expands environment variables at runtime. Persisted config intentionally
    /// keeps expressions such as %SystemRoot% so it remains portable across
    /// Windows installations that use a different system drive/root directory.
    /// </summary>
    public static string ResolveSourcePath(string sourceExpression)
    {
        if (string.IsNullOrWhiteSpace(sourceExpression))
            return string.Empty;

        string root = Environment.GetEnvironmentVariable("SystemRoot") ?? @"C:\Windows";
        string source = sourceExpression.Replace(
            "%SystemRoot%", root, StringComparison.OrdinalIgnoreCase);
        source = Environment.ExpandEnvironmentVariables(source);

        try { return Path.GetFullPath(source); }
        catch { return source; }
    }

    /// <summary>Returns the number of icon groups in the supplied library.</summary>
    public static int GetIconCount(string sourceExpression)
    {
        string path = ResolveSourcePath(sourceExpression);
        if (!File.Exists(path))
            return 0;

        // ExtractIconEx with index -1 and nIcons 0 returns the number of icon
        // resources without actually creating HICON handles.
        uint count = Shell32IconLibrary.ExtractIconExW(
            path, -1, IntPtr.Zero, IntPtr.Zero, 0);
        return count > int.MaxValue ? int.MaxValue : (int)count;
    }

    /// <summary>
    /// Extracts one icon resource by zero-based ordinal and returns normalized
    /// PNG bytes. HICON ownership is fully released inside this method.
    /// </summary>
    public static byte[]? ExtractPngBytes(string sourceExpression, int iconIndex, int size)
    {
        string libraryPath = ResolveSourcePath(sourceExpression);
        if (string.IsNullOrWhiteSpace(libraryPath) || !File.Exists(libraryPath) || iconIndex < 0)
            return null;

        size = Math.Clamp(size, 16, 256);
        var icons = new IntPtr[1];
        var iconIds = new int[1];

        int extracted = User32Icon.PrivateExtractIcons(
            libraryPath, iconIndex, size, size, icons, iconIds, 1, 0);
        if (extracted <= 0 || icons[0] == IntPtr.Zero)
            return null;

        try
        {
            using var icon = Icon.FromHandle(icons[0]);
            using var bitmap = icon.ToBitmap();
            using var stream = new MemoryStream();
            bitmap.Save(stream, ImageFormat.Png);
            return stream.Length > 0 ? stream.ToArray() : null;
        }
        catch
        {
            return null;
        }
        finally
        {
            User32Icon.DestroyIcon(icons[0]);
        }
    }
}

internal static class Shell32IconLibrary
{
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern uint ExtractIconExW(
        string szFileName,
        int nIconIndex,
        IntPtr phiconLarge,
        IntPtr phiconSmall,
        uint nIcons);
}
