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

    public bool AutoHideAtScreenEdge
    {
        get => _appServices.AppearanceService.GetAutoHideAtScreenEdge();
        set
        {
            if (_appServices.AppearanceService.GetAutoHideAtScreenEdge() == value)
                return;
            _appServices.AppearanceService.SetAutoHideAtScreenEdge(value);
            OnPropertyChanged(nameof(AutoHideAtScreenEdge));
            _dockRefreshAction();
        }
    }

    public string WindowBehaviorTitle => T("settings.general.windowBehavior.title");
    public string AlwaysOnTopTitle => T("settings.general.alwaysOnTop.title");
    public string AlwaysOnTopHelper => T("settings.general.alwaysOnTop.helper");
    public string EdgeAutoHideTitle => T("settings.general.edgeAutoHide.title");
    public string EdgeAutoHideHelper => T("settings.general.edgeAutoHide.helper");
}
