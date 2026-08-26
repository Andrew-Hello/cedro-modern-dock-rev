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
using CedroModernDock.Core.Domain;
using CedroModernDock.Core.Models;
using CedroModernDock.Infrastructure.Windows.Native;

namespace CedroModernDock.ViewModels;

/// <summary>
/// Main dock ViewModel. Holds pinned items, unpinned running apps, appearance
/// preferences and running-window state. Enhanced builds also support pinning a
/// live Windows app/PWA by its AppUserModelID.
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
    public ObservableCollection<RunningAppViewModel> RunningApps { get; } = new();

    private bool _hasRunningApps;
    public bool HasRunningApps
    {
        get => _hasRunningApps;
        private set
        {
            if (SetProperty(ref _hasRunningApps, value))
            {
                OnPropertyChanged(nameof(ShowHorizontalSeparator));
                OnPropertyChanged(nameof(ShowVerticalSeparator));
            }
        }
    }

    private bool _isVerticalDock;
    public bool IsVerticalDock
    {
        get => _isVerticalDock;
        set
        {
            if (SetProperty(ref _isVerticalDock, value))
            {
                OnPropertyChanged(nameof(DockOrientation));
                OnPropertyChanged(nameof(ShowHorizontalSeparator));
                OnPropertyChanged(nameof(ShowVerticalSeparator));
            }
        }
    }

    public Avalonia.Layout.Orientation DockOrientation
        => IsVerticalDock ? Avalonia.Layout.Orientation.Vertical : Avalonia.Layout.Orientation.Horizontal;

    public bool ShowHorizontalSeparator => HasRunningApps && !IsVerticalDock;
    public bool ShowVerticalSeparator => HasRunningApps && IsVerticalDock;

    public int IconsSize
    {
        get => _iconsSize;
        set => SetProperty(ref _iconsSize, value);
    }
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
    public ICommand PinRunningCommand { get; }

    public Action? OpenSettingsAction { get; set; }
    public Action? RepositionAction { get; set; }
    public Action? PreviewDismissAction { get; set; }

    public MainWindowViewModel()
    {
        LaunchCommand = new RelayCommand(_ => { });
        PinRunningCommand = new RelayCommand(_ => { });
    }

    public MainWindowViewModel(AppServices appServices)
    {
        _appServices = appServices;
        LaunchCommand = new RelayCommand(param => ExecuteItem(param));
        PinRunningCommand = new RelayCommand(param =>
        {
            if (param is RunningAppViewModel running)
                PinRunningApp(running);
        });
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
        IsVerticalDock = _appServices.AppearanceService.GetVerticalDock();

        var appearance = _appServices.AppearanceService;
        IconTinter.ActiveColor = appearance.GetTintIcons()
            ? ParseRgbColor(appearance.GetTintColorRGB())
            : null;

        Items.Clear();
        var dock = _appServices.DockService.GetDock();
        var loc = _appServices.LocalizationService;

        foreach (var item in dock.Items)
        {
            var vm = CreateItemViewModel(item, loc);
            if (vm != null)
            {
                vm.IconSize = appearance.GetIconsSize();
                ApplyInteractionSettings(vm);
                Items.Add(vm);
            }
        }

        foreach (var app in RunningApps)
        {
            app.PinToDockText = loc.Text("dock.runningApp.pin");
            app.PinCommand = PinRunningCommand;
            ApplyInteractionSettings(app);
        }

        ApplyAppearance();
        RepositionAction?.Invoke();
        PreviewDismissAction?.Invoke();
    }

    private DockItemViewModel? CreateItemViewModel(DockItem item, LocalizationService loc)
    {
        string label = loc.DockItemLabel(item);

        if (item is DockSettingsItemModel)
        {
            var icon = IconLoader.LoadFromAsset(IconLoader.MapResourcePath(item.Path));
            return new DockItemViewModel(item, label, LaunchCommand) { Icon = IconTinter.Apply(icon) };
        }

        if (item is DockWindowsModuleItemModel)
        {
            var icon = IconLoader.LoadFromAsset(IconLoader.MapResourcePath(item.Path));
            return new DockItemViewModel(item, label, LaunchCommand) { Icon = IconTinter.Apply(icon) };
        }

        if (item is DockProgramItemModel programItem)
        {
            Bitmap? icon = null;
            if (!string.IsNullOrWhiteSpace(programItem.IconCacheKey))
            {
                icon = IconLoader.LoadFromFile(
                    WindowIdentityIconCache.GetCachedIconPath(programItem.IconCacheKey));
            }

            icon ??= IconLoader.LoadFromFile(
                _appServices!.IconGateway.ResolveProgramIcon(programItem.ExecutablePath));

            var itemVm = new DockItemViewModel(
                item, label, LaunchCommand,
                showIndicator: true,
                executablePath: programItem.ExecutablePath,
                appUserModelId: programItem.AppUserModelId)
            {
                Icon = IconTinter.Apply(icon)
            };

            if (icon == null && !string.IsNullOrWhiteSpace(programItem.ExecutablePath))
            {
                string exe = programItem.ExecutablePath;
                _ = Task.Run(() =>
                {
                    _appServices.IconGateway.CacheProgramIcon(exe);
                    string? cached = _appServices.IconGateway.ResolveProgramIcon(exe);
                    var loaded = IconLoader.LoadFromFile(cached);
                    if (loaded == null)
                    {
                        WindowsIconExtractor.ExtractAndCacheAppxIcon(exe);
                        loaded = IconLoader.LoadFromFile(
                            _appServices.IconGateway.ResolveProgramIcon(exe));
                    }
                    if (loaded != null)
                        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                            itemVm.Icon = IconTinter.Apply(loaded));
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
            return new DockItemViewModel(item, label, LaunchCommand) { Icon = IconTinter.Apply(icon) };
        }

        return null;
    }

    private void ApplyInteractionSettings(DockItemViewModel item)
    {
        if (_appServices == null) return;
        var appearance = _appServices.AppearanceService;
        item.ShowRunningIndicator = appearance.GetShowRunningIndicators();
        item.EnableHoverMagnification = appearance.GetEnableHoverMagnification();
        item.ShowHoverLabel = appearance.GetShowHoverLabels();
    }

    private void ApplyInteractionSettings(RunningAppViewModel item)
    {
        if (_appServices == null) return;
        var appearance = _appServices.AppearanceService;
        item.ShowRunningIndicator = appearance.GetShowRunningIndicators();
        item.EnableHoverMagnification = appearance.GetEnableHoverMagnification();
        item.ShowHoverLabel = appearance.GetShowHoverLabels();
    }

    private void ApplyAppearance()
    {
        if (_appServices == null) return;
        var appearance = _appServices.AppearanceService;

        IconsSize = appearance.GetIconsSize();
        foreach (var item in Items)
        {
            item.IconSize = IconsSize;
            ApplyInteractionSettings(item);
        }
        foreach (var app in RunningApps)
        {
            app.IconSize = IconsSize;
            ApplyInteractionSettings(app);
        }

        Spacing = appearance.GetSpacingBetweenIcons();
        BorderRounding = appearance.GetDockBorderRounding();

        foreach (var app in RunningApps)
            app.Icon = IconTinter.Apply(app.OriginalIcon);

        string colorRgb = appearance.GetDockColorRGB();
        double transparency = appearance.GetDockTransparencyPercentage() / 100.0;
        byte alpha = (byte)(transparency * 255);
        var parts = colorRgb.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        byte r = parts.Length > 0 && byte.TryParse(parts[0], out var rv) ? rv : (byte)0;
        byte g = parts.Length > 1 && byte.TryParse(parts[1], out var gv) ? gv : (byte)0;
        byte b = parts.Length > 2 && byte.TryParse(parts[2], out var bv) ? bv : (byte)0;
        DockBackground = new SolidColorBrush(Color.FromArgb(alpha, r, g, b));
    }

    private static Color ParseRgbColor(string rgb)
    {
        var parts = rgb.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        byte r = parts.Length > 0 && byte.TryParse(parts[0], out var rv) ? rv : (byte)0;
        byte g = parts.Length > 1 && byte.TryParse(parts[1], out var gv) ? gv : (byte)0;
        byte b = parts.Length > 2 && byte.TryParse(parts[2], out var bv) ? bv : (byte)0;
        return Color.FromRgb(r, g, b);
    }

    private void ExecuteItem(object? param)
    {
        if (param is DockItemViewModel itemVm && _appServices != null)
        {
            bool launched = _appServices.ItemActionService.Execute(
                itemVm.Item, () => OpenSettingsAction?.Invoke());

            if (!launched && itemVm.Item is DockProgramItemModel programItem)
            {
                var loc = _appServices.LocalizationService;
                System.Windows.Forms.MessageBox.Show(
                    loc.Text("dialog.programNotFound.message",
                        programItem.Label,
                        programItem.LaunchTarget ?? programItem.ExecutablePath),
                    loc.Text("dialog.programNotFound.title"),
                    System.Windows.Forms.MessageBoxButtons.OK,
                    System.Windows.Forms.MessageBoxIcon.Warning);
            }
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
                    RefreshRunningApps();
                    await Task.Delay(1200, token);
                }
                catch (OperationCanceledException) { break; }
                catch { }
            }
        }, token);
    }

    private void RefreshIndicators()
    {
        if (_appServices == null) return;
        if (!_appServices.AppearanceService.GetShowRunningIndicators())
            return;

        var programItems = Items
            .Where(i => i.ShowIndicator &&
                        (!string.IsNullOrWhiteSpace(i.ExecutablePath) ||
                         !string.IsNullOrWhiteSpace(i.AppUserModelId)))
            .ToList();

        foreach (var item in programItems)
        {
            bool isOpen = _appServices.WindowPreviewService.HasOpenWindows(
                item.ExecutablePath, item.AppUserModelId);
            Avalonia.Threading.Dispatcher.UIThread.Post(() => item.IsRunning = isOpen);
        }
    }

    private readonly Dictionary<string, RunningAppViewModel> _runningAppsByIdentity =
        new(StringComparer.OrdinalIgnoreCase);

    private void RefreshRunningApps()
    {
        if (_appServices == null) return;

        if (!_appServices.AppearanceService.GetShowUnpinnedRunningApps())
        {
            if (RunningApps.Count > 0)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    RunningApps.Clear();
                    _runningAppsByIdentity.Clear();
                    HasRunningApps = false;
                    RepositionAction?.Invoke();
                    PreviewDismissAction?.Invoke();
                });
            }
            return;
        }

        List<DockProgramItemModel> pinnedPrograms = Items
            .Where(i => i.Item is DockProgramItemModel)
            .Select(i => (DockProgramItemModel)i.Item)
            .ToList();

        List<RunningWindowInfo> windows;
        try
        {
            windows = _appServices.WindowPreviewService.FindTaskbarWindows();
        }
        catch
        {
            return;
        }

        var desired = windows
            .GroupBy(GetRunningIdentity, StringComparer.OrdinalIgnoreCase)
            .Where(g => !IsPinned(g.First(), pinnedPrograms))
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (_appServices == null) return;

            bool changed = false;

            foreach (var identity in _runningAppsByIdentity.Keys.ToList())
            {
                if (!desired.TryGetValue(identity, out RunningWindowInfo? win) ||
                    IsPinned(win, pinnedPrograms))
                {
                    RunningApps.Remove(_runningAppsByIdentity[identity]);
                    _runningAppsByIdentity.Remove(identity);
                    changed = true;
                }
            }

            foreach (var (identity, win) in desired)
            {
                if (_runningAppsByIdentity.ContainsKey(identity))
                    continue;

                string label = ResolveRunningLabel(win);
                var vm = new RunningAppViewModel(
                    win.ExecutablePath, label, identity, win.AppUserModelId)
                {
                    IconSize = _appServices.AppearanceService.GetIconsSize(),
                    PinCommand = PinRunningCommand,
                    PinToDockText = _appServices.LocalizationService.Text("dock.runningApp.pin")
                };
                ApplyInteractionSettings(vm);
                LoadRunningIcon(vm, win.Handle);
                _runningAppsByIdentity[identity] = vm;
                RunningApps.Add(vm);
                changed = true;
            }

            if (changed)
            {
                HasRunningApps = RunningApps.Count > 0;
                RepositionAction?.Invoke();
                PreviewDismissAction?.Invoke();
            }
        });
    }

    private static string GetRunningIdentity(RunningWindowInfo window)
    {
        if (!string.IsNullOrWhiteSpace(window.AppUserModelId))
            return $"aumid:{window.AppUserModelId}";
        return $"exe:{window.ExecutablePath}";
    }

    private static string ResolveRunningLabel(RunningWindowInfo window)
    {
        if (!string.IsNullOrWhiteSpace(window.AppUserModelId))
        {
            string id = window.AppUserModelId;
            bool likelyPackagedOrWebApp = id.Contains('!') ||
                                           id.Contains(".App.", StringComparison.OrdinalIgnoreCase) ||
                                           window.ExecutablePath.Contains("\\WindowsApps\\",
                                               StringComparison.OrdinalIgnoreCase);
            if (likelyPackagedOrWebApp && !string.IsNullOrWhiteSpace(window.Title))
                return window.Title;
        }

        string label = Path.GetFileNameWithoutExtension(window.ExecutablePath);
        return string.IsNullOrWhiteSpace(label) ? window.Title : label;
    }

    private static bool IsPinned(
        RunningWindowInfo window,
        List<DockProgramItemModel> pinnedPrograms)
    {
        if (!string.IsNullOrWhiteSpace(window.AppUserModelId))
        {
            return pinnedPrograms.Any(p =>
                !string.IsNullOrWhiteSpace(p.AppUserModelId) &&
                string.Equals(p.AppUserModelId, window.AppUserModelId,
                    StringComparison.OrdinalIgnoreCase));
        }

        string file = Path.GetFileName(window.ExecutablePath);
        foreach (var pinned in pinnedPrograms)
        {
            if (!string.IsNullOrWhiteSpace(pinned.AppUserModelId))
                continue;
            if (string.Equals(pinned.ExecutablePath, window.ExecutablePath,
                    StringComparison.OrdinalIgnoreCase))
                return true;
            if (string.Equals(Path.GetFileName(pinned.ExecutablePath), file,
                    StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    public void PinRunningAppByIdentity(string identityKey)
    {
        RunningAppViewModel? vm = RunningApps.FirstOrDefault(a =>
            string.Equals(a.IdentityKey, identityKey, StringComparison.OrdinalIgnoreCase));
        if (vm != null)
            PinRunningApp(vm);
    }

    private void PinRunningApp(RunningAppViewModel vm)
    {
        if (_appServices == null)
            return;

        var existing = _appServices.DockService.GetItems()
            .OfType<DockProgramItemModel>();
        bool alreadyPinned = !string.IsNullOrWhiteSpace(vm.AppUserModelId)
            ? existing.Any(p => string.Equals(p.AppUserModelId, vm.AppUserModelId,
                StringComparison.OrdinalIgnoreCase))
            : existing.Any(p => string.IsNullOrWhiteSpace(p.AppUserModelId) &&
                string.Equals(p.ExecutablePath, vm.ExecutablePath,
                    StringComparison.OrdinalIgnoreCase));
        if (alreadyPinned)
            return;

        var item = new DockProgramItemModel(vm.Label, vm.ExecutablePath)
        {
            AppUserModelId = vm.AppUserModelId,
            LaunchTarget = vm.LaunchTarget,
            IconCacheKey = vm.IconCacheKey
        };
        _appServices.DockService.AddItem(item);

        if (string.IsNullOrWhiteSpace(vm.IconCacheKey) &&
            !string.IsNullOrWhiteSpace(vm.ExecutablePath))
        {
            _ = Task.Run(() => _appServices.IconGateway.CacheProgramIcon(vm.ExecutablePath));
        }

        _runningAppsByIdentity.Remove(vm.IdentityKey);
        RunningApps.Remove(vm);
        HasRunningApps = RunningApps.Count > 0;
        UpdateDockUI();
    }

    private void LoadRunningIcon(RunningAppViewModel vm, IntPtr windowHandle)
    {
        if (_appServices == null) return;
        string exe = vm.ExecutablePath;

        bool packagedPath = exe.Contains("\\WindowsApps\\", StringComparison.OrdinalIgnoreCase);

        // Desktop apps/PWAs with an explicit AUMID may share one executable.
        // Prefer their window-specific icon cache before the generic EXE icon.
        if (!packagedPath && !string.IsNullOrWhiteSpace(vm.AppUserModelId))
        {
            string key = vm.AppUserModelId;
            string? identityPath = WindowIdentityIconCache.GetCachedIconPath(key);
            var identityIcon = IconLoader.LoadFromFile(identityPath);
            if (identityIcon == null && windowHandle != IntPtr.Zero)
            {
                identityPath = WindowIdentityIconCache.CaptureWindowIcon(key, windowHandle);
                identityIcon = IconLoader.LoadFromFile(identityPath);
            }
            if (identityIcon != null)
            {
                vm.IconCacheKey = key;
                vm.OriginalIcon = identityIcon;
                vm.Icon = IconTinter.Apply(identityIcon);
                return;
            }
        }

        if (string.Equals(Path.GetFileName(exe),
                "SystemSettings.exe", StringComparison.OrdinalIgnoreCase))
        {
            var settingsIcon = IconLoader.LoadFromAsset("Assets/icons/windows_settings.png");
            if (settingsIcon != null)
            {
                vm.OriginalIcon = settingsIcon;
                vm.Icon = IconTinter.Apply(settingsIcon);
                return;
            }
        }

        var icon = IconLoader.LoadFromFile(_appServices.IconGateway.ResolveProgramIcon(exe));
        if (icon != null)
        {
            vm.OriginalIcon = icon;
            vm.Icon = IconTinter.Apply(icon);
            return;
        }

        _ = Task.Run(() =>
        {
            try
            {
                _appServices.IconGateway.CacheProgramIcon(exe);
                string? cached = _appServices.IconGateway.ResolveProgramIcon(exe);
                var loaded = IconLoader.LoadFromFile(cached);
                if (loaded == null && windowHandle != IntPtr.Zero)
                {
                    WindowsIconExtractor.ExtractAndCacheWindowIcon(exe, windowHandle);
                    cached = _appServices.IconGateway.ResolveProgramIcon(exe);
                    loaded = IconLoader.LoadFromFile(cached);
                }
                if (loaded == null)
                {
                    WindowsIconExtractor.ExtractAndCacheAppxIcon(exe);
                    cached = _appServices.IconGateway.ResolveProgramIcon(exe);
                    loaded = IconLoader.LoadFromFile(cached);
                }
                if (loaded != null)
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        vm.OriginalIcon = loaded;
                        vm.Icon = IconTinter.Apply(loaded);
                    });
            }
            catch { }
        });
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
