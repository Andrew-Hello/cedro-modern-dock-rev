using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using CedroModernDock.ViewModels;

namespace CedroModernDock.Views;

public partial class SettingsWindow
{
    private bool _windowBehaviorPanelInstalled;

    private void InitializeWindowBehaviorSettingsHooks()
    {
        ConfigureModernSettingsWindow();
        ItemsList.SelectionChanged += (_, _) => OnCustomIconSelectionChanged();

        Opened += (_, _) =>
        {
            InstallWindowBehaviorSettingsPanel();
            InstallBottomAppBarPositioningControls();
            InstallCustomIconControls();
            InstallConfigBackupSettingsPanel();

            // The Dock can itself be HWND_TOPMOST. Normalize Settings after its
            // HWND exists so owned dialogs (system icon picker, color picker,
            // module window, etc.) always appear above Settings as expected.
            NormalizeSettingsWindowZOrder();

            // Reflow only after every dynamically-added section exists. This
            // keeps the older insertion modules simple while presenting the user
            // with a compact multi-column settings surface.
            OptimizeSettingsLayout();

            if (DataContext is SettingsViewModel vm)
            {
                vm.ApplyCustomIconPreviews();
                RefreshCustomIconButtonStates();
                RefreshBottomAppBarPositioningUi();
            }
        };
    }

    /// <summary>
    /// Adds enhanced window-behavior and dock-interaction controls to the
    /// existing General tab without duplicating the whole Settings XAML.
    /// AppBar is deliberately NOT exposed here; it is an independent positioning mode.
    /// </summary>
    private void InstallWindowBehaviorSettingsPanel()
    {
        if (_windowBehaviorPanelInstalled || DataContext is not SettingsViewModel vm)
            return;

        TabControl? tabs = this.GetVisualDescendants().OfType<TabControl>().FirstOrDefault();
        if (tabs == null || tabs.Items.Count < 5 || tabs.Items[4] is not TabItem generalTab ||
            generalTab.Content is not StackPanel generalPanel)
            return;

        _windowBehaviorPanelInstalled = true;

        var heading = CreateSectionHeading(vm.WindowBehaviorTitle);
        var alwaysOnTop = CreateCheckBox(vm.AlwaysOnTopTitle, vm.AlwaysOnTop);
        alwaysOnTop.Click += (_, _) => vm.AlwaysOnTop = alwaysOnTop.IsChecked == true;
        var alwaysOnTopHelper = CreateHelper(vm.AlwaysOnTopHelper);

        var horizontalEdgeAutoHide = CreateCheckBox(
            vm.HorizontalEdgeAutoHideTitle, vm.AutoHideAtHorizontalEdges);
        horizontalEdgeAutoHide.Click += (_, _) =>
            vm.AutoHideAtHorizontalEdges = horizontalEdgeAutoHide.IsChecked == true;
        var horizontalEdgeHelper = CreateHelper(vm.HorizontalEdgeAutoHideHelper);

        var verticalEdgeAutoHide = CreateCheckBox(
            vm.VerticalEdgeAutoHideTitle, vm.AutoHideAtVerticalEdges);
        verticalEdgeAutoHide.Click += (_, _) =>
            vm.AutoHideAtVerticalEdges = verticalEdgeAutoHide.IsChecked == true;
        var verticalEdgeHelper = CreateHelper(vm.VerticalEdgeAutoHideHelper, bottomMargin: 8);

        var interactionHeading = CreateSectionHeading(vm.DockInteractionTitle);

        var runningIndicators = CreateCheckBox(
            vm.RunningIndicatorsTitle, vm.ShowRunningIndicators);
        runningIndicators.Click += (_, _) =>
            vm.ShowRunningIndicators = runningIndicators.IsChecked == true;
        var runningIndicatorsHelper = CreateHelper(vm.RunningIndicatorsHelper);

        var hoverMagnification = CreateCheckBox(
            vm.HoverMagnificationTitle, vm.EnableHoverMagnification);
        hoverMagnification.Click += (_, _) =>
            vm.EnableHoverMagnification = hoverMagnification.IsChecked == true;
        var hoverMagnificationHelper = CreateHelper(vm.HoverMagnificationHelper);

        var hoverLabels = CreateCheckBox(vm.HoverLabelsTitle, vm.ShowHoverLabels);
        hoverLabels.Click += (_, _) =>
            vm.ShowHoverLabels = hoverLabels.IsChecked == true;
        var hoverLabelsHelper = CreateHelper(vm.HoverLabelsHelper);

        var windowPreviews = CreateCheckBox(vm.WindowPreviewsTitle, vm.ShowWindowPreviews);
        windowPreviews.Click += (_, _) =>
            vm.ShowWindowPreviews = windowPreviews.IsChecked == true;
        var windowPreviewsHelper = CreateHelper(vm.WindowPreviewsHelper, bottomMargin: 8);

        // Insert before acknowledgements/version metadata so all interactive
        // General settings remain together. Config/backup is added afterwards.
        int insertAt = Math.Min(4, generalPanel.Children.Count);
        generalPanel.Children.Insert(insertAt++, heading);
        generalPanel.Children.Insert(insertAt++, alwaysOnTop);
        generalPanel.Children.Insert(insertAt++, alwaysOnTopHelper);
        generalPanel.Children.Insert(insertAt++, horizontalEdgeAutoHide);
        generalPanel.Children.Insert(insertAt++, horizontalEdgeHelper);
        generalPanel.Children.Insert(insertAt++, verticalEdgeAutoHide);
        generalPanel.Children.Insert(insertAt++, verticalEdgeHelper);
        generalPanel.Children.Insert(insertAt++, interactionHeading);
        generalPanel.Children.Insert(insertAt++, runningIndicators);
        generalPanel.Children.Insert(insertAt++, runningIndicatorsHelper);
        generalPanel.Children.Insert(insertAt++, hoverMagnification);
        generalPanel.Children.Insert(insertAt++, hoverMagnificationHelper);
        generalPanel.Children.Insert(insertAt++, hoverLabels);
        generalPanel.Children.Insert(insertAt++, hoverLabelsHelper);
        generalPanel.Children.Insert(insertAt++, windowPreviews);
        generalPanel.Children.Insert(insertAt, windowPreviewsHelper);
    }

    private static TextBlock CreateSectionHeading(string text) => new()
    {
        Text = text,
        Foreground = new SolidColorBrush(Color.Parse("#CCCCCC")),
        FontWeight = FontWeight.SemiBold,
        Margin = new Avalonia.Thickness(0, 14, 0, 2)
    };

    private static CheckBox CreateCheckBox(string text, bool value) => new()
    {
        Content = text,
        IsChecked = value,
        Foreground = new SolidColorBrush(Color.Parse("#CCCCCC")),
        Margin = new Avalonia.Thickness(0, 4, 0, 0)
    };

    private static TextBlock CreateHelper(string text, double bottomMargin = 4) => new()
    {
        Text = text,
        FontSize = 11,
        Foreground = new SolidColorBrush(Color.Parse("#888888")),
        TextWrapping = TextWrapping.Wrap,
        Margin = new Avalonia.Thickness(22, 0, 0, bottomMargin)
    };
}
