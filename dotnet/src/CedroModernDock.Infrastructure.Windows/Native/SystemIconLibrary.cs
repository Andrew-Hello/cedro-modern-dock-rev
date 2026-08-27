using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace CedroModernDock.Infrastructure.Windows.Native;

/// <summary>
/// Reads icon resources from Windows icon libraries. The default source is
/// %SystemRoot%\System32\SHELL32.dll, which contains the familiar built-in
/// Explorer/system icons. Preview icons can be extracted cheaply at a smaller
/// size while the selected icon is re-extracted at high resolution and stored
/// as portable PNG data in the normal custom-icon configuration field.
/// </summary>
public static class SystemIconLibrary
{
    public static string DefaultShell32Path
    {
        get
        {
            string systemDirectory = Environment.GetFolderPath(Environment.SpecialFolder.System);
            if (string.IsNullOrWhiteSpace(systemDirectory))
            {
                string root = Environment.GetEnvironmentVariable("SystemRoot") ?? @"C:\Windows";
                systemDirectory = Path.Combine(root, "System32");
            }
            return Path.Combine(systemDirectory, "SHELL32.dll");
        }
    }

    /// <summary>Returns the number of icon groups in the supplied library.</summary>
    public static int GetIconCount(string? libraryPath = null)
    {
        string path = libraryPath ?? DefaultShell32Path;
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
    /// PNG bytes. Callers own the returned managed bytes only; HICON ownership
    /// is fully released inside this method.
    /// </summary>
    public static byte[]? ExtractPngBytes(string libraryPath, int iconIndex, int size)
    {
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

    public static string? ExtractPngBase64(string libraryPath, int iconIndex, int size = 256)
    {
        byte[]? bytes = ExtractPngBytes(libraryPath, iconIndex, size);
        return bytes is { Length: > 0 } ? Convert.ToBase64String(bytes) : null;
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
