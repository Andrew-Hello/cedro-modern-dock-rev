using System;

namespace CedroModernDock.ViewModels;

public partial class SettingsViewModel
{
    public int DockVerticalPadding
    {
        get => _appServices.AppearanceService.GetDockVerticalPadding();
        set
        {
            int clamped = Math.Clamp(value, 0, 20);
            if (_appServices.AppearanceService.GetDockVerticalPadding() == clamped)
                return;

            _appServices.AppearanceService.SetDockVerticalPadding(clamped);
            OnPropertyChanged(nameof(DockVerticalPadding));
            _dockRefreshAction();
        }
    }

    public string VerticalPaddingTitle => T("settings.dockCustomization.verticalPadding.title");
    public string VerticalPaddingHelper => T("settings.dockCustomization.verticalPadding.helper");
}
