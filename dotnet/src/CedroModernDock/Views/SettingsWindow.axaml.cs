using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using CedroModernDock.Core.Application;
using CedroModernDock.Core.Models;
using CedroModernDock.ViewModels;

namespace CedroModernDock.Views;

public partial class SettingsWindow : Window
{
    private static SettingsWindow? _instance;

    private const double DragThresholdPixels = 4;

    private SettingsViewModel? _vm;
    private AppServices? _appServices;
    private Action? _dockRefreshAction;

    private Point _pressPoint;
    private int _dragSourceIndex = -1;
    private bool _dragInProgress;

    public SettingsWindow()
    {
        InitializeComponent();
        InitializeWindowBehaviorSettingsHooks();
        ItemsList.AddHandler(
            InputElement.PointerPressedEvent,
            OnItemsPointerPressed,
            RoutingStrategies.Bubble,
            handledEventsToo: true);
    }

    public static void Open(AppServices appServices, Window owner,
        Action dockRefreshAction, Action<DockPositioningMode> positioningModeChangeAction)
    {
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

        // Do not make Settings an owned child of the always-on-top Dock. Native
        // Windows owned-window z-order can otherwise promote Settings into the
        // topmost band and make it cover dialogs opened from Settings itself.
        window.Show();
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
            Vm?.RefreshItemLabels();
        }
    }

    private void OnRemove(object? sender, RoutedEventArgs e) => Vm?.RemoveSelected();
    private void OnMoveUp(object? sender, RoutedEventArgs e) => Vm?.MoveItemUp();
    private void OnMoveDown(object? sender, RoutedEventArgs e) => Vm?.MoveItemDown();

    private void OnItemsPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        if (e.Source is Visual source && source.FindAncestorOfType<ListBoxItem>() is { } container)
        {
            _dragSourceIndex = ItemsList.IndexFromContainer(container);
            if (_dragSourceIndex < 0) return;
            _pressPoint = e.GetPosition(ItemsList);
            _dragInProgress = false;
        }
    }

    private async void OnItemsPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_dragSourceIndex < 0 || _dragInProgress) return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        var current = e.GetPosition(ItemsList);
        double dx = current.X - _pressPoint.X;
        double dy = current.Y - _pressPoint.Y;
        if (Math.Abs(dx) < DragThresholdPixels && Math.Abs(dy) < DragThresholdPixels)
            return;

        _dragInProgress = true;
        var data = new DataTransfer();
        data.Add(DataTransferItem.CreateText(_dragSourceIndex.ToString()));
        await DragDrop.DoDragDropAsync(e, data, DragDropEffects.Move);
        _dragInProgress = false;
        _dragSourceIndex = -1;
        HideDropIndicator();
    }

    private void OnItemsPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _dragSourceIndex = -1;
        _dragInProgress = false;
    }

    private void OnItemsPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        _dragSourceIndex = -1;
        _dragInProgress = false;
        HideDropIndicator();
    }

    private void OnItemsDragOver(object? sender, DragEventArgs e)
    {
        if (!e.DataTransfer.Contains(DataFormat.Text))
        {
            e.DragEffects = DragDropEffects.None;
            return;
        }
        e.DragEffects = DragDropEffects.Move;
        ShowDropIndicatorAt(e.GetPosition(ItemsList));
        e.Handled = true;
    }

    private void OnItemsDragLeave(object? sender, DragEventArgs e) => HideDropIndicator();

    private void OnItemsDrop(object? sender, DragEventArgs e)
    {
        HideDropIndicator();
        string? sourceText = e.DataTransfer.TryGetText();
        if (sourceText is null || !int.TryParse(sourceText, out int fromIndex)) return;

        var (gapIndex, _) = ResolveDropGap(e.GetPosition(ItemsList));
        Vm?.MoveItem(fromIndex, gapIndex);
        e.DragEffects = DragDropEffects.Move;
        e.Handled = true;
    }

    private (int GapIndex, double GapY) ResolveDropGap(Point position)
    {
        int count = Vm?.ItemEntries.Count ?? 0;
        if (count == 0) return (0, 0);

        var rows = new List<(int Index, double Top, double Height)>();
        foreach (var container in ItemsList.GetRealizedContainers())
        {
            if (container is not Control c) continue;
            int index = ItemsList.IndexFromContainer(c);
            if (index < 0) continue;
            if (c.TranslatePoint(new Point(0, 0), ItemsList) is not Point topLeft) continue;
            double height = c.Bounds.Height;
            if (height <= 0) continue;
            rows.Add((index, topLeft.Y, height));
        }
        if (rows.Count == 0) return (0, 0);
        rows.Sort((a, b) => a.Index.CompareTo(b.Index));

        var first = rows[0];
        if (position.Y < first.Top)
            return (first.Index, first.Top);

        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            double bottom = row.Top + row.Height;
            if (position.Y <= bottom)
            {
                double mid = row.Top + row.Height / 2;
                return position.Y <= mid
                    ? (row.Index, row.Top)
                    : (row.Index + 1, bottom);
            }
        }

        var last = rows[^1];
        return (Math.Min(last.Index + 1, count), last.Top + last.Height);
    }

    private void ShowDropIndicatorAt(Point position)
    {
        if (DropIndicator is null) return;
        var (_, gapY) = ResolveDropGap(position);
        DropIndicator.Margin = new Thickness(0, gapY, 0, 0);
        DropIndicator.IsVisible = true;
    }

    private void HideDropIndicator()
    {
        if (DropIndicator is null) return;
        DropIndicator.IsVisible = false;
    }

    private void OnAcknowledgements(object? sender, RoutedEventArgs e)
    {
        if (_appServices != null)
            AcknowledgementsWindow.Open(this, _appServices.LocalizationService);
    }

    private void OnPresetColorPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border { DataContext: ISolidColorBrush brush })
            Vm.DockColor = brush.Color;
    }

    private void OnTintPresetColorPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border { DataContext: ISolidColorBrush brush })
            Vm.TintColor = brush.Color;
    }

    private void OnCustomTintColor(object? sender, RoutedEventArgs e)
    {
        var vm = Vm;
        var dialog = new Window
        {
            Title = vm.TintIconsTitle,
            Width = 340,
            Height = 420,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = new SolidColorBrush(Color.Parse("#1E1E1E")),
            Content = new ColorView
            {
                Color = vm.TintColor,
                Margin = new Thickness(16),
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch
            }
        };
        dialog.Closed += (_, _) =>
        {
            if (dialog.Content is ColorView picker)
                vm.TintColor = picker.Color;
        };
        dialog.ShowDialog(this);
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
            Background = new SolidColorBrush(Color.Parse("#1E1E1E")),
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
