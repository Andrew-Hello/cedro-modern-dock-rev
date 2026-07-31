using Avalonia.Controls;
using Avalonia.Interactivity;
using CedroModernDock.Core.Application;
using CedroModernDock.Core.Models;
using CedroModernDock.ViewModels;

namespace CedroModernDock.Views;

public partial class SettingsWindow : Window
{
    private SettingsViewModel? _vm;
    private AppServices? _appServices;
    private Action? _dockRefreshAction;

    public SettingsWindow()
    {
        InitializeComponent();
    }

    public static void Open(AppServices appServices, Window owner,
        Action dockRefreshAction, Action<DockPositioningMode> positioningModeChangeAction)
    {
        var vm = new SettingsViewModel(appServices, dockRefreshAction, positioningModeChangeAction);
        var window = new SettingsWindow
        {
            DataContext = vm, _vm = vm,
            _appServices = appServices, _dockRefreshAction = dockRefreshAction
        };
        vm.Initialize();
        window.Show(owner);
    }

    private SettingsViewModel Vm => _vm ??= (DataContext as SettingsViewModel)!;

    private void OnClosed(object? sender, EventArgs e) => Vm?.Shutdown();

    private async void OnAddProgram(object? sender, RoutedEventArgs e) => await Vm?.AddProgramAsync(this)!;
    private async void OnAddFolder(object? sender, RoutedEventArgs e) => await Vm?.AddFolderAsync(this)!;

    private void OnAddModule(object? sender, RoutedEventArgs e)
    {
        if (_appServices != null && _dockRefreshAction != null)
            AddWindowsModulesWindow.Open(_appServices, _dockRefreshAction, this);
    }

    private void OnRemove(object? sender, RoutedEventArgs e) => Vm?.RemoveSelected();
    private void OnMoveUp(object? sender, RoutedEventArgs e) => Vm?.MoveItemUp();
    private void OnMoveDown(object? sender, RoutedEventArgs e) => Vm?.MoveItemDown();
    private void OnAcknowledgements(object? sender, RoutedEventArgs e) => AcknowledgementsWindow.Open(this);
}
