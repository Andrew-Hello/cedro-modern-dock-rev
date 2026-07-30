namespace CedroModernDock.Infrastructure.Windows.Native;

using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Security.Cryptography;

/// <summary>
/// Direct port of WindowsIconHandler.java. Extracts high-resolution (256x256)
/// icons from executables and folders using the Windows Shell API, and caches
/// them as PNG files in %APPDATA%\CedroModernDock\iconsCache.
/// Uses System.Drawing.Icon.FromHandle() for HICON→Bitmap conversion.
/// </summary>
public static class WindowsIconExtractor
{
    private const int IconSize = 256;
    private const string IidImageList = "46EB5926-582E-4017-9FDF-E8998DAA0950";
    private static readonly string CacheDir = GetCacheDirectory();

    public static string? GetCachedIconPath(string exePath) => GetCachedPath(exePath, "program");

    public static string? GetCachedFolderIconPath(string folderPath) => GetCachedPath(folderPath, "folder_v3");

    public static string? ExtractAndCacheIcon(string exePath)
    {
        try
        {
            string? cachedIconPath = GetCachedIconPath(exePath);
            if (cachedIconPath == null) return null;
            if (File.Exists(cachedIconPath)) return cachedIconPath;

            if (!ExtractIconWithShellApi(exePath, cachedIconPath))
            {
                try { File.Delete(cachedIconPath); } catch { }
                return null;
            }
            return cachedIconPath;
        }
        catch (Exception e)
        {
            System.Diagnostics.Debug.WriteLine($"ExtractAndCacheIcon error: {e.Message}");
            return null;
        }
    }

    public static string? ExtractAndCacheFolderIcon(string folderPath)
    {
        try
        {
            string? cachedIconPath = GetCachedFolderIconPath(folderPath);
            if (cachedIconPath == null) return null;
            if (File.Exists(cachedIconPath)) return cachedIconPath;
            if (!Directory.Exists(folderPath)) return null;

            using var image = ExtractFolderIconWithShellApi(folderPath);
            if (image == null) return null;

            image.Save(cachedIconPath, ImageFormat.Png);
            return File.Exists(cachedIconPath) && new FileInfo(cachedIconPath).Length > 0
                ? cachedIconPath : null;
        }
        catch (Exception e)
        {
            System.Diagnostics.Debug.WriteLine($"ExtractAndCacheFolderIcon error: {e.Message}");
            return null;
        }
    }
    // --- private methods inserted below ---

    private static bool ExtractIconWithShellApi(string exePath, string outputPath)
    {
        var icons = new IntPtr[1];
        var iconIds = new int[1];

        int extracted = User32Icon.PrivateExtractIcons(
            exePath, 0, IconSize, IconSize, icons, iconIds, 1, 0);

        if (extracted <= 0 || icons[0] == IntPtr.Zero)
            return false;

        try
        {
            // Icon.FromHandle converts HICON → Bitmap (no manual GDI pixel loop needed).
            using var icon = Icon.FromHandle(icons[0]);
            using var bitmap = icon.ToBitmap();
            bitmap.Save(outputPath, ImageFormat.Png);
            return File.Exists(outputPath) && new FileInfo(outputPath).Length > 0;
        }
        catch
        {
            return false;
        }
        finally
        {
            User32Icon.DestroyIcon(icons[0]);
        }
    }

    private static Bitmap? ExtractFolderIconWithShellApi(string folderPath)
    {
        var shFileInfo = new SHFILEINFO();
        IntPtr result = Shell32.SHGetFileInfoW(
            folderPath, Shell32.FILE_ATTRIBUTE_DIRECTORY, ref shFileInfo,
            System.Runtime.InteropServices.Marshal.SizeOf<SHFILEINFO>(),
            Shell32.SHGFI_SYSICONINDEX);

        if (result == IntPtr.Zero)
            return null;

        var iid = new Guid(IidImageList);
        int hr = Shell32.SHGetImageList(Shell32.SHIL_JUMBO, in iid, out IntPtr imageList);
        if (hr != 0 || imageList == IntPtr.Zero)
            return null;

        IntPtr hIcon = Comctl32Icon.ImageList_GetIcon(
            imageList, shFileInfo.iIcon, Shell32.ILD_TRANSPARENT);
        if (hIcon == IntPtr.Zero)
            return null;

        try
        {
            var icon = Icon.FromHandle(hIcon);
            return icon.ToBitmap();
        }
        catch
        {
            return null;
        }
        finally
        {
            User32Icon.DestroyIcon(hIcon);
        }
    }

    private static string? GetCachedPath(string inputPath, string kind)
    {
        try
        {
            string fileName = $"{kind}_{GetHashedFileName(inputPath)}.png";
            return Path.Combine(CacheDir, fileName);
        }
        catch (Exception e)
        {
            System.Diagnostics.Debug.WriteLine($"GetCachedPath failed: {e.Message}");
            return null;
        }
    }

    private static string GetHashedFileName(string input)
    {
        byte[] hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string GetCacheDirectory()
    {
        string? appData = Environment.GetEnvironmentVariable("APPDATA");
        if (string.IsNullOrEmpty(appData))
            appData = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        string cacheDir = Path.Combine(appData, "CedroModernDock", "iconsCache");
        Directory.CreateDirectory(cacheDir);
        return cacheDir;
    }
}
