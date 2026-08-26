namespace CedroModernDock.ViewModels;

/// <summary>
/// User-facing interaction preferences for the dock. These are intentionally
/// independent so a user can keep, for example, live previews while disabling
/// hover labels and magnification.
/// </summary>
public partial class SettingsViewModel
{
    public bool ShowRunningIndicators
    {
        get => _appServices.AppearanceService.GetShowRunningIndicators();
        set
        {
            if (_appServices.AppearanceService.GetShowRunningIndicators() == value)
                return;
            _appServices.AppearanceService.SetShowRunningIndicators(value);
            OnPropertyChanged(nameof(ShowRunningIndicators));
            _dockRefreshAction();
        }
    }

    public bool EnableHoverMagnification
    {
        get => _appServices.AppearanceService.GetEnableHoverMagnification();
        set
        {
            if (_appServices.AppearanceService.GetEnableHoverMagnification() == value)
                return;
            _appServices.AppearanceService.SetEnableHoverMagnification(value);
            OnPropertyChanged(nameof(EnableHoverMagnification));
            _dockRefreshAction();
        }
    }

    public bool ShowHoverLabels
    {
        get => _appServices.AppearanceService.GetShowHoverLabels();
        set
        {
            if (_appServices.AppearanceService.GetShowHoverLabels() == value)
                return;
            _appServices.AppearanceService.SetShowHoverLabels(value);
            OnPropertyChanged(nameof(ShowHoverLabels));
            _dockRefreshAction();
        }
    }

    public bool ShowWindowPreviews
    {
        get => _appServices.AppearanceService.GetShowWindowPreviews();
        set
        {
            if (_appServices.AppearanceService.GetShowWindowPreviews() == value)
                return;
            _appServices.AppearanceService.SetShowWindowPreviews(value);
            OnPropertyChanged(nameof(ShowWindowPreviews));
            _dockRefreshAction();
        }
    }

    public string DockInteractionTitle => T("settings.general.interaction.title");
    public string RunningIndicatorsTitle => T("settings.general.runningIndicators.title");
    public string RunningIndicatorsHelper => T("settings.general.runningIndicators.helper");
    public string HoverMagnificationTitle => T("settings.general.hoverMagnification.title");
    public string HoverMagnificationHelper => T("settings.general.hoverMagnification.helper");
    public string HoverLabelsTitle => T("settings.general.hoverLabels.title");
    public string HoverLabelsHelper => T("settings.general.hoverLabels.helper");
    public string WindowPreviewsTitle => T("settings.general.windowPreviews.title");
    public string WindowPreviewsHelper => T("settings.general.windowPreviews.helper");
}
