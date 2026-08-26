using System;
using Avalonia;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Platform;
using Avalonia.Threading;
using CedroModernDock.Core.Models;
using CedroModernDock.Infrastructure.Windows.Native;

namespace CedroModernDock.Views;

public partial class MainWindow
{
    private const int EdgeTailThickness = 10;
    private const int EdgeTailLength = 48;
    private const int EdgeHideDelayMs = 450;
    private const int EdgeAnimationMs = 130;
    private const int DynamicEdgeSnapDistance = 24;
    private const int DynamicSnapDebounceMs = 130;

    private IntPtr _enhancedWindowHandle;
    private DispatcherTimer? _topmostPolicyTimer;
    private DispatcherTimer? _edgePollTimer;
    private DispatcherTimer? _edgeAnimationTimer;
    private DispatcherTimer? _dynamicSnapTimer;

    private bool _foregroundFullscreen;
    private bool _edgeAutoHideActive;
    private bool _edgeShown = true;
    private bool _dynamicSnapInProgress;
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

        // Fires before the original 200ms position-persist timer. In Dynamic
        // mode this gives edge snapping first chance after a native move-drag,
        // so a near-edge release becomes a docked state instead of being saved
        // as an arbitrary free coordinate.
        _dynamicSnapTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(DynamicSnapDebounceMs)
        };
        _dynamicSnapTimer.Tick += (_, _) =>
        {
            _dynamicSnapTimer.Stop();
            TrySnapDynamicDockAfterDrag();
        };
        PositionChanged += OnEnhancedDockPositionChanged;

        SetDockChromeVisible(true);
        RefreshTopmostPolicy();
        PollEdgeAutoHide();
        _topmostPolicyTimer.Start();
        _edgePollTimer.Start();
    }

    private void OnEnhancedWindowClosed(object? sender, EventArgs e)
    {
        PositionChanged -= OnEnhancedDockPositionChanged;
        _topmostPolicyTimer?.Stop();
        _edgePollTimer?.Stop();
        _edgeAnimationTimer?.Stop();
        _dynamicSnapTimer?.Stop();
        _topmostPolicyTimer = null;
        _edgePollTimer = null;
        _edgeAnimationTimer = null;
        _dynamicSnapTimer = null;
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

        bool requested = _appServices.AppearanceService.GetAutoHideAtScreenEdge();
        bool dynamic = _appServices.PositioningService.IsDynamicPositioning();

        // In Dynamic mode the feature is dormant until the user explicitly
        // drags close to a compatible edge. Static mode remains always docked
        // according to its anchor while the setting is enabled.
        if (requested && dynamic && !_appServices.AppearanceService.GetDynamicEdgeDocked())
            requested = false;

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

        bool pointerKeepsOpen = _edgeShown
            ? IsCursorInsideCurrentDock(cursor) || IsPointerOverPreview()
            : IsCursorInTailHotZone(cursor);

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

        bool dynamic = _appServices.PositioningService.IsDynamicPositioning();
        ApplyDockPosition(force: true);

        bool geometryReady = dynamic
            ? ComputeDynamicEdgeGeometryFromSavedState()
            : ComputeStaticEdgeGeometry(Position);
        if (!geometryReady)
            return;

        _edgeAutoHideActive = true;
        _edgeShown = true;
        SetDockChromeVisible(true);
        SetEdgePosition(_edgeVisiblePosition);
        _edgeHideAfterUtc = DateTime.UtcNow.AddMilliseconds(800);
    }

    private void DeactivateEdgeAutoHide()
    {
        _edgeAnimationTimer?.Stop();
        _edgeAutoHideActive = false;
        _edgeShown = true;
        SetDockChromeVisible(true);
        // Restore the exact normal/saved visible position instead of leaving
        // the dock partially outside the screen.
        ApplyDockPosition(force: true);
    }

    /// <summary>
    /// Computes the edge geometry used by Static positioning. The selected
    /// alignment chooses the edge; the current anchored position supplies the
    /// along-edge location.
    /// </summary>
    private bool ComputeStaticEdgeGeometry(PixelPoint anchorPosition)
    {
        if (_appServices == null ||
            !TryGetCurrentWindowMetrics(out RECT monitor, out int width, out int height))
            return false;

        bool verticalDock = _appServices.AppearanceService.GetVerticalDock();
        EdgeSide side;
        int offset;

        if (verticalDock)
        {
            DockHorizontalAnchor anchor = _appServices.PositioningService.GetHorizontalAnchor();
            bool left = anchor switch
            {
                DockHorizontalAnchor.LEFT => true,
                DockHorizontalAnchor.RIGHT => false,
                _ => anchorPosition.X + (width / 2) < monitor.Left + ((monitor.Right - monitor.Left) / 2)
            };
            side = left ? EdgeSide.Left : EdgeSide.Right;
            offset = anchorPosition.Y - monitor.Top;
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
            side = top ? EdgeSide.Top : EdgeSide.Bottom;
            offset = anchorPosition.X - monitor.Left;
        }

        return ApplyEdgeGeometry(monitor, width, height, side, offset);
    }

    private bool ComputeDynamicEdgeGeometryFromSavedState()
    {
        if (_appServices == null ||
            !TryGetCurrentWindowMetrics(out RECT monitor, out int width, out int height))
            return false;

        if (!TryDecodeEdgeSide(_appServices.AppearanceService.GetDynamicEdgeSide(), out EdgeSide side))
            return false;

        bool verticalDock = _appServices.AppearanceService.GetVerticalDock();
        if ((verticalDock && side is not (EdgeSide.Left or EdgeSide.Right)) ||
            (!verticalDock && side is not (EdgeSide.Top or EdgeSide.Bottom)))
        {
            _appServices.AppearanceService.SetDynamicEdgeDockState(false, 0, 0);
            return false;
        }

        return ApplyEdgeGeometry(
            monitor,
            width,
            height,
            side,
            _appServices.AppearanceService.GetDynamicEdgeOffset());
    }

    private bool ApplyEdgeGeometry(RECT monitor, int width, int height, EdgeSide side, int alongOffset)
    {
        int visibleX;
        int visibleY;
        int hiddenX;
        int hiddenY;

        switch (side)
        {
            case EdgeSide.Top:
                visibleX = Math.Clamp(monitor.Left + alongOffset, monitor.Left, Math.Max(monitor.Left, monitor.Right - width));
                visibleY = monitor.Top;
                hiddenX = visibleX;
                hiddenY = monitor.Top - height + EdgeTailThickness;
                break;

            case EdgeSide.Bottom:
                visibleX = Math.Clamp(monitor.Left + alongOffset, monitor.Left, Math.Max(monitor.Left, monitor.Right - width));
                visibleY = monitor.Bottom - height;
                hiddenX = visibleX;
                hiddenY = monitor.Bottom - EdgeTailThickness;
                break;

            case EdgeSide.Left:
                visibleX = monitor.Left;
                visibleY = Math.Clamp(monitor.Top + alongOffset, monitor.Top, Math.Max(monitor.Top, monitor.Bottom - height));
                hiddenX = monitor.Left - width + EdgeTailThickness;
                hiddenY = visibleY;
                break;

            case EdgeSide.Right:
                visibleX = monitor.Right - width;
                visibleY = Math.Clamp(monitor.Top + alongOffset, monitor.Top, Math.Max(monitor.Top, monitor.Bottom - height));
                hiddenX = monitor.Right - EdgeTailThickness;
                hiddenY = visibleY;
                break;

            default:
                return false;
        }

        _edgeSide = side;
        _edgeMonitorRect = monitor;
        _edgeWindowWidth = width;
        _edgeWindowHeight = height;
        _edgeVisiblePosition = new PixelPoint(visibleX, visibleY);
        _edgeHiddenPosition = new PixelPoint(hiddenX, hiddenY);
        ConfigureEdgeTail(side);
        return true;
    }

    private bool TryGetCurrentWindowMetrics(out RECT monitor, out int width, out int height)
    {
        monitor = default;
        width = height = 0;
        if (!WindowEnvironment.TryGetMonitorRect(_enhancedWindowHandle, out monitor) ||
            !WindowEnvironment.TryGetWindowRect(_enhancedWindowHandle, out RECT window))
            return false;

        width = Math.Max(1, window.Right - window.Left);
        height = Math.Max(1, window.Bottom - window.Top);
        return true;
    }

    private void OnEnhancedDockPositionChanged(object? sender, PixelPointEventArgs e)
    {
        if (_appServices == null || _dynamicSnapTimer == null || _dynamicSnapInProgress)
            return;
        if (!_appServices.PositioningService.IsDynamicPositioning())
            return;
        if (!_appServices.AppearanceService.GetAutoHideAtScreenEdge())
            return;
        if (_edgeAutoHideActive)
            return;

        _dynamicSnapTimer.Stop();
        _dynamicSnapTimer.Start();
    }

    /// <summary>
    /// Called shortly after a Dynamic-mode native move-drag stops. Horizontal
    /// docks magnetically snap to top/bottom; vertical docks snap to left/right.
    /// A free-position release simply clears any previous edge-docked state.
    /// </summary>
    private void TrySnapDynamicDockAfterDrag()
    {
        if (_appServices == null || _enhancedWindowHandle == IntPtr.Zero)
            return;
        if (!_appServices.PositioningService.IsDynamicPositioning() ||
            !_appServices.AppearanceService.GetAutoHideAtScreenEdge() ||
            _edgeAutoHideActive || _dynamicSnapInProgress)
            return;
        if (!WindowEnvironment.TryGetMonitorRect(_enhancedWindowHandle, out RECT monitor) ||
            !WindowEnvironment.TryGetWindowRect(_enhancedWindowHandle, out RECT window))
            return;

        bool verticalDock = _appServices.AppearanceService.GetVerticalDock();
        EdgeSide side;
        int distance;
        int visibleX;
        int visibleY;
        int offset;

        if (verticalDock)
        {
            int leftDistance = Math.Abs(window.Left - monitor.Left);
            int rightDistance = Math.Abs(monitor.Right - window.Right);
            distance = Math.Min(leftDistance, rightDistance);
            if (distance > DynamicEdgeSnapDistance)
            {
                _appServices.AppearanceService.SetDynamicEdgeDockState(false, 0, 0);
                return;
            }

            side = leftDistance <= rightDistance ? EdgeSide.Left : EdgeSide.Right;
            visibleX = side == EdgeSide.Left ? monitor.Left : monitor.Right - (window.Right - window.Left);
            visibleY = Math.Clamp(window.Top, monitor.Top, Math.Max(monitor.Top, monitor.Bottom - (window.Bottom - window.Top)));
            offset = visibleY - monitor.Top;
        }
        else
        {
            int topDistance = Math.Abs(window.Top - monitor.Top);
            int bottomDistance = Math.Abs(monitor.Bottom - window.Bottom);
            distance = Math.Min(topDistance, bottomDistance);
            if (distance > DynamicEdgeSnapDistance)
            {
                _appServices.AppearanceService.SetDynamicEdgeDockState(false, 0, 0);
                return;
            }

            side = topDistance <= bottomDistance ? EdgeSide.Top : EdgeSide.Bottom;
            visibleX = Math.Clamp(window.Left, monitor.Left, Math.Max(monitor.Left, monitor.Right - (window.Right - window.Left)));
            visibleY = side == EdgeSide.Top ? monitor.Top : monitor.Bottom - (window.Bottom - window.Top);
            offset = visibleX - monitor.Left;
        }

        _dynamicSnapInProgress = true;
        try
        {
            int persistedSide = EncodeEdgeSide(side);
            _appServices.AppearanceService.SetDynamicEdgeDockState(true, persistedSide, offset);

            // Persist the visible edge position, never the later hidden/offscreen
            // animation coordinate. This makes restarts deterministic.
            _appServices.DockService.SetDockPosition(visibleX, visibleY);
            _positionPersistTimer.Stop();
            Position = new PixelPoint(visibleX, visibleY);
            _positionPersistTimer.Stop();

            if (ComputeDynamicEdgeGeometryFromSavedState())
            {
                _edgeAutoHideActive = true;
                _edgeShown = true;
                SetDockChromeVisible(true);
                SetEdgePosition(_edgeVisiblePosition);
                _edgeHideAfterUtc = DateTime.UtcNow.AddMilliseconds(650);
            }
        }
        finally
        {
            _dynamicSnapInProgress = false;
        }
    }

    /// <summary>
    /// Called from MainWindow before a user starts dragging an already docked
    /// Dynamic dock. The dock becomes free immediately; releasing near an edge
    /// snaps it again, while releasing inside the screen leaves it undocked.
    /// </summary>
    private void PrepareDynamicEdgeDockForDrag()
    {
        if (_appServices == null || !_appServices.PositioningService.IsDynamicPositioning())
            return;
        if (!_edgeAutoHideActive && !_appServices.AppearanceService.GetDynamicEdgeDocked())
            return;

        _edgeAnimationTimer?.Stop();
        _dynamicSnapTimer?.Stop();
        _edgeAutoHideActive = false;
        _edgeShown = true;
        SetDockChromeVisible(true);
        _appServices.AppearanceService.SetDynamicEdgeDockState(false, 0, 0);
        _positionPersistTimer.Stop();
    }

    private void RefreshEdgeGeometryIfNeeded()
    {
        if (!_edgeAutoHideActive || _edgeAnimationTimer?.IsEnabled == true || _appServices == null)
            return;

        if (!TryGetCurrentWindowMetrics(out RECT monitor, out int width, out int height))
            return;

        bool geometryChanged = width != _edgeWindowWidth ||
                               height != _edgeWindowHeight ||
                               !RectsEqual(monitor, _edgeMonitorRect);

        bool atVisible = IsNear(Position, _edgeVisiblePosition);
        bool atHidden = IsNear(Position, _edgeHiddenPosition);
        bool externallyRepositioned = !atVisible && !atHidden;

        if (!geometryChanged && !externallyRepositioned)
            return;

        bool dynamic = _appServices.PositioningService.IsDynamicPositioning();
        bool ok;
        if (dynamic)
        {
            ok = ComputeDynamicEdgeGeometryFromSavedState();
        }
        else
        {
            PixelPoint anchor = externallyRepositioned ? Position : _edgeVisiblePosition;
            ok = ComputeStaticEdgeGeometry(anchor);
        }

        if (ok)
            SetEdgePosition(_edgeShown ? _edgeVisiblePosition : _edgeHiddenPosition);
    }

    private bool IsCursorInTailHotZone(POINT cursor)
    {
        int halfExtra = 4;
        int tailLeft = _edgeVisiblePosition.X + Math.Max(0, (_edgeWindowWidth - EdgeTailLength) / 2) - halfExtra;
        int tailTop = _edgeVisiblePosition.Y + Math.Max(0, (_edgeWindowHeight - EdgeTailLength) / 2) - halfExtra;
        int horizontalLength = Math.Min(EdgeTailLength, _edgeWindowWidth) + (halfExtra * 2);
        int verticalLength = Math.Min(EdgeTailLength, _edgeWindowHeight) + (halfExtra * 2);

        return _edgeSide switch
        {
            EdgeSide.Top =>
                cursor.Y >= _edgeMonitorRect.Top && cursor.Y <= _edgeMonitorRect.Top + EdgeTailThickness + halfExtra &&
                cursor.X >= tailLeft && cursor.X <= tailLeft + horizontalLength,

            EdgeSide.Bottom =>
                cursor.Y >= _edgeMonitorRect.Bottom - EdgeTailThickness - halfExtra && cursor.Y < _edgeMonitorRect.Bottom &&
                cursor.X >= tailLeft && cursor.X <= tailLeft + horizontalLength,

            EdgeSide.Left =>
                cursor.X >= _edgeMonitorRect.Left && cursor.X <= _edgeMonitorRect.Left + EdgeTailThickness + halfExtra &&
                cursor.Y >= tailTop && cursor.Y <= tailTop + verticalLength,

            EdgeSide.Right =>
                cursor.X >= _edgeMonitorRect.Right - EdgeTailThickness - halfExtra && cursor.X < _edgeMonitorRect.Right &&
                cursor.Y >= tailTop && cursor.Y <= tailTop + verticalLength,

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
            SetDockChromeVisible(show);
            return;
        }

        if (_edgeAnimationTimer.IsEnabled && _edgeAnimationTo == target)
            return;

        // The full dock needs to be drawable while sliding out. While sliding
        // in, the tail is already configured and becomes the only visible
        // control once the animation reaches its hidden endpoint.
        SetDockChromeVisible(true);
        if (!show)
            EdgeTail.IsVisible = true;

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
        double eased = 1.0 - Math.Pow(1.0 - t, 3.0);

        int x = (int)Math.Round(_edgeAnimationFrom.X + ((_edgeAnimationTo.X - _edgeAnimationFrom.X) * eased));
        int y = (int)Math.Round(_edgeAnimationFrom.Y + ((_edgeAnimationTo.Y - _edgeAnimationFrom.Y) * eased));
        SetEdgePosition(new PixelPoint(x, y));

        if (t >= 1.0)
        {
            SetEdgePosition(_edgeAnimationTo);
            _edgeAnimationTimer.Stop();
            SetDockChromeVisible(_edgeShown);
        }
    }

    private void SetEdgePosition(PixelPoint position)
    {
        if (Position != position)
            Position = position;

        // MainWindow's legacy Dynamic-position persistence observes all window
        // movement, including our animation. Never let hidden/offscreen positions
        // overwrite the saved visible edge anchor.
        if (_edgeAutoHideActive)
            _positionPersistTimer.Stop();
    }

    private void SetDockChromeVisible(bool dockVisible)
    {
        DockBar.Opacity = dockVisible ? 1.0 : 0.0;
        DockBar.IsHitTestVisible = dockVisible;
        EdgeTail.IsVisible = !dockVisible && _edgeAutoHideActive;
    }

    private void ConfigureEdgeTail(EdgeSide side)
    {
        bool horizontalTail = side is EdgeSide.Top or EdgeSide.Bottom;
        EdgeTail.Width = horizontalTail ? EdgeTailLength : EdgeTailThickness;
        EdgeTail.Height = horizontalTail ? EdgeTailThickness : EdgeTailLength;
        EdgeTailGrip.Width = horizontalTail ? 18 : 2;
        EdgeTailGrip.Height = horizontalTail ? 2 : 18;

        EdgeTail.HorizontalAlignment = side switch
        {
            EdgeSide.Left => HorizontalAlignment.Right,
            EdgeSide.Right => HorizontalAlignment.Left,
            _ => HorizontalAlignment.Center
        };
        EdgeTail.VerticalAlignment = side switch
        {
            EdgeSide.Top => VerticalAlignment.Bottom,
            EdgeSide.Bottom => VerticalAlignment.Top,
            _ => VerticalAlignment.Center
        };
    }

    private void OnEdgeTailPointerEntered(object? sender, PointerEventArgs e)
    {
        if (_foregroundFullscreen) return;
        _edgeHideAfterUtc = DateTime.UtcNow.AddMilliseconds(EdgeHideDelayMs);
        AnimateEdgeDock(show: true);
    }

    private void OnEdgeTailPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_foregroundFullscreen) return;
        e.Handled = true;
        _edgeHideAfterUtc = DateTime.UtcNow.AddMilliseconds(EdgeHideDelayMs);
        AnimateEdgeDock(show: true);
    }

    private static int EncodeEdgeSide(EdgeSide side) => side switch
    {
        EdgeSide.Top => 1,
        EdgeSide.Bottom => 2,
        EdgeSide.Left => 3,
        EdgeSide.Right => 4,
        _ => 0
    };

    private static bool TryDecodeEdgeSide(int value, out EdgeSide side)
    {
        side = value switch
        {
            1 => EdgeSide.Top,
            2 => EdgeSide.Bottom,
            3 => EdgeSide.Left,
            4 => EdgeSide.Right,
            _ => EdgeSide.Top
        };
        return value is >= 1 and <= 4;
    }

    private static bool IsNear(PixelPoint a, PixelPoint b)
        => Math.Abs(a.X - b.X) <= 2 && Math.Abs(a.Y - b.Y) <= 2;

    private static bool RectsEqual(RECT a, RECT b)
        => a.Left == b.Left && a.Top == b.Top && a.Right == b.Right && a.Bottom == b.Bottom;
}
