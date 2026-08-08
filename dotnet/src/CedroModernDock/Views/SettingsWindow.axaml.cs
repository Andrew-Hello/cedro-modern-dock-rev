using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using CedroModernDock.Core.Application;
using CedroModernDock.Core.Models;
using CedroModernDock.ViewModels;

namespace CedroModernDock.Views;

public partial class SettingsWindow : Window
{
    private static SettingsWindow? _instance;

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
        // Only one settings window at a time: focus the existing one.
        if (_instance != null)
        {
            _instance.WindowState = WindowState.Normal;
            _instance.Activate();
            return;
        }

        var vm = new SettingsViewModel(appServices, dockRefreshAction, positioningModeChangeAction);
        var window = new SettingsWindow
        {
            DataContext = vm, _vm = vm,
            _appServices = appServices, _dockRefreshAction = dockRefreshAction
        };
        _instance = window;
        window.Closed += (_, _) => _instance = null;
        vm.Initialize();
        window.Show(owner);
    }

    private SettingsViewModel Vm => _vm ??= (DataContext as SettingsViewModel)!;

    private void OnClosed(object? sender, EventArgs e) => Vm?.Shutdown();

    private async void OnAddProgram(object? sender, RoutedEventArgs e) => await Vm?.AddProgramAsync(this)!;
    private async void OnAddFolder(object? sender, RoutedEventArgs e) => await Vm?.AddFolderAsync(this)!;

    private async void OnAddModule(object? sender, RoutedEventArgs e)
    {
        if (_appServices != null && _dockRefreshAction != null)
        {
            await AddWindowsModulesWindow.Open(_appServices, _dockRefreshAction, this);
            // Refresh the dock-items list after the modal closes: adding a
            // module must show up immediately, not only after reopening.
            Vm?.RefreshItemLabels();
        }
    }

    private void OnRemove(object? sender, RoutedEventArgs e) => Vm?.RemoveSelected();
    private void OnMoveUp(object? sender, RoutedEventArgs e) => Vm?.MoveItemUp();
    private void OnMoveDown(object? sender, RoutedEventArgs e) => Vm?.MoveItemDown();
    private void OnAcknowledgements(object? sender, RoutedEventArgs e) => AcknowledgementsWindow.Open(this);

    private void OnPresetColorPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        if (sender is Border { DataContext: ISolidColorBrush brush })
            Vm.DockColor = brush.Color;
    }

    private void OnCustomColor(object? sender, RoutedEventArgs e)
    {
        var vm = Vm;
        var dialog = new Window
        {
            Title = vm.BgColorTitle,
            Width = 340,
            Height = 420,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = new Avalonia.Media.SolidColorBrush(Color.Parse("#1E1E1E")),
            Content = new ColorView
            {
                Color = vm.DockColor,
                Margin = new Thickness(16),
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch
            }
        };
        dialog.Closed += (_, _) =>
        {
            if (dialog.Content is ColorView picker)
                vm.DockColor = picker.Color;
        };
        dialog.ShowDialog(this);
    }
}
