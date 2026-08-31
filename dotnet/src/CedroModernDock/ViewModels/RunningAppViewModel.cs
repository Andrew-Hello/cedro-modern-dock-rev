using System.Windows.Input;
using Avalonia.Media.Imaging;

namespace CedroModernDock.ViewModels;

/// <summary>
/// A running program/window-group that is not pinned yet. It carries both the
/// executable path and an optional AppUserModelID so packaged apps/PWAs can be
/// pinned with a stable Windows shell identity.
/// </summary>
public class RunningAppViewModel : ViewModelBase
{
    private Bitmap? _icon;
    private bool _isRunning = true;
    private int _iconSize = 48;
    private bool _showRunningIndicator = true;
    private bool _enableHoverMagnification = true;
    private bool _showHoverLabel = true;
    private string _pinToDockText = "Pin to Dock";

    public string ExecutablePath { get; }
    public string Label { get; }
    public string IdentityKey { get; }
    public string? AppUserModelId { get; }
    public string? LaunchTarget { get; }

    /// <summary>Identity-specific icon cache key, usually the AUMID.</summary>
    public string? IconCacheKey { get; set; }

    public Bitmap? Icon
    {
        get => _icon;
        set => SetProperty(ref _icon, value);
    }

    public Bitmap? OriginalIcon { get; set; }

    public bool IsRunning
    {
        get => _isRunning;
        set
        {
            if (SetProperty(ref _isRunning, value))
            {
                OnPropertyChanged(nameof(IndicatorVisible));
                OnPropertyChanged(nameof(IndicatorIconOffsetY));
            }
        }
    }

    public int IconSize
    {
        get => _iconSize;
        set => SetProperty(ref _iconSize, value);
    }

    public bool ShowRunningIndicator
    {
        get => _showRunningIndicator;
        set
        {
            if (SetProperty(ref _showRunningIndicator, value))
            {
                OnPropertyChanged(nameof(IndicatorVisible));
                OnPropertyChanged(nameof(IndicatorIconOffsetY));
            }
        }
    }

    public bool IndicatorVisible => ShowRunningIndicator && IsRunning;

    /// <summary>
    /// XAML permanently reserves the indicator slot. The icon only moves within
    /// that fixed slot, so showing/hiding a dot never changes Dock/AppBar height.
    /// </summary>
    public double IndicatorIconOffsetY => IndicatorVisible ? 0d : 3d;

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

    public string PinToDockText
    {
        get => _pinToDockText;
        set => SetProperty(ref _pinToDockText, value);
    }

    public ICommand? PinCommand { get; set; }

    public RunningAppViewModel(
        string executablePath,
        string label,
        string identityKey,
        string? appUserModelId = null)
    {
        ExecutablePath = executablePath;
        Label = label;
        IdentityKey = identityKey;
        AppUserModelId = appUserModelId;
        LaunchTarget = string.IsNullOrWhiteSpace(appUserModelId)
            ? null
            : $"shell:AppsFolder\\{appUserModelId}";
    }
}
