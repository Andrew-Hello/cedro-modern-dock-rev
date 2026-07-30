using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform;
using CedroModernDock.Infrastructure.Windows.Native;
using CedroModernDock.ViewModels;

namespace CedroModernDock.Views;

public partial class MainWindow : Window
{
    private DockWindowBehavior? _dockBehavior;

    public MainWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Once the native window is created and shown, grab its HWND and apply
    /// the dock-specific Win32 behavior (no-activate, no-taskbar, Win+D defense).
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