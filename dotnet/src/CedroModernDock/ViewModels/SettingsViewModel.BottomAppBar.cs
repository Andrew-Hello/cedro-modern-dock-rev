using System.ComponentModel;
using CedroModernDock.Core.Models;

namespace CedroModernDock.ViewModels;

/// <summary>
/// Three-way positioning-mode selector. It deliberately does not reuse the old
/// IsStaticMode true/false toggle because Bottom AppBar is a third, mutually
/// exclusive mode with a stricter Windows Shell contract.
/// </summary>
public partial class SettingsViewModel
{
    public bool IsStaticPositioningSelected
    {
        get => _appServices.PositioningService.GetPositioningMode() == DockPositioningMode.STATIC;
        set { if (value) SelectPositioningMode(DockPositioningMode.STATIC); }
    }

    public bool IsDynamicPositioningSelected
    {
        get => _appServices.PositioningService.GetPositioningMode() == DockPositioningMode.DYNAMIC;
        set { if (value) SelectPositioningMode(DockPositioningMode.DYNAMIC); }
    }

    public bool IsBottomAppBarMode
    {
        get => _appServices.PositioningService.GetPositioningMode() == DockPositioningMode.BOTTOM_APPBAR;
        set { if (value) SelectPositioningMode(DockPositioningMode.BOTTOM_APPBAR); }
    }

    public bool CanArrangeVertical => !IsBottomAppBarMode;

    public string BottomAppBarText => T("settings.positioning.mode.bottomAppBar");
    public string BottomAppBarTitle => T("settings.positioning.bottomAppBar.title");
    public string BottomAppBarHelper => T("settings.positioning.bottomAppBar.helper");
    public string BottomAppBarAlignmentTitle => T("settings.positioning.bottomAppBar.alignment");

    private void SelectPositioningMode(DockPositioningMode mode)
    {
        DockPositioningMode current = _appServices.PositioningService.GetPositioningMode();
        if (current == mode)
            return;

        // Bottom AppBar is always a horizontal strip directly above the taskbar.
        // Persisting a horizontal layout avoids a contradictory General setting.
        if (mode == DockPositioningMode.BOTTOM_APPBAR && _appServices.AppearanceService.GetVerticalDock())
        {
            _appServices.AppearanceService.SetVerticalDock(false);
            _isVerticalDock = false;
            base.OnPropertyChanged(new PropertyChangedEventArgs(nameof(IsVerticalDock)));
        }

        // Keep the legacy bool field coherent for any old code paths without
        // invoking its old two-state mode-change handler.
        _isStaticMode = mode == DockPositioningMode.STATIC;
        _positioningModeChangeAction(mode);

        base.OnPropertyChanged(new PropertyChangedEventArgs(nameof(IsStaticMode)));
        OnPropertyChanged(nameof(IsStaticPositioningSelected));
        OnPropertyChanged(nameof(IsDynamicPositioningSelected));
        OnPropertyChanged(nameof(IsBottomAppBarMode));
        OnPropertyChanged(nameof(CanArrangeVertical));
        _dockRefreshAction();
    }
}
