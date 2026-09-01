using Avalonia.Controls;
using CedroModernDock.ViewModels;

namespace CedroModernDock.Views;

public partial class SettingsWindow
{
    /// <summary>
    /// Settings is now declared in its final form in XAML. No page is mutated or
    /// reflowed when the window opens; this keeps layout deterministic at every
    /// DPI and window size.
    /// </summary>
    private void InitializeWindowBehaviorSettingsHooks()
    {
        ConfigureModernSettingsWindow();
        ItemsList.SelectionChanged += (_, _) => OnCustomIconSelectionChanged();

        Opened += (_, _) =>
        {
            NormalizeSettingsWindowZOrder();
            if (DataContext is SettingsViewModel vm)
            {
                vm.ApplyCustomIconPreviews();
                vm.NotifyCustomIconSelectionChanged();
            }
        };
    }
}
