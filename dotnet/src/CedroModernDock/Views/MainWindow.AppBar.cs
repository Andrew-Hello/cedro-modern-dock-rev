using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Threading;
using CedroModernDock.Core.Models;
using CedroModernDock.Infrastructure.Windows.Native;
using CedroModernDock.ViewModels;

namespace CedroModernDock.Views;

/// <summary>
/// Runtime for the dedicated BOTTOM_APPBAR positioning mode.
///
/// This intentionally has no periodic Shell polling and no ABN_POSCHANGED
/// callback loop. The Shell is touched only when entering/leaving the mode or
/// after a debounced real Dock height change.
/// </summary>
public partial class MainWindow
{
    private readonly DispatcherTimer _bottomAppBarLayoutTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(180)
    };

    private AppBarReservationManager? _appBarManager;
    private IntPtr _appBarHwnd;
    private bool _bottomAppBarHooksInstalled;

    private void OnBottomAppBarWindowOpened(object? sender, EventArgs e)
    {
        if (_bottomAppBarHooksInstalled)
            return;

        IPlatformHandle? handle = this.TryGetPlatformHandle();
        _appBarHwnd = handle?.Handle ?? IntPtr.Zero;
        if (_appBarHwnd == IntPtr.Zero)
            return;

        _bottomAppBarHooksInstalled = true;
        _appBarManager = new AppBarReservationManager(_appBarHwnd);
        _bottomAppBarLayoutTimer.Tick += OnBottomAppBarLayoutTimerTick;
        SizeChanged += OnBottomAppBarSizeChanged;

        // The MainWindow override installs this same mode-aware callback during
        // its own initialization. Post once as a defensive guarantee for older
        // view-model initialization order; this has no Shell side effects.
        Dispatcher.UIThread.Post(() =>
        {
            if (!_bottomAppBarHooksInstalled)
                return;
            if (DataContext is MainWindowViewModel vm)
                vm.RepositionAction = RepositionForCurrentMode;
            ScheduleBottomAppBarLayout();
        }, DispatcherPriority.Loaded);
    }

    private void OnBottomAppBarWindowClosed(object? sender, EventArgs e)
    {
        SizeChanged -= OnBottomAppBarSizeChanged;
        _bottomAppBarLayoutTimer.Stop();
        _bottomAppBarLayoutTimer.Tick -= OnBottomAppBarLayoutTimerTick;

        _appBarManager?.Dispose();
        _appBarManager = null;
        _appBarHwnd = IntPtr.Zero;
        _bottomAppBarHooksInstalled = false;
    }

    /// <summary>
    /// Actual SizeToContent changes are the authoritative height signal. This
    /// covers vertical padding, icon size, running-indicator visibility and any
    /// future item-layout change. In Bottom AppBar mode it merely restarts a
    /// short debounce timer; it never registers/query-positions an AppBar here.
    /// </summary>
    private void OnBottomAppBarSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (_appServices?.PositioningService.IsBottomAppBarPositioning() == true)
            ScheduleBottomAppBarLayout();
    }

    /// <summary>
    /// Called by MainWindowViewModel after settings/UI changes. Non-AppBar modes
    /// keep the original positioning path; Bottom AppBar never calls it while
    /// its work-area reservation is active.
    /// </summary>
    private void RepositionForCurrentMode()
    {
        if (_appServices == null)
            return;

        if (_appServices.PositioningService.IsBottomAppBarPositioning())
        {
            ScheduleBottomAppBarLayout();
            return;
        }

        // Leaving Bottom AppBar must release the Windows work area BEFORE a
        // static/dynamic position is resolved. This prevents the previous AppBar
        // height from becoming an accidental extra bottom margin.
        if (_appBarManager?.IsRegistered == true)
        {
            _bottomAppBarLayoutTimer.Stop();
            _appBarManager.Remove();
            Dispatcher.UIThread.Post(() => ApplyDockPosition(force: true), DispatcherPriority.Loaded);
            return;
        }

        ApplyDockPosition();
    }

    private void ScheduleBottomAppBarLayout()
    {
        if (!_bottomAppBarHooksInstalled || _appServices == null)
            return;

        if (!_appServices.PositioningService.IsBottomAppBarPositioning())
        {
            if (_appBarManager?.IsRegistered == true)
                _appBarManager.Remove();
            return;
        }

        // Wait for Avalonia's SizeToContent layout to settle. Multiple changes in
        // the same UI operation collapse to one UpdateHeight call.
        _bottomAppBarLayoutTimer.Stop();
        _bottomAppBarLayoutTimer.Start();
    }

    private void OnBottomAppBarLayoutTimerTick(object? sender, EventArgs e)
    {
        _bottomAppBarLayoutTimer.Stop();
        ApplyBottomAppBarLayout();
    }

    private void ApplyBottomAppBarLayout()
    {
        if (_appServices == null || _appBarManager == null || _appBarHwnd == IntPtr.Zero)
            return;

        if (!_appServices.PositioningService.IsBottomAppBarPositioning())
        {
            _appBarManager.Remove();
            return;
        }

        // Bottom AppBar is horizontal by contract. This is also enforced in the
        // Settings ViewModel, but keep a runtime guard for imported configs.
        if (_appServices.AppearanceService.GetVerticalDock())
        {
            _appServices.AppearanceService.SetVerticalDock(false);
            if (DataContext is MainWindowViewModel vm)
                vm.UpdateDockUI();
            return;
        }

        if (!WindowEnvironment.TryGetWindowRect(_appBarHwnd, out RECT windowRect))
            return;

        int width = Math.Max(1, windowRect.Right - windowRect.Left);
        int height = Math.Max(1, windowRect.Bottom - windowRect.Top);

        if (!_appBarManager.IsRegistered)
        {
            // Read taskbar-aware work geometry ONCE before Cedro registers.
            // After ABM_NEW/SETPOS we deliberately never feed rcWork back into
            // AppBar geometry, which is the core anti-nesting invariant.
            if (!MonitorWorkArea.TryGet(_appBarHwnd, out RECT monitor, out RECT baselineWork))
                return;

            if (!_appBarManager.RegisterBottom(monitor, baselineWork, height))
                return;
        }
        else
        {
            // No-op when height is unchanged. Settings open/close and horizontal
            // alignment changes therefore do not touch Windows work-area height.
            if (!_appBarManager.UpdateHeight(height))
                return;
        }

        PositionBottomAppBarDock(width, height);
    }

    private void PositionBottomAppBarDock(int width, int height)
    {
        if (_appServices == null || _appBarManager?.IsRegistered != true)
            return;

        RECT reservation = _appBarManager.ReservationRect;
        int availableWidth = Math.Max(1, reservation.Right - reservation.Left);
        width = Math.Min(width, availableWidth);

        int x = _appServices.PositioningService.GetHorizontalAnchor() switch
        {
            DockHorizontalAnchor.LEFT => reservation.Left,
            DockHorizontalAnchor.MIDDLE => reservation.Left + ((availableWidth - width) / 2),
            DockHorizontalAnchor.RIGHT => reservation.Right - width,
            _ => reservation.Left
        };
        int y = reservation.Top;

        var target = new PixelPoint(x, y);
        if (Position != target)
            Position = target;
    }
}
