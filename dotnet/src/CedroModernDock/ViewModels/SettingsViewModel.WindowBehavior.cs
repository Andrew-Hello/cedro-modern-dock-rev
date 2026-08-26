namespace CedroModernDock.ViewModels;

public partial class SettingsViewModel
{
    public bool AlwaysOnTop
    {
        get => _appServices.AppearanceService.GetAlwaysOnTop();
        set
        {
            if (_appServices.AppearanceService.GetAlwaysOnTop() == value)
                return;
            _appServices.AppearanceService.SetAlwaysOnTop(value);
            OnPropertyChanged(nameof(AlwaysOnTop));
            _dockRefreshAction();
        }
    }

    /// <summary>Top/bottom edge docking and auto-hide.</summary>
    public bool AutoHideAtHorizontalEdges
    {
        get => _appServices.AppearanceService.GetAutoHideAtHorizontalEdges();
        set
        {
            if (_appServices.AppearanceService.GetAutoHideAtHorizontalEdges() == value)
                return;
            _appServices.AppearanceService.SetAutoHideAtHorizontalEdges(value);
            OnPropertyChanged(nameof(AutoHideAtHorizontalEdges));
            _dockRefreshAction();
        }
    }

    /// <summary>Left/right edge docking and auto-hide.</summary>
    public bool AutoHideAtVerticalEdges
    {
        get => _appServices.AppearanceService.GetAutoHideAtVerticalEdges();
        set
        {
            if (_appServices.AppearanceService.GetAutoHideAtVerticalEdges() == value)
                return;
            _appServices.AppearanceService.SetAutoHideAtVerticalEdges(value);
            OnPropertyChanged(nameof(AutoHideAtVerticalEdges));
            _dockRefreshAction();
        }
    }

    public string WindowBehaviorTitle => T("settings.general.windowBehavior.title");
    public string AlwaysOnTopTitle => T("settings.general.alwaysOnTop.title");
    public string AlwaysOnTopHelper => T("settings.general.alwaysOnTop.helper");
    public string HorizontalEdgeAutoHideTitle => T("settings.general.edgeAutoHide.horizontal.title");
    public string HorizontalEdgeAutoHideHelper => T("settings.general.edgeAutoHide.horizontal.helper");
    public string VerticalEdgeAutoHideTitle => T("settings.general.edgeAutoHide.vertical.title");
    public string VerticalEdgeAutoHideHelper => T("settings.general.edgeAutoHide.vertical.helper");
}
