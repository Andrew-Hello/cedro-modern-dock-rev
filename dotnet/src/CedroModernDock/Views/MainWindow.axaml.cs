using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform;
using CedroModernDock.Core.Application;
using CedroModernDock.Infrastructure.Windows.Native;
using CedroModernDock.ViewModels;

namespace CedroModernDock.Views;

public partial class MainWindow : Window
{
    private DockWindowBehavior? _dockBehavior;
    private AppServices? _appServices;

    public MainWindow()
    {
        InitializeComponent();
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

        // Apply saved dock position (static anchor or dynamic coordinates).
        if (_appServices != null)
        {
            var (x, y) = _appServices.PositioningService.ResolvePosition(Width, Height);
            Position = new PixelPoint((int)x, (int)y);
        }
    }

    /// <summary>Allows dragging the borderless dock window.</summary>
    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Pointer.IsPrimary)
            BeginMoveDrag(e);
    }

    private void UpdateStatus(string status)
    {
        if (DataContext is MainWindowViewModel vm)
            vm.StatusText = status;
    }

    protected override void OnClosed(EventArgs e)
    {
        _dockBehavior?.Dispose();
        _dockBehavior = null;
        base.OnClosed(e);
    }
}