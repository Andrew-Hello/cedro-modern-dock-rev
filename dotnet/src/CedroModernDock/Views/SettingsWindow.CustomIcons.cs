using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using CedroModernDock.ViewModels;

namespace CedroModernDock.Views;

public partial class SettingsWindow
{
    private bool _customIconPanelInstalled;
    private Button? _chooseCustomIconButton;
    private Button? _resetCustomIconButton;

    private void InstallCustomIconControls()
    {
        if (_customIconPanelInstalled || DataContext is not SettingsViewModel vm)
            return;

        TabControl? tabs = this.GetVisualDescendants().OfType<TabControl>().FirstOrDefault();
        if (tabs == null || tabs.Items.Count == 0 || tabs.Items[0] is not TabItem iconsTab ||
            iconsTab.Content is not Grid iconsGrid)
            return;

        // The first tab's right-hand direct child is the existing Actions stack.
        StackPanel? actionsPanel = iconsGrid.Children
            .OfType<StackPanel>()
            .FirstOrDefault(panel => Grid.GetColumn(panel) == 1);
        if (actionsPanel == null)
            return;

        _customIconPanelInstalled = true;

        var heading = new TextBlock
        {
            Text = vm.CustomIconTitle,
            Foreground = new SolidColorBrush(Color.Parse("#CCCCCC")),
            FontWeight = FontWeight.SemiBold,
            Margin = new Thickness(0, 14, 0, 0)
        };

        var helper = new TextBlock
        {
            Text = vm.CustomIconHelper,
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.Parse("#888888")),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 2)
        };

        _chooseCustomIconButton = new Button
        {
            Content = vm.ChooseCustomIconText,
            Background = new SolidColorBrush(Color.Parse("#007ACC")),
            Foreground = Brushes.White,
            BorderBrush = Brushes.Transparent,
            Padding = new Thickness(12, 6),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left
        };
        _chooseCustomIconButton.Click += async (_, _) =>
        {
            await vm.ChooseCustomIconAsync(this);
            RefreshCustomIconButtonStates();
        };

        _resetCustomIconButton = new Button
        {
            Content = vm.ResetCustomIconText,
            Background = new SolidColorBrush(Color.Parse("#444444")),
            Foreground = Brushes.White,
            BorderBrush = Brushes.Transparent,
            Padding = new Thickness(12, 6),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left
        };
        _resetCustomIconButton.Click += (_, _) =>
        {
            vm.ResetCustomIcon();
            RefreshCustomIconButtonStates();
        };

        actionsPanel.Children.Add(heading);
        actionsPanel.Children.Add(helper);
        actionsPanel.Children.Add(_chooseCustomIconButton);
        actionsPanel.Children.Add(_resetCustomIconButton);
        RefreshCustomIconButtonStates();
    }

    private void RefreshCustomIconButtonStates()
    {
        if (DataContext is not SettingsViewModel vm)
            return;
        if (_chooseCustomIconButton != null)
            _chooseCustomIconButton.IsEnabled = vm.CanCustomizeIcon;
        if (_resetCustomIconButton != null)
            _resetCustomIconButton.IsEnabled = vm.CanResetCustomIcon;
    }

    private void OnCustomIconSelectionChanged()
    {
        if (DataContext is SettingsViewModel vm)
        {
            // SelectionChanged may be raised before the two-way binding has
            // committed SelectedItemIndex. Use the ListBox as the immediate
            // source of truth so the buttons respond on the very first click.
            if (vm.SelectedItemIndex != ItemsList.SelectedIndex)
                vm.SelectedItemIndex = ItemsList.SelectedIndex;
            vm.NotifyCustomIconSelectionChanged();
        }
        RefreshCustomIconButtonStates();
    }
}
