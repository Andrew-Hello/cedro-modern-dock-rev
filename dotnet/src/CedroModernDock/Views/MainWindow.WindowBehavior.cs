using System;
using Avalonia;
using Avalonia.Platform;
using Avalonia.Threading;
using CedroModernDock.Core.Models;
using CedroModernDock.Infrastructure.Windows.Native;

namespace CedroModernDock.Views;

public partial class MainWindow
{
    private const int EdgeRevealThickness = 3;
    private const int EdgeHideDelayMs = 450;
    private const int EdgeAnimationMs = 130;

    private IntPtr _enhancedWindowHandle;
    private DispatcherTimer? _topmostPolicyTimer;
    private DispatcherTimer? _edgePollTimer;
    private DispatcherTimer? _edgeAnimationTimer;

    private bool _foregroundFullscreen;
    private bool _edgeAutoHideActive;
    private bool _edgeShown = true;
    private PixelPoint _edgeVisiblePosition;
    private PixelPoint _edgeHiddenPosition;
    private EdgeSide _edgeSide;
    private RECT _edgeMonitorRect;
    private int _edgeWindowWidth;
    private int _edgeWindowHeight;
    private DateTime _edgeHideAfterUtc;

    private PixelPoint _edgeAnimationFrom;
    private PixelPoint _edgeAnimationTo;
    private DateTime _edgeAnimationStartedUtc;

    private enum EdgeSide
    {
        Top,
        Bottom,
        Left,
        Right
    }

    /// <summary>
    /// Called by the main constructor. The actual native work starts only after
    /// Opened, when Avalonia has created the HWND and DockWindowBehavior exists.
    /// </summary>
    private void InitializeEnhancedWindowBehaviorHooks()
    {
        Opened += OnEnhancedWindowOpened;
        Closed += OnEnhancedWindowClosed;
    }

    private void OnEnhancedWindowOpened(object? sender, EventArgs e)
    {
        IPlatformHandle? handle = this.TryGetPlatformHandle();
        _enhancedWindowHandle = handle?.Handle ?? IntPtr.Zero;
        if (_enhancedWindowHandle == IntPtr.Zero)
            return;

        _topmostPolicyTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(350)
        };
        _topmostPolicyTimer.Tick += (_, _) => RefreshTopmostPolicy();

        _edgePollTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(40)
        };
        _edgePollTimer.Tick += (_, _) => PollEdgeAutoHide();

        _edgeAnimationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(15)
        };
        _edgeAnimationTimer.Tick += (_, _) => TickEdgeAnimation();

        RefreshTopmostPolicy();
        PollEdgeAutoHide();
        _topmostPolicyTimer.Start();
        _edgePollTimer.Start();
    }

    private void OnEnhancedWindowClosed(object? sender, EventArgs e)
    {
        _topmostPolicyTimer?.Stop();
        _edgePollTimer?.Stop();
        _edgeAnimationTimer?.Stop();
        _topmostPolicyTimer = null;
        _edgePollTimer = null;
        _edgeAnimationTimer = null;
        _edgeAutoHideActive = false;
        _enhancedWindowHandle = IntPtr.Zero;
    }

    /// <summary>
    /// Applies the user's Always-on-top preference, but temporarily drops the
    /// dock out of the topmost band while another app truly covers its monitor.
    /// This makes fullscreen/borderless games immune to dock overlays and
    /// restores topmost automatically as soon as fullscreen ends.
    /// </summary>
    private void RefreshTopmostPolicy()
    {
        if (_appServices == null || _dockBehavior == null || _enhancedWindowHandle == IntPtr.Zero)
            return;

        _foregroundFullscreen = WindowEnvironment.IsForegroundFullscreen(_enhancedWindowHandle);
        bool requested = _appServices.AppearanceService.GetAlwaysOnTop();
        _dockBehavior.SetTopmost(requested && !_foregroundFullscreen);
    }

    private void PollEdgeAutoHide()
    {
        if (_appServices == null || _enhancedWindowHandle == IntPtr.Zero)
            return;

        // Edge docking intentionally uses the fixed/anchored positioning mode.
        // Dynamic positioning remains freely draggable and is restored unchanged
        // when this option is disabled or the user switches to Dynamic mode.
        bool requested = _appServices.AppearanceService.GetAutoHideAtScreenEdge() &&
                         !_appServices.PositioningService.IsDynamicPositioning();

        if (!requested)
        {
            if (_edgeAutoHideActive)
                DeactivateEdgeAutoHide();
            return;
        }

        if (!_edgeAutoHideActive)
        {
            ActivateEdgeAutoHide();
            return;
        }

        RefreshEdgeGeometryIfNeeded();

        // Do not let touching a screen edge summon the dock over a fullscreen
        // game/video. It stays hidden until fullscreen ends, then behaves normally.
        if (_foregroundFullscreen)
        {
            AnimateEdgeDock(show: false);
            return;
        }

        if (!User32.GetCursorPos(out POINT cursor))
            return;

        bool pointerKeepsOpen;
        if (_edgeShown)
        {
            pointerKeepsOpen = IsCursorInsideCurrentDock(cursor) || IsPointerOverPreview();
        }
        else
        {
            pointerKeepsOpen = IsCursorInEdgeHotZone(cursor);
        }

        if (pointerKeepsOpen)
        {
            _edgeHideAfterUtc = DateTime.UtcNow.AddMilliseconds(EdgeHideDelayMs);
            AnimateEdgeDock(show: true);
        }
        else if (_edgeShown && DateTime.UtcNow >= _edgeHideAfterUtc)
        {
            AnimateEdgeDock(show: false);
        }
    }

    private void ActivateEdgeAutoHide()
    {
        if (_appServices == null) return;

        // Start from the user's normal static anchor/alignment, then snap only
        // the cross-axis to the physical screen edge.
        ApplyDockPosition(force: true);
        if (!ComputeEdgeGeometry(Position))
            return;

        _edgeAutoHideActive = true;
        _edgeShown = true;
        SetEdgePosition(_edgeVisiblePosition);
        _edgeHideAfterUtc = DateTime.UtcNow.AddMilliseconds(800);
    }

    private void DeactivateEdgeAutoHide()
    {
        _edgeAnimationTimer?.Stop();
        _edgeAutoHideActive = false;
        _edgeShown = true;
        // Restore the exact normal position, including the user's configured
        // screen-edge spacing, rather than leaving the dock snapped to zero.
        ApplyDockPosition(force: true);
    }

    private bool ComputeEdgeGeometry(PixelPoint anchorPosition)
    {
        if (_appServices == null ||
            !WindowEnvironment.TryGetMonitorRect(_enhancedWindowHandle, out RECT monitor) ||
            !WindowEnvironment.TryGetWindowRect(_enhancedWindowHandle, out RECT window))
            return false;

        int width = Math.Max(1, window.Right - window.Left);
        int height = Math.Max(1, window.Bottom - window.Top);
        bool verticalDock = _appServices.AppearanceService.GetVerticalDock();

        int visibleX;
        int visibleY;
        int hiddenX;
        int hiddenY;

        if (verticalDock)
        {
            DockHorizontalAnchor anchor = _appServices.PositioningService.GetHorizontalAnchor();
            bool left = anchor switch
            {
                DockHorizontalAnchor.LEFT => true,
                DockHorizontalAnchor.RIGHT => false,
                _ => anchorPosition.X + (width / 2) < monitor.Left + ((monitor.Right - monitor.Left) / 2)
            };

            visibleY = Math.Clamp(anchorPosition.Y, monitor.Top, Math.Max(monitor.Top, monitor.Bottom - height));
            hiddenY = visibleY;
            if (left)
            {
                _edgeSide = EdgeSide.Left;
                visibleX = monitor.Left;
                hiddenX = monitor.Left - width + EdgeRevealThickness;
            }
            else
            {
                _edgeSide = EdgeSide.Right;
                visibleX = monitor.Right - width;
                hiddenX = monitor.Right - EdgeRevealThickness;
            }
        }
        else
        {
            DockVerticalAnchor anchor = _appServices.PositioningService.GetVerticalAnchor();
            bool top = anchor switch
            {
                DockVerticalAnchor.TOP => true,
                DockVerticalAnchor.DOWN => false,
                _ => anchorPosition.Y + (height / 2) < monitor.Top + ((monitor.Bottom - monitor.Top) / 2)
            };

            visibleX = Math.Clamp(anchorPosition.X, monitor.Left, Math.Max(monitor.Left, monitor.Right - width));
            hiddenX = visibleX;
            if (top)
            {
                _edgeSide = EdgeSide.Top;
                visibleY = monitor.Top;
                hiddenY = monitor.Top - height + EdgeRevealThickness;
            }
            else
            {
                _edgeSide = EdgeSide.Bottom;
                visibleY = monitor.Bottom - height;
                hiddenY = monitor.Bottom - EdgeRevealThickness;
            }
        }

        _edgeMonitorRect = monitor;
        _edgeWindowWidth = width;
        _edgeWindowHeight = height;
        _edgeVisiblePosition = new PixelPoint(visibleX, visibleY);
        _edgeHiddenPosition = new PixelPoint(hiddenX, hiddenY);
        return true;
    }

    private void RefreshEdgeGeometryIfNeeded()
    {
        if (!_edgeAutoHideActive || _edgeAnimationTimer?.IsEnabled == true)
            return;

        if (!WindowEnvironment.TryGetWindowRect(_enhancedWindowHandle, out RECT window) ||
            !WindowEnvironment.TryGetMonitorRect(_enhancedWindowHandle, out RECT monitor))
            return;

        int width = Math.Max(1, window.Right - window.Left);
        int height = Math.Max(1, window.Bottom - window.Top);
        bool geometryChanged = width != _edgeWindowWidth ||
                               height != _edgeWindowHeight ||
                               !RectsEqual(monitor, _edgeMonitorRect);

        bool atVisible = IsNear(Position, _edgeVisiblePosition);
        bool atHidden = IsNear(Position, _edgeHiddenPosition);
        bool externallyRepositioned = !atVisible && !atHidden;

        if (!geometryChanged && !externallyRepositioned)
            return;

        // If only the window size changed while hidden, use the stored visible
        // position as the along-edge anchor; otherwise the new Position came
        // from the user's positioning settings and is the desired anchor.
        PixelPoint anchor = externallyRepositioned ? Position : _edgeVisiblePosition;
        if (ComputeEdgeGeometry(anchor))
            SetEdgePosition(_edgeShown ? _edgeVisiblePosition : _edgeHiddenPosition);
    }

    private bool IsCursorInEdgeHotZone(POINT cursor)
    {
        return _edgeSide switch
        {
            EdgeSide.Top =>
                cursor.Y >= _edgeMonitorRect.Top &&
                cursor.Y <= _edgeMonitorRect.Top + EdgeRevealThickness &&
                cursor.X >= _edgeVisiblePosition.X &&
                cursor.X <= _edgeVisiblePosition.X + _edgeWindowWidth,

            EdgeSide.Bottom =>
                cursor.Y >= _edgeMonitorRect.Bottom - EdgeRevealThickness &&
                cursor.Y < _edgeMonitorRect.Bottom &&
                cursor.X >= _edgeVisiblePosition.X &&
                cursor.X <= _edgeVisiblePosition.X + _edgeWindowWidth,

            EdgeSide.Left =>
                cursor.X >= _edgeMonitorRect.Left &&
                cursor.X <= _edgeMonitorRect.Left + EdgeRevealThickness &&
                cursor.Y >= _edgeVisiblePosition.Y &&
                cursor.Y <= _edgeVisiblePosition.Y + _edgeWindowHeight,

            EdgeSide.Right =>
                cursor.X >= _edgeMonitorRect.Right - EdgeRevealThickness &&
                cursor.X < _edgeMonitorRect.Right &&
                cursor.Y >= _edgeVisiblePosition.Y &&
                cursor.Y <= _edgeVisiblePosition.Y + _edgeWindowHeight,

            _ => false
        };
    }

    private bool IsCursorInsideCurrentDock(POINT cursor)
    {
        if (!WindowEnvironment.TryGetWindowRect(_enhancedWindowHandle, out RECT rect))
            return false;

        const int tolerance = 2;
        return cursor.X >= rect.Left - tolerance &&
               cursor.X <= rect.Right + tolerance &&
               cursor.Y >= rect.Top - tolerance &&
               cursor.Y <= rect.Bottom + tolerance;
    }

    private void AnimateEdgeDock(bool show)
    {
        if (!_edgeAutoHideActive || _edgeAnimationTimer == null)
            return;
        if (_edgeShown == show && _edgeAnimationTimer.IsEnabled == false)
            return;

        PixelPoint target = show ? _edgeVisiblePosition : _edgeHiddenPosition;
        if (Position == target)
        {
            _edgeShown = show;
            _edgeAnimationTimer.Stop();
            return;
        }

        // If the same target is already being animated toward, keep it.
        if (_edgeAnimationTimer.IsEnabled && _edgeAnimationTo == target)
            return;

        _edgeShown = show;
        _edgeAnimationFrom = Position;
        _edgeAnimationTo = target;
        _edgeAnimationStartedUtc = DateTime.UtcNow;
        _edgeAnimationTimer.Start();
    }

    private void TickEdgeAnimation()
    {
        if (_edgeAnimationTimer == null)
            return;

        double elapsed = (DateTime.UtcNow - _edgeAnimationStartedUtc).TotalMilliseconds;
        double t = Math.Clamp(elapsed / EdgeAnimationMs, 0.0, 1.0);
        // Cubic ease-out: quick initial response with a soft stop at the edge.
        double eased = 1.0 - Math.Pow(1.0 - t, 3.0);

        int x = (int)Math.Round(_edgeAnimationFrom.X + ((_edgeAnimationTo.X - _edgeAnimationFrom.X) * eased));
        int y = (int)Math.Round(_edgeAnimationFrom.Y + ((_edgeAnimationTo.Y - _edgeAnimationFrom.Y) * eased));
        SetEdgePosition(new PixelPoint(x, y));

        if (t >= 1.0)
        {
            SetEdgePosition(_edgeAnimationTo);
            _edgeAnimationTimer.Stop();
        }
    }

    private void SetEdgePosition(PixelPoint position)
    {
        if (Position != position)
            Position = position;
    }

    private static bool IsNear(PixelPoint a, PixelPoint b)
        => Math.Abs(a.X - b.X) <= 2 && Math.Abs(a.Y - b.Y) <= 2;

    private static bool RectsEqual(RECT a, RECT b)
        => a.Left == b.Left && a.Top == b.Top && a.Right == b.Right && a.Bottom == b.Bottom;
}
