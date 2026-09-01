using Avalonia.Controls;
using Avalonia.Interactivity;
using CedroModernDock.Infrastructure.Windows.Native;
using CedroModernDock.ViewModels;

namespace CedroModernDock.Views;

public partial class SettingsWindow
{
    private async void OnAddScript(object? sender, RoutedEventArgs e)
        => await Vm.AddScriptAsync(this);

    private async void OnChooseCustomIcon(object? sender, RoutedEventArgs e)
        => await Vm.ChooseCustomIconAsync(this);

    private async void OnChooseSystemIcon(object? sender, RoutedEventArgs e)
    {
        var vm = Vm;
        if (!vm.CanCustomizeIcon)
            return;

        var picker = new SystemIconPickerWindow(
            vm.SystemIconPickerTitle,
            vm.SystemIconPickerSubtitle,
            vm.SystemIconPickerLibraryLabel,
            vm.SystemIconPickerLoading,
            vm.SystemIconPickerLoaded,
            vm.SystemIconPickerFailed,
            vm.SystemIconPickerNoLibraries,
            vm.SystemIconPickerCancel,
            vm.SystemIconCategoryName);

        SystemIconSelection? selection =
            await picker.ShowDialog<SystemIconSelection?>(this);
        if (selection != null)
            vm.ApplySystemIconOverride(selection);
    }

    private void OnResetCustomIcon(object? sender, RoutedEventArgs e)
        => Vm.ResetCustomIcon();

    private void OnCustomIconSelectionChanged()
    {
        var vm = Vm;
        if (vm.SelectedItemIndex != ItemsList.SelectedIndex)
            vm.SelectedItemIndex = ItemsList.SelectedIndex;
        vm.NotifyCustomIconSelectionChanged();
    }
}
