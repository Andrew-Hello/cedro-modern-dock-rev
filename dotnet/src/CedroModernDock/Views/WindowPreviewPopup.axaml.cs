using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using CedroModernDock.Core.Application;
using CedroModernDock.Core.Domain;
using CedroModernDock.Infrastructure.Windows.Native;

namespace CedroModernDock.Views;

/// <summary>
/// Popup listing a program item's open windows as live DWM thumbnails.
/// The window is fully transparent; thumbnails render beneath the content,
/// so only the row borders/titles are drawn by Avalonia.
/// </summary>
public partial class WindowPreviewPopup : Window
{
    private const int ThumbWidth = 144;
    private const int ThumbHeight = 81;
    private const int MaxRounding = 15;
    // 75% of the popup width (144 thumbnail + 2*6 row padding + 2*4 panel margin).
    private const int TitleMaxWidth = 123;

    private readonly List<(IntPtr Thumb, Border Area)> _rows = new();
    private readonly List<IntPtr> _sources = new();
    private IReadOnlyList<IntPtr> _sourceHandles = Array.Empty<IntPtr>();
    private string _appLabel = "";
    private string _colorRgb = "0, 0, 0, ";
    private double _transparency = 0.3;
    private int _rounding = 12;
    private IntPtr _hwnd;
    private bool _regPostQueued;
    private Button? _positionAnchor;
    private bool _repositionPending;

    public WindowPreviewPopup()
    {
        InitializeComponent();
        // Position only once the window has a real layout size: before the first
        // Show the content tree is not attached (DesiredSize is 0), and after
        // BuildRows the measure is invalidated, so positioning in ShowFor would
        // anchor the popup to the item's edge instead of centering it.
        LayoutUpdated += (_, _) =>
        {
            if (!_repositionPending) return;
            _repositionPending = false;
            if (_positionAnchor is { } anchor)
                PositionNear(anchor);
        };
    }

    /// <summary>Invoked with the row's source HWND when a thumbnail row is clicked.</summary>
    public Action<IntPtr>? ThumbnailClicked { get; set; }

    /// <summary>Invoked when the pointer enters/exits the popup (keeps it open on hover).</summary>
    public Action? PointerEnteredCallback { get; set; }
    public Action? PointerExitedCallback { get; set; }

    /// <summary>Populates rows and positions the popup over <paramref name="anchor"/>.</summary>
    public void ShowFor(IReadOnlyList<WindowInfo> windows, string appLabel,
        string colorRgb, int rounding, double transparency, Button anchor)
    {
        _appLabel = appLabel;
        _colorRgb = colorRgb;
        _rounding = rounding;
        _transparency = transparency;
        _hwnd = IntPtr.Zero;
        _sourceHandles = new List<IntPtr>(windows.Select(w => w.Handle));

        bool wasVisible = IsVisible;
        _positionAnchor = anchor;
        _repositionPending = true;
        BuildRows(windows);
        if (wasVisible) PositionNear(anchor);
        Show();
        if (wasVisible && !_regPostQueued)
        {
            _regPostQueued = true;
            Dispatcher.UIThread.Post(() =>
            {
                _regPostQueued = false;
                RegisterThumbnails();
            }, DispatcherPriority.Loaded);
        }
    }

    private void BuildRows(IReadOnlyList<WindowInfo> windows)
    {
        ClearRows();
        _rows.Clear();
        _sources.Clear();

        var parts = _colorRgb.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        byte r = parts.Length > 0 && byte.TryParse(parts[0], out var rv) ? rv : (byte)0;
        byte g = parts.Length > 1 && byte.TryParse(parts[1], out var gv) ? gv : (byte)0;
        byte b = parts.Length > 2 && byte.TryParse(parts[2], out var bv) ? bv : (byte)0;
        byte alpha = (byte)(_transparency * 255);
        var rowBrush = new SolidColorBrush(Color.FromArgb(alpha, r, g, b));
        int rounding = Math.Min(_rounding, MaxRounding);
        var textBrush = WindowTitleFormatter.IsDarkBackground(_colorRgb)
            ? (IBrush)new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC))
            : new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x44));

        foreach (var window in windows)
        {
            var thumbArea = new Border { Width = ThumbWidth, Height = ThumbHeight, Background = Brushes.Transparent };
            var thumbHost = new Grid { Width = ThumbWidth, Height = ThumbHeight };
            thumbHost.Children.Add(thumbArea);
            AddThumbnailCornerMasks(thumbHost, rowBrush, rounding);
            var title = new TextBlock
            {
                Text = WindowTitleFormatter.Format(window.Title, _appLabel),
                Foreground = textBrush,
                FontSize = 12,
                // Cap the title at 75% of the popup width: long titles must
                // never widen the popup beyond the previews it contains.
                MaxWidth = TitleMaxWidth,
                TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                Margin = new Thickness(0, 4, 0, 4)
            };
            var content = new StackPanel { Orientation = Orientation.Vertical };
            content.Children.Add(thumbHost);
            content.Children.Add(title);

            var row = new Border
            {
                CornerRadius = new CornerRadius(rounding),
                // Same background and transparency as the dock itself.
                Background = rowBrush,
                Padding = new Thickness(6, 6, 6, 0),
                Child = content
            };
            RowsPanel.Children.Add(row);
            row.PointerPressed += (_, _) => ThumbnailClicked?.Invoke(window.Handle);
            _rows.Add((IntPtr.Zero, thumbArea));
            _sources.Add(IntPtr.Zero);
        }
    }

    private void ClearRows()
    {
        foreach (var (thumb, _) in _rows)
            DwmThumbnailInterop.Unregister(thumb);
        RowsPanel.Children.Clear();
    }

    /// <summary>
    /// DWM thumbnails are always rectangular, so rounded corners are faked by
    /// over-painting the four corners with the row background (same color and
    /// transparency as the dock).
    /// </summary>
    private static void AddThumbnailCornerMasks(Grid host, IBrush brush, double radius)
    {
        if (radius <= 0) return;
        host.Children.Add(new Border
        {
            Width = radius, Height = radius, Background = brush,
            CornerRadius = new CornerRadius(radius, 0, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top,
            IsHitTestVisible = false
        });
        host.Children.Add(new Border
        {
            Width = radius, Height = radius, Background = brush,
            CornerRadius = new CornerRadius(0, radius, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top,
            IsHitTestVisible = false
        });
        host.Children.Add(new Border
        {
            Width = radius, Height = radius, Background = brush,
            CornerRadius = new CornerRadius(0, 0, radius, 0),
            HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Bottom,
            IsHitTestVisible = false
        });
        host.Children.Add(new Border
        {
            Width = radius, Height = radius, Background = brush,
            CornerRadius = new CornerRadius(0, 0, 0, radius),
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom,
            IsHitTestVisible = false
        });
    }

    private void PositionNear(Button anchor)
    {
        Measure(Size.Infinity);
        double scale = RenderScaling;
        int w = (int)(DesiredSize.Width * scale);
        int h = (int)(DesiredSize.Height * scale);

        var anchorCenter = anchor.PointToScreen(new Point(anchor.Bounds.Width / 2, anchor.Bounds.Height / 2));

        var screens = Screens;
        var screen = screens.ScreenFromPoint(anchorCenter);
        if (screen is null && screens.All.Count > 0)
            screen = screens.All[0];

        // Gap (DIPs) between the item button and the dock window's own border:
        // the popup must sit outside the dock, not just below the button.
        double gapBelow = 0, gapAbove = 0;
        if (TopLevel.GetTopLevel(anchor) is { } root &&
            anchor.TransformToVisual(root) is { } transform)
        {
            gapBelow = root.Bounds.Height - new Point(0, anchor.Bounds.Height).Transform(transform).Y;
            gapAbove = new Point(0, 0).Transform(transform).Y;
        }

        // Place below the dock by default; above when the dock is in the lower half.
        var anchorBottom = anchor.PointToScreen(new Point(anchor.Bounds.Width / 2, anchor.Bounds.Height));
        int popupX = anchorCenter.X - w / 2;
        int popupY = (int)(anchorBottom.Y + gapBelow * scale) + 6;
        if (screen is not null)
        {
            var work = screen.WorkingArea;
            if (popupY + h > work.Bottom)
            {
                var anchorTop = anchor.PointToScreen(new Point(anchor.Bounds.Width / 2, 0));
                popupY = (int)(anchorTop.Y - gapAbove * scale) - h - 6;
            }

            popupX = Math.Max(work.X + 4, Math.Min(popupX, work.Right - w - 4));
            popupY = Math.Max(work.Y + 4, popupY);
        }
        Position = new PixelPoint(popupX, popupY);
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        _hwnd = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;

        // Never steal focus: same extended styles as DockWindowBehavior.ApplyExtendedStyles.
        if (_hwnd != IntPtr.Zero)
        {
            IntPtr exStylePtr = User32.GetWindowLongPtr(_hwnd, Win32Constants.GWL_EXSTYLE);
            int exStyle = exStylePtr.ToInt32();
            exStyle |= Win32Constants.WS_EX_NOACTIVATE | Win32Constants.WS_EX_TOOLWINDOW;
            exStyle &= ~Win32Constants.WS_EX_APPWINDOW;
            User32.SetWindowLongPtr(_hwnd, Win32Constants.GWL_EXSTYLE, new IntPtr(exStyle));
        }

        // Single registration path: layout must be final before registering
        // thumbnails, so defer to Loaded priority. Source HWNDs were stored by
        // ShowFor (_sourceHandles).
        Dispatcher.UIThread.Post(RegisterThumbnails, DispatcherPriority.Loaded);
    }

    private void RegisterThumbnails()
    {
        if (_hwnd == IntPtr.Zero) return;
        double scale = RenderScaling;

        for (int i = 0; i < _rows.Count && i < _sourceHandles.Count; i++)
        {
            var area = _rows[i].Area;
            var matrix = area.TransformToVisual(this);
            if (matrix == null) continue;
            var bounds = area.Bounds.TransformToAABB(matrix.Value);
            if (bounds.Width <= 0 || bounds.Height <= 0) continue;

            IntPtr sourceHwnd = _sourceHandles[i];
            if (sourceHwnd == IntPtr.Zero) continue;
            if (!DwmThumbnailInterop.Register(_hwnd, sourceHwnd, out IntPtr thumb)) continue;
            _rows[i] = (thumb, area);
            _sources[i] = sourceHwnd;

            if (!DwmThumbnailInterop.QuerySourceSize(thumb, out var srcSize) || srcSize.Cx <= 0 || srcSize.Cy <= 0)
                continue;

            // Fit preserving aspect inside the area (letterbox), physical pixels.
            double areaW = bounds.Width * scale;
            double areaH = bounds.Height * scale;
            double ar = (double)srcSize.Cx / srcSize.Cy;
            double fitW = areaW, fitH = areaW / ar;
            if (fitH > areaH) { fitH = areaH; fitW = areaH * ar; }
            double ox = bounds.X * scale + (areaW - fitW) / 2;
            double oy = bounds.Y * scale + (areaH - fitH) / 2;

            var rect = new DwmThumbnailInterop.RECT
            {
                Left = (int)ox,
                Top = (int)oy,
                Right = (int)Math.Ceiling(ox + fitW),
                Bottom = (int)Math.Ceiling(oy + fitH)
            };
            DwmThumbnailInterop.Update(thumb, rect);
        }
    }

    public void HidePopup()
    {
        ClearRows();
        Hide();
    }

    private void OnPointerEntered(object? sender, PointerEventArgs e) => PointerEnteredCallback?.Invoke();

    private void OnPointerExited(object? sender, PointerEventArgs e) => PointerExitedCallback?.Invoke();
}
