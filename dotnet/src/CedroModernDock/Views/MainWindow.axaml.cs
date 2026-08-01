using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CedroModernDock.Core.Application;
using CedroModernDock.Core.Domain;
using CedroModernDock.Core.Models;
using CedroModernDock.Infrastructure.Windows.Native;
using CedroModernDock.ViewModels;

namespace CedroModernDock.Views;

public partial class MainWindow : Window
{
    private DockWindowBehavior? _dockBehavior;
    private AppServices? _appServices;
    private WindowPreviewPopup? _previewPopup;
    private Button? _hoveredButton;
    private int _previewRequestId;
    private bool _isOverPreview;
    private readonly DispatcherTimer _previewHideDebounce = new() { Interval = TimeSpan.FromMilliseconds(80) };

    public MainWindow()
    {
        InitializeComponent();
        _previewHideDebounce.Tick += (_, _) =>
        {
            _previewHideDebounce.Stop();
            if (!_isOverPreview)
                HidePreview();
        };
    }

    /// <summary>Receives the composed application services from the App composition root.</summary>
    public void SetAppServices(AppServices appServices) => _appServices = appServices;

    /// <summary>
    /// Once the native window is created and shown, grab its HWND and apply
    /// the dock-specific Win32 behavior (no-activate, no-taskbar, Win+D defense).
    /// Also applies the saved dock position from the positioning service.
    /// </summary>
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        IPlatformHandle? handle = this.TryGetPlatformHandle();
        if (handle is null)
        {
            UpdateStatus("ERROR: Could not obtain native window handle");
            return;
        }

        _dockBehavior = new DockWindowBehavior(handle.Handle, UpdateStatus);
        _dockBehavior.Apply();

        // Initialize the dock ViewModel (loads items, starts indicator watcher).
        if (DataContext is MainWindowViewModel vm)
        {
            vm.OpenSettingsAction = () => OpenSettings(vm);
            vm.RepositionAction = () => ApplyDockPosition();
            vm.PreviewDismissAction = HidePreview;
            vm.Initialize();
        }

        ApplyDockPosition(force: true);

        // Static anchors must use the finalized window size, which SizeToContent
        // only produces after the first layout pass. Re-apply once layout settles
        // and whenever the dock content resizes the window.
        if (_appServices?.PositioningService.IsDynamicPositioning() == false)
        {
            SizeChanged += OnDockSizeChanged;
            Dispatcher.UIThread.Post(() => ApplyDockPosition(), DispatcherPriority.Loaded);
        }
    }

    /// <summary>
    /// Applies the dock position from the positioning service. In STATIC mode
    /// this re-anchors the dock to the current screen edge; in DYNAMIC mode the
    /// saved position is only applied once at startup (force) so the user's
    /// drags are never overridden by subsequent refreshes.
    /// </summary>
    private void ApplyDockPosition(bool force = false)
    {
        if (_appServices == null) return;
        if (!force && _appServices.PositioningService.IsDynamicPositioning()) return;
        var (x, y) = _appServices.PositioningService.ResolvePosition(Width, Height);
        Position = new PixelPoint((int)x, (int)y);
    }

    private void OnDockSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (_appServices?.PositioningService.IsDynamicPositioning() == false)
            ApplyDockPosition();
    }

    private void OnItemPointerEntered(object? sender, PointerEventArgs e)
    {
        if (sender is not Button button || _appServices == null) return;
        if (button.DataContext is not DockItemViewModel vm || vm.Item is not DockProgramItemModel item)
            return;

        _hoveredButton = button;
        _previewHideDebounce.Stop();
        int requestId = ++_previewRequestId;
        var programItem = item;

        Task.Run(() =>
        {
            List<WindowInfo> windows;
            try
            {
                windows = _appServices.WindowPreviewService.LoadPreview(programItem);
            }
            catch (Exception)
            {
                windows = new List<WindowInfo>();
            }
            Dispatcher.UIThread.Post(() => OnPreviewLoaded(requestId, button, vm, windows));
        });
    }

    private void OnPreviewLoaded(int requestId, Button button, DockItemViewModel vm,
        List<WindowInfo> windows)
    {
        if (requestId != _previewRequestId || _hoveredButton != button) return;
        if (windows.Count == 0)
        {
            if (_previewPopup?.IsVisible == true)
                HidePreview();
            return;
        }
        if (_appServices == null) return;

        var appearance = _appServices.AppearanceService;
        if (_previewPopup == null)
        {
            _previewPopup = new WindowPreviewPopup();
            _previewPopup.ThumbnailClicked = hwnd => OnPopupThumbnailClicked(hwnd);
            _previewPopup.PointerEnteredCallback = OnPopupPointerEntered;
            _previewPopup.PointerExitedCallback = OnPopupPointerExited;
        }
        _previewPopup.ShowFor(windows, vm.Label,
            appearance.GetDockColorRGB(), appearance.GetDockBorderRounding(), button);
    }

    private void OnItemPointerExited(object? sender, PointerEventArgs e)
    {
        if (_hoveredButton == sender) _hoveredButton = null;
        _previewHideDebounce.Stop();
        _previewHideDebounce.Start();
    }

    private void HidePreview()
    {
        ++_previewRequestId;
        _previewPopup?.HidePopup();
        _hoveredButton = null;
        _isOverPreview = false;
    }

    private void OnPopupPointerEntered()
    {
        _isOverPreview = true;
        _previewHideDebounce.Stop();
    }

    private void OnPopupPointerExited()
    {
        _isOverPreview = false;
        _previewHideDebounce.Stop();
        _previewHideDebounce.Start();
    }

    private void OnPopupThumbnailClicked(IntPtr sourceHwnd)
    {
        HidePreview();
        if (sourceHwnd != IntPtr.Zero)
            _appServices?.WindowPreviewService.Activate(new WindowInfo(sourceHwnd, ""));
    }

    /// <summary>Allows dragging the borderless dock window (DYNAMIC mode only).</summary>
    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Don't start a window drag when pressing a dock item button — the
        // button must receive the click. Drag only when pressing the dock
        // background itself.
        if (!e.Pointer.IsPrimary)
            return;
        if (e.Source is Visual source && source.FindAncestorOfType<Button>() != null)
            return;
        // In STATIC mode the dock is anchored to the screen and must not be
        // dragged; dragging is only meaningful in DYNAMIC mode.
        if (_appServices == null || !_appServices.PositioningService.IsDynamicPositioning())
            return;
        BeginMoveDrag(e);
    }

    private void UpdateStatus(string status)
    {
        if (DataContext is MainWindowViewModel vm)
            vm.StatusText = status;
    }

    /// <summary>Opens the settings window. Port of App.java openSettingsWindow().</summary>
    private void OpenSettings(MainWindowViewModel vm)
    {
        if (_appServices == null) return;
        SettingsWindow.Open(
            _appServices,
            this,
            dockRefreshAction: vm.UpdateDockUI,
            positioningModeChangeAction: mode => HandlePositioningModeChange(mode)
        );
    }

    /// <summary>Port of App.java handlePositioningModeChange().</summary>
    private void HandlePositioningModeChange(DockPositioningMode mode)
    {
        if (_appServices == null) return;
        var currentMode = _appServices.PositioningService.GetPositioningMode();
        if (currentMode == DockPositioningMode.STATIC && mode == DockPositioningMode.DYNAMIC)
        {
            _appServices.DockService.SetDockPosition(Position.X, Position.Y);
        }
        _appServices.PositioningService.SetPositioningMode(mode);
    }

    protected override void OnClosed(EventArgs e)
    {
        HidePreview();
        if (DataContext is MainWindowViewModel vm)
            vm.Shutdown();
        _dockBehavior?.Dispose();
        _dockBehavior = null;
        base.OnClosed(e);
    }
}