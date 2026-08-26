namespace CedroModernDock.ViewModels;

using System.Windows.Input;
using Avalonia.Media.Imaging;
using CedroModernDock.Core.Models;
using CedroModernDock.Infrastructure.Windows.Native;

/// <summary>
/// ViewModel for a single pinned dock item. Besides icon/running state, it now
/// exposes per-item interaction flags copied from the global dock preferences so
/// XAML can turn indicators, hover labels and magnification on/off live.
/// </summary>
public class DockItemViewModel : ViewModelBase
{
    private Bitmap? _icon;
    private Bitmap? _customIcon;
    private bool _customIconResolved;
    private bool _isRunning;
    private bool _showRunningIndicator = true;
    private bool _enableHoverMagnification = true;
    private bool _showHoverLabel = true;

    public DockItem Item { get; }
    public string Label { get; }

    public Bitmap? Icon
    {
        get => _icon;
        set
        {
            // A custom icon is an explicit per-item override and therefore wins
            // over every automatic source (EXE, AppX/PWA cache, folder, module).
            // It is intentionally shown exactly as selected rather than receiving
            // the optional global icon tint.
            Bitmap? effective = ResolveCustomIcon() ?? value;
            SetProperty(ref _icon, effective);
        }
    }

    private Bitmap? ResolveCustomIcon()
    {
        if (_customIconResolved)
            return _customIcon;

        _customIconResolved = true;
        string? path = CustomIconStore.EnsureCached(Item.CustomIconPngBase64);
        _customIcon = IconLoader.LoadFromFile(path);
        return _customIcon;
    }

    public bool IsRunning
    {
        get => _isRunning;
        set
        {
            if (SetProperty(ref _isRunning, value))
                OnPropertyChanged(nameof(IndicatorVisible));
        }
    }

    /// <summary>True for program items that support running-state polling.</summary>
    public bool ShowIndicator { get; }

    public bool ShowRunningIndicator
    {
        get => _showRunningIndicator;
        set
        {
            if (SetProperty(ref _showRunningIndicator, value))
                OnPropertyChanged(nameof(IndicatorVisible));
        }
    }

    public bool IndicatorVisible => ShowIndicator && ShowRunningIndicator && IsRunning;

    public bool EnableHoverMagnification
    {
        get => _enableHoverMagnification;
        set => SetProperty(ref _enableHoverMagnification, value);
    }

    public bool ShowHoverLabel
    {
        get => _showHoverLabel;
        set
        {
            if (SetProperty(ref _showHoverLabel, value))
                OnPropertyChanged(nameof(TooltipText));
        }
    }

    public string? TooltipText => ShowHoverLabel ? Label : null;

    public int IconSize { get; set; } = 48;
    public string? ExecutablePath { get; }
    public string? AppUserModelId { get; }
    public ICommand ClickCommand { get; }

    public DockItemViewModel(
        DockItem item,
        string label,
        ICommand clickCommand,
        bool showIndicator = false,
        string? executablePath = null,
        string? appUserModelId = null)
    {
        Item = item;
        Label = label;
        ClickCommand = clickCommand;
        ShowIndicator = showIndicator;
        ExecutablePath = executablePath;
        AppUserModelId = appUserModelId;
    }
}
