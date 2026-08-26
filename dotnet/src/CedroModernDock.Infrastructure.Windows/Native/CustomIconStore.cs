using System.Drawing;
using System.Drawing.Imaging;
using System.Security.Cryptography;

namespace CedroModernDock.Infrastructure.Windows.Native;

/// <summary>
/// Imports user-selected icon files as normalized PNG data and materializes a
/// local cache under %APPDATA%\CedroModernDock\customIcons. The normalized PNG
/// bytes are stored in config.json as Base64 by the dock-item model, so exported
/// configurations remain self-contained when moved to another computer.
/// </summary>
public static class CustomIconStore
{
    private const long MaxSourceBytes = 20L * 1024L * 1024L;

    public static string CustomIconsDirectoryPath { get; } = CreateDirectory();

    public static readonly string[] SupportedPatterns =
    {
        "*.png", "*.ico", "*.jpg", "*.jpeg", "*.bmp", "*.gif", "*.tif", "*.tiff"
    };

    /// <summary>
    /// Reads a supported image/icon file, converts the first frame to PNG and
    /// returns the PNG bytes as Base64. A deterministic cached PNG is also
    /// written immediately for fast subsequent loading.
    /// </summary>
    public static string ImportAsPngBase64(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            throw new FileNotFoundException("Icon file was not found.", sourcePath);

        var info = new FileInfo(sourcePath);
        if (info.Length <= 0)
            throw new InvalidDataException("The selected icon file is empty.");
        if (info.Length > MaxSourceBytes)
            throw new InvalidDataException("The selected icon file is larger than 20 MB.");

        string extension = Path.GetExtension(sourcePath).ToLowerInvariant();
        if (!SupportedPatterns.Any(p => extension == p[1..].ToLowerInvariant()))
            throw new InvalidDataException("Unsupported icon format.");

        byte[] pngBytes;
        using (var stream = new MemoryStream())
        {
            if (extension == ".ico")
            {
                using var icon = new Icon(sourcePath);
                using var bitmap = icon.ToBitmap();
                bitmap.Save(stream, ImageFormat.Png);
            }
            else
            {
                using var image = Image.FromFile(sourcePath);
                image.Save(stream, ImageFormat.Png);
            }
            pngBytes = stream.ToArray();
        }

        if (pngBytes.Length == 0)
            throw new InvalidDataException("The selected icon could not be converted to PNG.");

        string data = Convert.ToBase64String(pngBytes);
        EnsureCached(data);
        return data;
    }

    /// <summary>
    /// Returns a local PNG path for a persisted Base64 override, creating the
    /// cache file when this configuration has just been imported on another PC.
    /// </summary>
    public static string? EnsureCached(string? pngBase64)
    {
        if (string.IsNullOrWhiteSpace(pngBase64))
            return null;

        try
        {
            byte[] bytes = Convert.FromBase64String(pngBase64);
            if (bytes.Length == 0)
                return null;

            string path = GetCachePath(bytes);
            if (!File.Exists(path) || new FileInfo(path).Length != bytes.Length)
            {
                Directory.CreateDirectory(CustomIconsDirectoryPath);
                File.WriteAllBytes(path, bytes);
            }
            return path;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Deletes only the managed cache copy. The caller clears config separately.</summary>
    public static void DeleteCached(string? pngBase64)
    {
        if (string.IsNullOrWhiteSpace(pngBase64))
            return;

        try
        {
            byte[] bytes = Convert.FromBase64String(pngBase64);
            string path = GetCachePath(bytes);
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Resetting an override should never fail because a cache file is
            // missing/corrupt. Clearing the model remains the source of truth.
        }
    }

    private static string GetCachePath(byte[] pngBytes)
    {
        string hash = Convert.ToHexString(SHA256.HashData(pngBytes)).ToLowerInvariant();
        return Path.Combine(CustomIconsDirectoryPath, $"custom_{hash}.png");
    }

    private static string CreateDirectory()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrWhiteSpace(appData))
            appData = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string directory = Path.Combine(appData, "CedroModernDock", "customIcons");
        Directory.CreateDirectory(directory);
        return directory;
    }
}
