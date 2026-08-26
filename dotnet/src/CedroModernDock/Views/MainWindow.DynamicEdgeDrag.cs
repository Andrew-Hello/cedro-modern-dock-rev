using Avalonia;

namespace CedroModernDock.Views;

public partial class MainWindow
{
    /// <summary>
    /// XAML-level PositionChanged hook used specifically to detect a user moving
    /// an already edge-docked Dynamic dock. Auto-hide animation moves are ignored.
    /// Once the window leaves both known edge positions it becomes free again;
    /// the normal dynamic snap debounce then decides whether the release should
    /// magnetically attach to a new edge or remain freely positioned.
    /// </summary>
    private void OnDynamicEdgePositionChanged(object? sender, PixelPointEventArgs e)
    {
        if (_appServices == null || !_edgeAutoHideActive || _dynamicSnapInProgress)
            return;
        if (!_appServices.PositioningService.IsDynamicPositioning() ||
            !_appServices.AppearanceService.GetAutoHideAtScreenEdge())
            return;
        if (_edgeAnimationTimer?.IsEnabled == true)
            return;

        bool atVisible = IsNear(Position, _edgeVisiblePosition);
        bool atHidden = IsNear(Position, _edgeHiddenPosition);
        if (atVisible || atHidden)
            return;

        // A native move-drag has pulled the shown dock away from its magnetic
        // edge. Release the old docking state immediately. The 130ms snap timer
        // will re-dock it if the drag ends near another compatible edge.
        _edgeAutoHideActive = false;
        _edgeShown = true;
        SetDockChromeVisible(true);
        _appServices.AppearanceService.SetDynamicEdgeDockState(false, 0, 0);

        _dynamicSnapTimer?.Stop();
        _dynamicSnapTimer?.Start();
    }
}
