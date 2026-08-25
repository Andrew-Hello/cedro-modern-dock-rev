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
        Opened += (_, _) => InstallWindowBehaviorSettingsPanel();
    }

    /// <summary>
    /// Adds the enhanced window-behavior controls to the existing General tab
    /// without duplicating the whole Settings XAML. The view-model owns all
    /// persistence; these controls simply mirror its boolean properties.
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

        var heading = new TextBlock
        {
            Text = vm.WindowBehaviorTitle,
            Foreground = new SolidColorBrush(Color.Parse("#CCCCCC")),
            FontWeight = Avalonia.Media.FontWeight.SemiBold,
            Margin = new Avalonia.Thickness(0, 14, 0, 2)
        };

        var alwaysOnTop = new CheckBox
        {
            Content = vm.AlwaysOnTopTitle,
            IsChecked = vm.AlwaysOnTop,
            Foreground = new SolidColorBrush(Color.Parse("#CCCCCC")),
            Margin = new Avalonia.Thickness(0, 4, 0, 0)
        };
        alwaysOnTop.Click += (_, _) => vm.AlwaysOnTop = alwaysOnTop.IsChecked == true;

        var alwaysOnTopHelper = new TextBlock
        {
            Text = vm.AlwaysOnTopHelper,
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.Parse("#888888")),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Avalonia.Thickness(22, 0, 0, 4)
        };

        var edgeAutoHide = new CheckBox
        {
            Content = vm.EdgeAutoHideTitle,
            IsChecked = vm.AutoHideAtScreenEdge,
            Foreground = new SolidColorBrush(Color.Parse("#CCCCCC")),
            Margin = new Avalonia.Thickness(0, 4, 0, 0)
        };
        edgeAutoHide.Click += (_, _) => vm.AutoHideAtScreenEdge = edgeAutoHide.IsChecked == true;

        var edgeAutoHideHelper = new TextBlock
        {
            Text = vm.EdgeAutoHideHelper,
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.Parse("#888888")),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Avalonia.Thickness(22, 0, 0, 8)
        };

        // Insert before acknowledgements/version metadata so window behavior
        // remains grouped with the other interactive General settings.
        int insertAt = Math.Min(4, generalPanel.Children.Count);
        generalPanel.Children.Insert(insertAt++, heading);
        generalPanel.Children.Insert(insertAt++, alwaysOnTop);
        generalPanel.Children.Insert(insertAt++, alwaysOnTopHelper);
        generalPanel.Children.Insert(insertAt++, edgeAutoHide);
        generalPanel.Children.Insert(insertAt, edgeAutoHideHelper);
    }
}
