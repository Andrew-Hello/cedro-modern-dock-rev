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
    private RECT _edgeWorkRect;
    private int _edgeBottomBoundary;
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

        ClearWindowRegion();
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
        ClearWindowRegion();
        _enhancedWindowHandle = IntPtr.Zero;
    }

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

        // Dynamic mode stays free until the user explicitly drags close enough
        // to any of the four monitor edges. Static mode remains anchored.
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
        ApplyDockPosition(force: true);
    }

    private bool ComputeStaticEdgeGeometry(PixelPoint anchorPosition)
    {
        if (_appServices == null ||
            !TryGetCurrentWindowMetrics(out RECT monitor, out RECT work, out int width, out int height))
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

        return ApplyEdgeGeometry(monitor, work, width, height, side, offset);
    }

    private bool ComputeDynamicEdgeGeometryFromSavedState()
    {
        if (_appServices == null ||
            !TryGetCurrentWindowMetrics(out RECT monitor, out RECT work, out int width, out int height))
            return false;

        if (!TryDecodeEdgeSide(_appServices.AppearanceService.GetDynamicEdgeSide(), out EdgeSide side))
            return false;

        // Dynamic docking intentionally accepts all four edges regardless of
        // whether the dock's icon layout itself is horizontal or vertical.
        return ApplyEdgeGeometry(
            monitor,
            work,
            width,
            height,
            side,
            _appServices.AppearanceService.GetDynamicEdgeOffset());
    }

    private bool ApplyEdgeGeometry(
        RECT monitor,
        RECT work,
        int width,
        int height,
        EdgeSide side,
        int alongOffset)
    {
        int visibleX;
        int visibleY;
        int hiddenX;
        int hiddenY;
        int effectiveBottom = MonitorWorkArea.GetEffectiveBottom(monitor, work);

        switch (side)
        {
            case EdgeSide.Top:
                visibleX = Math.Clamp(monitor.Left + alongOffset, monitor.Left, Math.Max(monitor.Left, monitor.Right - width));
                visibleY = monitor.Top;
                hiddenX = visibleX;
                hiddenY = monitor.Top - height + EdgeTailThickness;
                break;

            case EdgeSide.Bottom:
                // Bottom is special: when a normal taskbar reserves the bottom
                // work area, the dock treats the taskbar's top edge as its edge.
                visibleX = Math.Clamp(monitor.Left + alongOffset, monitor.Left, Math.Max(monitor.Left, monitor.Right - width));
                visibleY = effectiveBottom - height;
                hiddenX = visibleX;
                hiddenY = effectiveBottom - EdgeTailThickness;
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
        _edgeWorkRect = work;
        _edgeBottomBoundary = effectiveBottom;
        _edgeWindowWidth = width;
        _edgeWindowHeight = height;
        _edgeVisiblePosition = new PixelPoint(visibleX, visibleY);
        _edgeHiddenPosition = new PixelPoint(hiddenX, hiddenY);
        ConfigureEdgeTail(side);
        return true;
    }

    private bool TryGetCurrentWindowMetrics(
        out RECT monitor,
        out RECT work,
        out int width,
        out int height)
    {
        monitor = default;
        work = default;
        width = height = 0;
        if (!MonitorWorkArea.TryGet(_enhancedWindowHandle, out monitor, out work) ||
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
    /// After a Dynamic-mode drag ends, choose the nearest of all four edges.
    /// The edge is only accepted inside the magnetic threshold. Bottom distance
    /// is measured against the taskbar-aware work-area boundary rather than the
    /// physical monitor bottom.
    /// </summary>
    private void TrySnapDynamicDockAfterDrag()
    {
        if (_appServices == null || _enhancedWindowHandle == IntPtr.Zero)
            return;
        if (!_appServices.PositioningService.IsDynamicPositioning() ||
            !_appServices.AppearanceService.GetAutoHideAtScreenEdge() ||
            _edgeAutoHideActive || _dynamicSnapInProgress)
            return;
        if (!MonitorWorkArea.TryGet(_enhancedWindowHandle, out RECT monitor, out RECT work) ||
            !WindowEnvironment.TryGetWindowRect(_enhancedWindowHandle, out RECT window))
            return;

        int width = Math.Max(1, window.Right - window.Left);
        int height = Math.Max(1, window.Bottom - window.Top);
        int effectiveBottom = MonitorWorkArea.GetEffectiveBottom(monitor, work);

        int topDistance = Math.Abs(window.Top - monitor.Top);
        int bottomDistance = Math.Abs(effectiveBottom - window.Bottom);
        int leftDistance = Math.Abs(window.Left - monitor.Left);
        int rightDistance = Math.Abs(monitor.Right - window.Right);

        EdgeSide side = EdgeSide.Top;
        int distance = topDistance;
        if (bottomDistance < distance)
        {
            side = EdgeSide.Bottom;
            distance = bottomDistance;
        }
        if (leftDistance < distance)
        {
            side = EdgeSide.Left;
            distance = leftDistance;
        }
        if (rightDistance < distance)
        {
            side = EdgeSide.Right;
            distance = rightDistance;
        }

        if (distance > DynamicEdgeSnapDistance)
        {
            _appServices.AppearanceService.SetDynamicEdgeDockState(false, 0, 0);
            return;
        }

        int visibleX;
        int visibleY;
        int offset;

        switch (side)
        {
            case EdgeSide.Top:
                visibleX = Math.Clamp(window.Left, monitor.Left, Math.Max(monitor.Left, monitor.Right - width));
                visibleY = monitor.Top;
                offset = visibleX - monitor.Left;
                break;

            case EdgeSide.Bottom:
                visibleX = Math.Clamp(window.Left, monitor.Left, Math.Max(monitor.Left, monitor.Right - width));
                visibleY = effectiveBottom - height;
                offset = visibleX - monitor.Left;
                break;

            case EdgeSide.Left:
                visibleX = monitor.Left;
                visibleY = Math.Clamp(window.Top, monitor.Top, Math.Max(monitor.Top, monitor.Bottom - height));
                offset = visibleY - monitor.Top;
                break;

            case EdgeSide.Right:
                visibleX = monitor.Right - width;
                visibleY = Math.Clamp(window.Top, monitor.Top, Math.Max(monitor.Top, monitor.Bottom - height));
                offset = visibleY - monitor.Top;
                break;

            default:
                return;
        }

        _dynamicSnapInProgress = true;
        try
        {
            _appServices.AppearanceService.SetDynamicEdgeDockState(true, EncodeEdgeSide(side), offset);

            // Persist the expanded/safe anchor, never an offscreen hidden point.
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

        if (!TryGetCurrentWindowMetrics(out RECT monitor, out RECT work, out int width, out int height))
            return;

        bool geometryChanged = width != _edgeWindowWidth ||
                               height != _edgeWindowHeight ||
                               !RectsEqual(monitor, _edgeMonitorRect) ||
                               !RectsEqual(work, _edgeWorkRect);

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
        const int halfExtra = 4;
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
                cursor.Y >= _edgeBottomBoundary - EdgeTailThickness - halfExtra && cursor.Y < _edgeBottomBoundary &&
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

        // The full rectangular region is restored during animation; once fully
        // hidden we clip the native window to the tail so invisible portions do
        // not block the desktop or the taskbar.
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

        if (_edgeAutoHideActive)
            _positionPersistTimer.Stop();
    }

    private void SetDockChromeVisible(bool dockVisible)
    {
        if (dockVisible)
            ClearWindowRegion();

        DockBar.Opacity = dockVisible ? 1.0 : 0.0;
        DockBar.IsHitTestVisible = dockVisible;
        EdgeTail.IsVisible = !dockVisible && _edgeAutoHideActive;

        if (!dockVisible && _edgeAutoHideActive)
            ApplyTailOnlyWindowRegion();
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

    /// <summary>
    /// Restricts the native HWND to the visible tail while hidden. This is
    /// especially important for bottom docking above the taskbar: the offscreen
    /// transparent part of the dock must not cover or intercept the taskbar.
    /// </summary>
    private void ApplyTailOnlyWindowRegion()
    {
        if (_enhancedWindowHandle == IntPtr.Zero || _edgeWindowWidth <= 0 || _edgeWindowHeight <= 0)
            return;

        int x;
        int y;
        int width;
        int height;

        if (_edgeSide is EdgeSide.Top or EdgeSide.Bottom)
        {
            width = Math.Min(EdgeTailLength, _edgeWindowWidth);
            height = Math.Min(EdgeTailThickness, _edgeWindowHeight);
            x = Math.Max(0, (_edgeWindowWidth - width) / 2);
            y = _edgeSide == EdgeSide.Top ? Math.Max(0, _edgeWindowHeight - height) : 0;
        }
        else
        {
            width = Math.Min(EdgeTailThickness, _edgeWindowWidth);
            height = Math.Min(EdgeTailLength, _edgeWindowHeight);
            x = _edgeSide == EdgeSide.Left ? Math.Max(0, _edgeWindowWidth - width) : 0;
            y = Math.Max(0, (_edgeWindowHeight - height) / 2);
        }

        IntPtr region = User32.CreateRoundRectRgn(
            x,
            y,
            x + width + 1,
            y + height + 1,
            EdgeTailThickness,
            EdgeTailThickness);
        if (region == IntPtr.Zero)
            return;

        // On success SetWindowRgn transfers ownership of HRGN to Windows.
        if (User32.SetWindowRgn(_enhancedWindowHandle, region, true) == 0)
            Gdi32.DeleteObject(region);
    }

    private void ClearWindowRegion()
    {
        if (_enhancedWindowHandle != IntPtr.Zero)
            User32.SetWindowRgn(_enhancedWindowHandle, IntPtr.Zero, true);
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
