namespace CedroModernDock.Core.Application;

using CedroModernDock.Core.Models;

/// <summary>Direct port of DockAppearanceService.</summary>
public class DockAppearanceService
{
    private readonly DockService _dockService;

    public DockAppearanceService(DockService dockService)
    {
        _dockService = dockService;
    }

    public DockModel GetDock() => _dockService.GetDock();

    public int GetIconsSize() => GetDock().IconsSize;

    public void SetIconsSize(int iconsSize)
    {
        GetDock().IconsSize = iconsSize;
        _dockService.SaveChanges();
    }

    public int GetSpacingBetweenIcons() => GetDock().SpacingBetweenIcons;

    public void SetSpacingBetweenIcons(int spacingValue)
    {
        GetDock().SpacingBetweenIcons = spacingValue;
        _dockService.SaveChanges();
    }

    public int GetDockVerticalPadding() => Math.Clamp(GetDock().DockVerticalPadding, 0, 20);

    public void SetDockVerticalPadding(int value)
    {
        GetDock().DockVerticalPadding = Math.Clamp(value, 0, 20);
        _dockService.SaveChanges();
    }

    public bool GetShowRunningIndicators() => GetDock().ShowRunningIndicators;

    public void SetShowRunningIndicators(bool value)
    {
        GetDock().ShowRunningIndicators = value;
        _dockService.SaveChanges();
    }

    public bool GetEnableHoverMagnification() => GetDock().EnableHoverMagnification;

    public void SetEnableHoverMagnification(bool value)
    {
        GetDock().EnableHoverMagnification = value;
        _dockService.SaveChanges();
    }

    public bool GetShowHoverLabels() => GetDock().ShowHoverLabels;

    public void SetShowHoverLabels(bool value)
    {
        GetDock().ShowHoverLabels = value;
        _dockService.SaveChanges();
    }

    public bool GetShowWindowPreviews() => GetDock().ShowWindowPreviews;

    public void SetShowWindowPreviews(bool value)
    {
        GetDock().ShowWindowPreviews = value;
        _dockService.SaveChanges();
    }

    public bool GetAlwaysOnTop() => GetDock().AlwaysOnTop;

    public void SetAlwaysOnTop(bool value)
    {
        GetDock().AlwaysOnTop = value;
        _dockService.SaveChanges();
    }

    /// <summary>
    /// Legacy Rev v1.2 AppBar experiment flag. It is deliberately no longer
    /// consumed by runtime positioning; AppBar ownership now exists only as the
    /// dedicated BOTTOM_APPBAR positioning mode. Keep the field/getter/setter so
    /// old config files continue to deserialize and round-trip safely.
    /// </summary>
    public bool GetReserveDesktopSpace() => GetDock().ReserveDesktopSpace;

    public void SetReserveDesktopSpace(bool value)
    {
        GetDock().ReserveDesktopSpace = value;
        _dockService.SaveChanges();
    }

    /// <summary>
    /// Legacy aggregate getter kept for runtime callers. Bottom AppBar owns the
    /// Shell work-area contract exclusively, so the independent edge-auto-hide
    /// state machine is forcibly disabled in that mode even for imported configs.
    /// </summary>
    public bool GetAutoHideAtScreenEdge()
    {
        if (GetDock().PositioningMode == DockPositioningMode.BOTTOM_APPBAR)
            return false;
        return GetAutoHideAtHorizontalEdges() || GetAutoHideAtVerticalEdges();
    }

    /// <summary>
    /// Legacy aggregate setter. Used only for backward compatibility and sets
    /// both independent edge groups to the same value.
    /// </summary>
    public void SetAutoHideAtScreenEdge(bool value)
    {
        DockModel dock = GetDock();
        dock.AutoHideAtScreenEdge = value;
        dock.AutoHideAtHorizontalEdges = value;
        dock.AutoHideAtVerticalEdges = value;
        _dockService.SaveChanges();
    }

    /// <summary>Top/bottom screen edges. Always disabled in Bottom AppBar mode.</summary>
    public bool GetAutoHideAtHorizontalEdges()
    {
        DockModel dock = GetDock();
        if (dock.PositioningMode == DockPositioningMode.BOTTOM_APPBAR)
            return false;
        return dock.AutoHideAtHorizontalEdges ?? dock.AutoHideAtScreenEdge;
    }

    public void SetAutoHideAtHorizontalEdges(bool value)
    {
        DockModel dock = GetDock();
        dock.AutoHideAtHorizontalEdges = value;
        dock.AutoHideAtScreenEdge = value || GetAutoHideAtVerticalEdges();
        _dockService.SaveChanges();
    }

    /// <summary>Left/right screen edges. Always disabled in Bottom AppBar mode.</summary>
    public bool GetAutoHideAtVerticalEdges()
    {
        DockModel dock = GetDock();
        if (dock.PositioningMode == DockPositioningMode.BOTTOM_APPBAR)
            return false;
        return dock.AutoHideAtVerticalEdges ?? dock.AutoHideAtScreenEdge;
    }

    public void SetAutoHideAtVerticalEdges(bool value)
    {
        DockModel dock = GetDock();
        dock.AutoHideAtVerticalEdges = value;
        dock.AutoHideAtScreenEdge = value || GetAutoHideAtHorizontalEdges();
        _dockService.SaveChanges();
    }

    public bool GetDynamicEdgeDocked() => GetDock().DynamicEdgeDocked;
    public int GetDynamicEdgeSide() => Math.Clamp(GetDock().DynamicEdgeSide, 0, 4);
    public int GetDynamicEdgeOffset() => Math.Max(0, GetDock().DynamicEdgeOffset);

    public void SetDynamicEdgeDockState(bool docked, int side, int offset)
    {
        DockModel dock = GetDock();
        dock.DynamicEdgeDocked = docked;
        dock.DynamicEdgeSide = docked ? Math.Clamp(side, 1, 4) : 0;
        dock.DynamicEdgeOffset = docked ? Math.Max(0, offset) : 0;
        _dockService.SaveChanges();
    }

    public int GetDockTransparencyPercentage() => (int)(GetDock().DockTransparency * 100);

    public void SetDockTransparencyPercentage(int value)
    {
        GetDock().DockTransparency = (double)value / 100;
        _dockService.SaveChanges();
    }

    public int GetDockBorderRounding() => GetDock().DockBorderRounding;

    public void SetDockBorderRounding(int value)
    {
        GetDock().DockBorderRounding = value;
        _dockService.SaveChanges();
    }

    public string GetDockColorRGB() => GetDock().DockColorRGB;

    public void SetDockColorRGB(string value)
    {
        GetDock().DockColorRGB = value;
        _dockService.SaveChanges();
    }

    public DockTheme GetDockTheme()
    {
        DockModel dock = GetDock();
        return new DockTheme(dock.DockColorRGB, dock.DockTransparency, dock.DockBorderRounding);
    }

    public bool GetShowUnpinnedRunningApps() => GetDock().ShowUnpinnedRunningApps;

    public void SetShowUnpinnedRunningApps(bool value)
    {
        GetDock().ShowUnpinnedRunningApps = value;
        _dockService.SaveChanges();
    }

    public bool GetVerticalDock() => GetDock().VerticalDock;

    public void SetVerticalDock(bool value)
    {
        GetDock().VerticalDock = value;
        _dockService.SaveChanges();
    }

    public bool GetTintIcons() => GetDock().TintIcons;

    public void SetTintIcons(bool value)
    {
        GetDock().TintIcons = value;
        _dockService.SaveChanges();
    }

    public string GetTintColorRGB() => GetDock().TintColorRGB;

    public void SetTintColorRGB(string value)
    {
        GetDock().TintColorRGB = value;
        _dockService.SaveChanges();
    }
}
