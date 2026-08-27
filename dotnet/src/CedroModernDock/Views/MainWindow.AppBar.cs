using System;
using Avalonia;
using Avalonia.Platform;
using Avalonia.Threading;
using CedroModernDock.Core.Models;
using CedroModernDock.Infrastructure.Windows.Native;

namespace CedroModernDock.Views;

/// <summary>
/// Optional Windows AppBar integration. Cedro reserves a full edge strip so
/// ordinary maximized windows stop at the Dock's inner edge, while the actual
/// Dock HWND remains compact and keeps its current along-edge alignment.
/// </summary>
public partial class MainWindow
{
    private readonly DispatcherTimer _appBarPolicyTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(300)
    };

    private AppBarReservationManager? _appBarManager;
    private IntPtr _appBarHwnd;

    private void OnAppBarOpened(object? sender, EventArgs e)
    {
        IPlatformHandle? handle = this.TryGetPlatformHandle();
        _appBarHwnd = handle?.Handle ?? IntPtr.Zero;
        if (_appBarHwnd == IntPtr.Zero)
            return;

        _appBarManager = new AppBarReservationManager(_appBarHwnd);
        _appBarManager.ShellPositionChanged += OnAppBarShellPositionChanged;
        _appBarPolicyTimer.Tick += OnAppBarPolicyTick;
        _appBarPolicyTimer.Start();

        // Wait until Avalonia has completed the first SizeToContent pass and the
        // existing Dock positioning code has applied its saved anchor.
        Dispatcher.UIThread.Post(RefreshAppBarPolicy, DispatcherPriority.Loaded);
    }

    private void OnAppBarClosed(object? sender, EventArgs e)
    {
        _appBarPolicyTimer.Stop();
        _appBarPolicyTimer.Tick -= OnAppBarPolicyTick;

        if (_appBarManager != null)
        {
            _appBarManager.ShellPositionChanged -= OnAppBarShellPositionChanged;
            _appBarManager.Dispose();
            _appBarManager = null;
        }

        _appBarHwnd = IntPtr.Zero;
    }

    private void OnAppBarPolicyTick(object? sender, EventArgs e)
        => RefreshAppBarPolicy();

    private void OnAppBarShellPositionChanged()
    {
        // The Shell broadcasts ABN_POSCHANGED whenever an AppBar moves. Cedro
        // deliberately does not force another ABM_SETPOS for an unchanged
        // geometry: doing so could make our own SetPos generate another callback
        // indefinitely. The normal Update comparison still re-queries whenever
        // the taskbar/work area, monitor, edge or Dock thickness actually changes.
        Dispatcher.UIThread.Post(RefreshAppBarPolicy);
    }

    private void RefreshAppBarPolicy()
    {
        if (_appServices == null || _appBarManager == null || _appBarHwnd == IntPtr.Zero)
            return;

        if (!_appServices.AppearanceService.GetReserveDesktopSpace() ||
            !TryResolveAppBarSide(out EdgeSide side))
        {
            ReleaseAppBarReservation();
            return;
        }

        if (!MonitorWorkArea.TryGet(_appBarHwnd, out RECT monitor, out RECT safeWork) ||
            !WindowEnvironment.TryGetWindowRect(_appBarHwnd, out RECT currentWindow))
        {
            ReleaseAppBarReservation();
            return;
        }

        int width = Math.Max(1, currentWindow.Right - currentWindow.Left);
        int height = Math.Max(1, currentWindow.Bottom - currentWindow.Top);

        // Auto-hide may currently have the HWND mostly outside the visible area.
        // Always calculate reservation thickness from its expanded position.
        RECT visibleWindow = currentWindow;
        if (_edgeAutoHideActive && _edgeWindowWidth > 0 && _edgeWindowHeight > 0)
        {
            width = _edgeWindowWidth;
            height = _edgeWindowHeight;
            visibleWindow = new RECT
            {
                Left = _edgeVisiblePosition.X,
                Top = _edgeVisiblePosition.Y,
                Right = _edgeVisiblePosition.X + width,
                Bottom = _edgeVisiblePosition.Y + height
            };
        }

        int edgeGap = side switch
        {
            EdgeSide.Top => Math.Max(0, visibleWindow.Top - safeWork.Top),
            EdgeSide.Bottom => Math.Max(0, safeWork.Bottom - visibleWindow.Bottom),
            EdgeSide.Left => Math.Max(0, visibleWindow.Left - safeWork.Left),
            EdgeSide.Right => Math.Max(0, safeWork.Right - visibleWindow.Right),
            _ => 0
        };

        int thickness = side is EdgeSide.Top or EdgeSide.Bottom
            ? height + edgeGap
            : width + edgeGap;

        AppBarEdge appBarEdge = ToAppBarEdge(side);
        bool updated = _appBarManager.Update(
            appBarEdge,
            monitor,
            safeWork,
            thickness,
            force: false);

        if (!updated)
        {
            ReleaseAppBarReservation();
            return;
        }

        // Edge-auto-hide has its own 40 ms geometry engine. MonitorWorkArea now
        // removes Cedro's own reservation from rcWork, so that engine naturally
        // continues using the taskbar boundary without recursive inward drift.
        if (_edgeAutoHideActive)
            return;

        PositionDockInsideReservation(
            side, _appBarManager.ReservationRect, monitor, width, height, currentWindow);
    }

    private bool TryResolveAppBarSide(out EdgeSide side)
    {
        side = EdgeSide.Bottom;
        if (_appServices == null)
            return false;

        if (_edgeAutoHideActive)
        {
            side = _edgeSide;
            return true;
        }

        if (_appServices.PositioningService.IsDynamicPositioning())
        {
            // A free-floating Dynamic Dock must never leave a phantom work-area
            // strip behind. Only a deliberately edge-snapped Dynamic Dock gets
            // an AppBar reservation.
            if (!_appServices.AppearanceService.GetDynamicEdgeDocked())
                return false;
            return TryDecodeEdgeSide(
                _appServices.AppearanceService.GetDynamicEdgeSide(), out side);
        }

        bool verticalDock = _appServices.AppearanceService.GetVerticalDock();
        if (verticalDock)
        {
            DockHorizontalAnchor anchor = _appServices.PositioningService.GetHorizontalAnchor();
            if (anchor == DockHorizontalAnchor.LEFT)
            {
                side = EdgeSide.Left;
                return true;
            }
            if (anchor == DockHorizontalAnchor.RIGHT)
            {
                side = EdgeSide.Right;
                return true;
            }
            return false;
        }

        DockVerticalAnchor verticalAnchor = _appServices.PositioningService.GetVerticalAnchor();
        if (verticalAnchor == DockVerticalAnchor.TOP)
        {
            side = EdgeSide.Top;
            return true;
        }
        if (verticalAnchor == DockVerticalAnchor.DOWN)
        {
            side = EdgeSide.Bottom;
            return true;
        }
        return false;
    }

    private void PositionDockInsideReservation(
        EdgeSide side,
        RECT reservation,
        RECT monitor,
        int width,
        int height,
        RECT currentWindow)
    {
        int x = currentWindow.Left;
        int y = currentWindow.Top;

        switch (side)
        {
            case EdgeSide.Top:
                x = Math.Clamp(x, monitor.Left, Math.Max(monitor.Left, monitor.Right - width));
                y = reservation.Bottom - height;
                break;
            case EdgeSide.Bottom:
                x = Math.Clamp(x, monitor.Left, Math.Max(monitor.Left, monitor.Right - width));
                y = reservation.Top;
                break;
            case EdgeSide.Left:
                x = reservation.Right - width;
                y = Math.Clamp(y, monitor.Top, Math.Max(monitor.Top, monitor.Bottom - height));
                break;
            case EdgeSide.Right:
                x = reservation.Left;
                y = Math.Clamp(y, monitor.Top, Math.Max(monitor.Top, monitor.Bottom - height));
                break;
        }

        var target = new PixelPoint(x, y);
        if (!IsNear(Position, target))
            Position = target;
    }

    private void ReleaseAppBarReservation()
    {
        if (_appBarManager?.IsRegistered != true)
            return;

        _appBarManager.Remove();

        // Static positioning may have been adjusted by the AppBar boundary.
        // Re-resolve once after the Shell restores the normal work area.
        if (_appServices?.PositioningService.IsDynamicPositioning() == false)
            Dispatcher.UIThread.Post(() => ApplyDockPosition(force: true));
    }

    private static AppBarEdge ToAppBarEdge(EdgeSide side) => side switch
    {
        EdgeSide.Top => AppBarEdge.Top,
        EdgeSide.Bottom => AppBarEdge.Bottom,
        EdgeSide.Left => AppBarEdge.Left,
        EdgeSide.Right => AppBarEdge.Right,
        _ => AppBarEdge.Bottom
    };
}
