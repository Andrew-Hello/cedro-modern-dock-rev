using Avalonia.Controls;

namespace CedroModernDock.Views;

public partial class SettingsWindow
{
    /// <summary>
    /// Keep Settings within a deliberate desktop-tool footprint. The content
    /// itself has a fixed maximum width, so 4K desktops no longer stretch cards
    /// into a sparse full-screen canvas.
    /// </summary>
    private void ConfigureModernSettingsWindow()
    {
        Width = 1000;
        Height = 700;
        MinWidth = 860;
        MinHeight = 600;
        MaxWidth = 1180;
        MaxHeight = 900;
        CanResize = true;
        ShowInTaskbar = false;
        Topmost = false;
    }

    /// <summary>
    /// Settings is intentionally not topmost. It is shown without the Dock as a
    /// native owner, while dialogs launched from Settings use Settings as their
    /// modal owner and therefore appear above it naturally.
    /// </summary>
    private void NormalizeSettingsWindowZOrder()
    {
        Topmost = false;
    }
}
