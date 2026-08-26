using System.Drawing;
using System.Drawing.Imaging;
using System.Security.Cryptography;

namespace CedroModernDock.Infrastructure.Windows.Native;

/// <summary>
/// Stores a window-specific icon under an application identity rather than an
/// executable path. This prevents distinct Edge/Chromium web apps that share
/// one browser executable from collapsing to the generic browser icon after
/// they are pinned.
/// </summary>
public static class WindowIdentityIconCache
{
    private static readonly string CacheDir = GetCacheDirectory();

    public static string? GetCachedIconPath(string? identity)
    {
        if (string.IsNullOrWhiteSpace(identity))
            return null;
        try
        {
            byte[] hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(identity));
            string name = $"identity_v1_{Convert.ToHexString(hash).ToLowerInvariant()}.png";
            return Path.Combine(CacheDir, name);
        }
        catch
        {
            return null;
        }
    }

    public static string? CaptureWindowIcon(string identity, IntPtr hwnd)
    {
        try
        {
            if (hwnd == IntPtr.Zero)
                return null;
            string? destination = GetCachedIconPath(identity);
            if (destination == null)
                return null;
            if (File.Exists(destination) && new FileInfo(destination).Length > 0)
                return destination;

            IntPtr hIcon = User32.SendMessage(
                hwnd, Win32Constants.WM_GETICON, Win32Constants.ICON_BIG, IntPtr.Zero);
            if (hIcon == IntPtr.Zero)
                hIcon = User32.GetClassLongPtr(hwnd, Win32Constants.GCLP_HICON);
            if (hIcon == IntPtr.Zero)
                hIcon = User32.SendMessage(
                    hwnd, Win32Constants.WM_GETICON, Win32Constants.ICON_SMALL2, IntPtr.Zero);
            if (hIcon == IntPtr.Zero)
                return null;

            // Window/class icons are owned by the source window. Do not destroy
            // the HICON; only dispose our managed wrappers/copy.
            using var icon = Icon.FromHandle(hIcon);
            using var bitmap = icon.ToBitmap();
            bitmap.Save(destination, ImageFormat.Png);
            return File.Exists(destination) && new FileInfo(destination).Length > 0
                ? destination : null;
        }
        catch
        {
            return null;
        }
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
