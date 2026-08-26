using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using CedroModernDock.ViewModels;

namespace CedroModernDock.Views;

public partial class MainWindow
{
    private const double RunningPinDragThreshold = 6;
    private const string RunningPinDragPrefix = "CEDRO_RUNNING_APP|";

    private Point _runningDragPressPoint;
    private RunningAppViewModel? _runningDragSource;
    private bool _runningDragInProgress;

    /// <summary>
    /// Wrapper used by XAML so disabling window previews truly prevents any
    /// thumbnail query/popup work while hover labels remain independent.
    /// </summary>
    private void OnConfiguredItemPointerEntered(object? sender, PointerEventArgs e)
    {
        if (_appServices?.AppearanceService.GetShowWindowPreviews() != true)
        {
            HidePreview();
            return;
        }
        OnItemPointerEntered(sender, e);
    }

    private void OnConfiguredRunningAppPointerEntered(object? sender, PointerEventArgs e)
    {
        if (_appServices?.AppearanceService.GetShowWindowPreviews() != true)
        {
            HidePreview();
            return;
        }
        OnRunningAppPointerEntered(sender, e);
    }

    private void OnRunningAppDragPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.Pointer.IsPrimary || sender is not Button button ||
            button.DataContext is not RunningAppViewModel vm)
            return;

        if (!e.GetCurrentPoint(button).Properties.IsLeftButtonPressed)
            return;

        _runningDragPressPoint = e.GetPosition(button);
        _runningDragSource = vm;
        _runningDragInProgress = false;
    }

    private async void OnRunningAppDragPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_runningDragSource == null || _runningDragInProgress || sender is not Button button)
            return;
        if (!e.GetCurrentPoint(button).Properties.IsLeftButtonPressed)
            return;

        Point current = e.GetPosition(button);
        double dx = current.X - _runningDragPressPoint.X;
        double dy = current.Y - _runningDragPressPoint.Y;
        if (Math.Abs(dx) < RunningPinDragThreshold && Math.Abs(dy) < RunningPinDragThreshold)
            return;

        _runningDragInProgress = true;
        HidePreview();

        var data = new DataTransfer();
        data.Add(DataTransferItem.CreateText(
            RunningPinDragPrefix + _runningDragSource.IdentityKey));

        try
        {
            await DragDrop.DoDragDropAsync(e, data, DragDropEffects.Copy);
        }
        finally
        {
            _runningDragInProgress = false;
            _runningDragSource = null;
        }
    }

    private void OnRunningAppDragPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_runningDragInProgress)
            _runningDragSource = null;
    }

    private void OnRunningAppDragPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (!_runningDragInProgress)
            _runningDragSource = null;
    }

    private void OnPinnedAreaDragOver(object? sender, DragEventArgs e)
    {
        string? text = e.DataTransfer.TryGetText();
        if (text?.StartsWith(RunningPinDragPrefix, StringComparison.Ordinal) == true)
        {
            e.DragEffects = DragDropEffects.Copy;
            e.Handled = true;
            return;
        }

        e.DragEffects = DragDropEffects.None;
    }

    private void OnPinnedAreaDrop(object? sender, DragEventArgs e)
    {
        string? text = e.DataTransfer.TryGetText();
        if (text?.StartsWith(RunningPinDragPrefix, StringComparison.Ordinal) != true)
            return;

        string identity = text[RunningPinDragPrefix.Length..];
        if (DataContext is MainWindowViewModel vm && !string.IsNullOrWhiteSpace(identity))
            vm.PinRunningAppByIdentity(identity);

        e.DragEffects = DragDropEffects.Copy;
        e.Handled = true;
    }
}
