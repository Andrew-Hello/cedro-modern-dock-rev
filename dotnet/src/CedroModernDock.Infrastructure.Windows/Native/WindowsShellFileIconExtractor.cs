using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace CedroModernDock.Infrastructure.Windows.Native;

/// <summary>
/// Extracts the Windows Shell-associated icon for non-EXE launchable files such
/// as .bat, .cmd and .vbs. The result is written into the same deterministic
/// program-icon cache used by WindowsIconExtractor.
/// </summary>
public static class WindowsShellFileIconExtractor
{
    private static readonly Guid IidImageList =
        new("46EB5926-582E-4017-9FDF-E8998DAA0950");

    public static string? ExtractAndCacheIcon(string filePath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                return null;

            string? outputPath = WindowsIconExtractor.GetCachedIconPath(filePath);
            if (string.IsNullOrWhiteSpace(outputPath))
                return null;
            if (File.Exists(outputPath))
                return outputPath;

            var fileInfo = new SHFILEINFO();
            IntPtr result = Shell32.SHGetFileInfoW(
                filePath,
                0,
                ref fileInfo,
                Marshal.SizeOf<SHFILEINFO>(),
                Shell32.SHGFI_SYSICONINDEX);

            if (result == IntPtr.Zero)
                return null;

            int hr = Shell32.SHGetImageList(
                Shell32.SHIL_JUMBO,
                in IidImageList,
                out IntPtr imageList);
            if (hr != 0 || imageList == IntPtr.Zero)
                return null;

            IntPtr hIcon = Comctl32Icon.ImageList_GetIcon(
                imageList,
                fileInfo.iIcon,
                Shell32.ILD_TRANSPARENT);
            if (hIcon == IntPtr.Zero)
                return null;

            try
            {
                using var icon = Icon.FromHandle(hIcon);
                using var bitmap = icon.ToBitmap();
                bitmap.Save(outputPath, ImageFormat.Png);
                return File.Exists(outputPath) && new FileInfo(outputPath).Length > 0
                    ? outputPath
                    : null;
            }
            finally
            {
                User32Icon.DestroyIcon(hIcon);
            }
        }
        catch (Exception e)
        {
            System.Diagnostics.Debug.WriteLine(
                $"WindowsShellFileIconExtractor error: {e.Message}");
            return null;
        }
    }
}
