using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Media;
using CedroModernDock.Core.Application;
using CedroModernDock.Core.Models;

namespace CedroModernDock.ViewModels;

/// <summary>
/// Main dock ViewModel. Holds the dock items collection, appearance settings,
/// and the running-app indicator watcher. Direct port of DockController's
/// state and update logic, expressed as MVVM bindings.
/// </summary>
public partial class MainWindowViewModel : ViewModelBase
{
    private readonly AppServices? _appServices;
    private CancellationTokenSource? _indicatorCts;

    private int _iconsSize = 48;
    private int _spacing = 5;
    private int _borderRounding = 12;
    private IBrush _dockBackground = new SolidColorBrush(Color.FromArgb(158, 0, 0, 0));
    private string _statusText = "";

    public ObservableCollection<DockItemViewModel> Items { get; } = new();

    public int IconsSize { get => _iconsSize; set => SetProperty(ref _iconsSize, value); }
    public int Spacing { get => _spacing; set => SetProperty(ref _spacing, value); }
    public int BorderRounding
    {
        get => _borderRounding;
        set
        {
            if (SetProperty(ref _borderRounding, value))
                OnPropertyChanged(nameof(DockCornerRadius));
        }
    }
    public CornerRadius DockCornerRadius => new CornerRadius(BorderRounding);
    public IBrush DockBackground { get => _dockBackground; set => SetProperty(ref _dockBackground, value); }
    public string StatusText { get => _statusText; set => SetProperty(ref _statusText, value); }

    public ICommand LaunchCommand { get; }

    /// <summary>Set by MainWindow — opens the settings window with this window as owner.</summary>
    public Action? OpenSettingsAction { get; set; }

    /// <summary>Set by MainWindow — re-anchors the dock after dock content/settings change.</summary>
    public Action? RepositionAction { get; set; }

    /// <summary>Set by MainWindow — dismisses the window-preview popup (dock refresh).</summary>
    public Action? PreviewDismissAction { get; set; }

    public MainWindowViewModel()
    {
        LaunchCommand = new RelayCommand(_ => { });
    }

    public MainWindowViewModel(AppServices appServices)
    {
        _appServices = appServices;
        LaunchCommand = new RelayCommand(param => ExecuteItem(param));
        _appServices.LocalizationService.AddListener(UpdateDockUI);
    }

    public void Initialize()
    {
        if (_appServices == null) return;
        ApplyAppearance();
        UpdateDockUI();
        StartIndicatorWatcher();
    }

    public void UpdateDockUI()
    {
        if (_appServices == null) return;
        Items.Clear();
        var dock = _appServices.DockService.GetDock();
        var loc = _appServices.LocalizationService;

        foreach (var item in dock.Items)
        {
            var vm = CreateItemViewModel(item, loc);
            if (vm != null)
            {
                vm.IconSize = _appServices.AppearanceService.GetIconsSize();
                Items.Add(vm);
            }
        }
        ApplyAppearance();
        RepositionAction?.Invoke();
        PreviewDismissAction?.Invoke();
    }
    // --- continued below ---

    private DockItemViewModel? CreateItemViewModel(DockItem item, LocalizationService loc)
    {
        string label = loc.DockItemLabel(item);

        if (item is DockSettingsItemModel)
        {
            var icon = IconLoader.LoadFromAsset(IconLoader.MapResourcePath(item.Path));
            return new DockItemViewModel(item, label, LaunchCommand) { Icon = icon };
        }

        if (item is DockWindowsModuleItemModel)
        {
            var icon = IconLoader.LoadFromAsset(IconLoader.MapResourcePath(item.Path));
            return new DockItemViewModel(item, label, LaunchCommand) { Icon = icon };
        }

        if (item is DockProgramItemModel programItem)
        {
            string? iconPath = _appServices!.IconGateway.ResolveProgramIcon(programItem.ExecutablePath);
            var icon = IconLoader.LoadFromFile(iconPath);
            var itemVm = new DockItemViewModel(item, label, LaunchCommand,
                showIndicator: true, executablePath: programItem.ExecutablePath) { Icon = icon };

            // The icon may not be cached yet (first run with a new item). Extract it in the
            // background, then push the resulting bitmap into the ViewModel so the dock
            // updates without requiring a restart.
            if (icon == null)
            {
                string exe = programItem.ExecutablePath;
                _ = Task.Run(() =>
                {
                    _appServices.IconGateway.CacheProgramIcon(exe);
                    string? cached = _appServices.IconGateway.ResolveProgramIcon(exe);
                    var loaded = IconLoader.LoadFromFile(cached);
                    if (loaded != null)
                        Avalonia.Threading.Dispatcher.UIThread.Post(() => itemVm.Icon = loaded);
                });
            }

            return itemVm;
        }

        if (item is DockFolderItemModel folderItem)
        {
            string? iconPath = _appServices!.IconGateway.ResolveFolderIcon(folderItem.FolderPath);
            var icon = IconLoader.LoadFromFile(iconPath);
            if (icon == null)
            {
                _ = Task.Run(() => _appServices.IconGateway.CacheFolderIcon(folderItem.FolderPath));
                icon = IconLoader.LoadFromAsset("Assets/icons/folder.png");
            }
            return new DockItemViewModel(item, label, LaunchCommand) { Icon = icon };
        }

        return null;
    }

    private void ApplyAppearance()
    {
        if (_appServices == null) return;
        var appearance = _appServices.AppearanceService;

        IconsSize = appearance.GetIconsSize();
        Spacing = appearance.GetSpacingBetweenIcons();
        BorderRounding = appearance.GetDockBorderRounding();

        string colorRgb = appearance.GetDockColorRGB();
        double transparency = appearance.GetDockTransparencyPercentage() / 100.0;
        byte alpha = (byte)(transparency * 255);
        var parts = colorRgb.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        byte r = parts.Length > 0 && byte.TryParse(parts[0], out var rv) ? rv : (byte)0;
        byte g = parts.Length > 1 && byte.TryParse(parts[1], out var gv) ? gv : (byte)0;
        byte b = parts.Length > 2 && byte.TryParse(parts[2], out var bv) ? bv : (byte)0;
        DockBackground = new SolidColorBrush(Color.FromArgb(alpha, r, g, b));
    }

    private void ExecuteItem(object? param)
    {
        if (param is DockItemViewModel itemVm && _appServices != null)
        {
            _appServices.ItemActionService.Execute(itemVm.Item, () => OpenSettingsAction?.Invoke());
        }
    }

    private void StartIndicatorWatcher()
    {
        _indicatorCts?.Cancel();
        _indicatorCts = new CancellationTokenSource();
        var token = _indicatorCts.Token;

        Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    RefreshIndicators();
                    await Task.Delay(1200, token);
                }
                catch (OperationCanceledException) { break; }
                catch { /* keep watching */ }
            }
        }, token);
    }

    private void RefreshIndicators()
    {
        if (_appServices == null) return;
        var programItems = Items.Where(i => i.ShowIndicator && i.ExecutablePath != null).ToList();
        foreach (var item in programItems)
        {
            bool isOpen = _appServices.WindowPreviewService.HasOpenWindows(item.ExecutablePath);
            Avalonia.Threading.Dispatcher.UIThread.Post(() => item.IsRunning = isOpen);
        }
    }

    public void Shutdown()
    {
        if (_appServices != null)
            _appServices.LocalizationService.RemoveListener(UpdateDockUI);
        _indicatorCts?.Cancel();
        _indicatorCts?.Dispose();
        _indicatorCts = null;
    }
}
