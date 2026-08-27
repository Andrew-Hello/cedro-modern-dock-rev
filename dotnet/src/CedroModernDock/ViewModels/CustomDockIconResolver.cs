using Avalonia.Media.Imaging;
using CedroModernDock.Core.Models;
using CedroModernDock.Infrastructure.Windows.Native;

namespace CedroModernDock.ViewModels;

/// <summary>
/// Resolves the two supported per-item icon override forms. Windows resource
/// references are extracted directly in memory at runtime; imported image/ICO
/// overrides continue to use the existing normalized PNG cache.
/// </summary>
public static class CustomDockIconResolver
{
    public static bool HasOverride(DockItem item)
        => HasSystemOverride(item) || !string.IsNullOrWhiteSpace(item.CustomIconPngBase64);

    public static bool HasSystemOverride(DockItem item)
        => !string.IsNullOrWhiteSpace(item.CustomSystemIconSource)
           && item.CustomSystemIconIndex is >= 0;

    public static Bitmap? Resolve(DockItem item, int systemIconSize = 256)
    {
        // Source + index is the preferred representation for Windows system
        // libraries. No PNG is written to disk/config for this path.
        if (HasSystemOverride(item))
        {
            byte[]? bytes = SystemIconLibrary.ExtractPngBytes(
                item.CustomSystemIconSource!, item.CustomSystemIconIndex!.Value,
                systemIconSize);
            if (bytes is { Length: > 0 })
            {
                try
                {
                    using var stream = new MemoryStream(bytes, writable: false);
                    return new Bitmap(stream);
                }
                catch
                {
                    // A missing/changed Windows resource should not break Dock;
                    // fall through to legacy PNG or the automatic item icon.
                }
            }
        }

        string? path = CustomIconStore.EnsureCached(item.CustomIconPngBase64);
        return IconLoader.LoadFromFile(path);
    }
}
