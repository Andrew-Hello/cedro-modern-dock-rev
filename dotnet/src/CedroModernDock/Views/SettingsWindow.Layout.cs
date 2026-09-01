using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.VisualTree;
using CedroModernDock.Infrastructure.Windows.Native;
using CedroModernDock.ViewModels;

namespace CedroModernDock.Views;

/// <summary>
/// Keeps the growing Settings UI compact and, more importantly, removes the
/// native owner/topmost relationship inherited from Cedro's always-on-top Dock.
/// Child dialogs can then use SettingsWindow as their normal modal owner without
/// being hidden behind it.
/// </summary>
public partial class SettingsWindow
{
    private const int GwlHwndParent = -8;
    private static readonly IntPtr HwndNotTopmost = new(-2);
    private bool _settingsLayoutOptimized;

    /// <summary>
    /// Called from the constructor, before the window is shown. A wider/resizable
    /// settings surface lets the enhanced controls use columns instead of ever
    /// longer vertical stacks while remaining usable on 1280 logical-pixel screens.
    /// </summary>
    private void ConfigureModernSettingsWindow()
    {
        Width = 940;
        Height = 620;
        MinWidth = 780;
        MinHeight = 520;
        CanResize = true;
        ShowInTaskbar = false;
        Topmost = false;
    }

    /// <summary>
    /// The Settings window used to be shown as an owned child of the Dock. Since
    /// the Dock itself can be HWND_TOPMOST, Windows can promote its owned Settings
    /// window into the same topmost band. That makes Settings cover dialogs which
    /// it opens. Detach only the native owner after HWND creation and explicitly
    /// place Settings in the normal z-order band. Cedro still keeps a managed
    /// single-instance reference, while dialogs opened from Settings can safely
    /// use Settings as their own owner.
    /// </summary>
    private void NormalizeSettingsWindowZOrder()
    {
        Topmost = false;
        IPlatformHandle? handle = this.TryGetPlatformHandle();
        if (handle?.Handle is not IntPtr hwnd || hwnd == IntPtr.Zero)
            return;

        User32.SetWindowLongPtr(hwnd, GwlHwndParent, IntPtr.Zero);
        User32.SetWindowPos(
            hwnd,
            HwndNotTopmost,
            0, 0, 0, 0,
            Win32Constants.SWP_NOMOVE |
            Win32Constants.SWP_NOSIZE |
            Win32Constants.SWP_NOACTIVATE |
            Win32Constants.SWP_SHOWWINDOW);
    }

    /// <summary>
    /// Reflows the five original pages only after all enhanced panels have been
    /// installed. This means the older feature modules can keep their simple
    /// insertion logic, while the final Settings surface is compact and balanced.
    /// </summary>
    private void OptimizeSettingsLayout()
    {
        if (_settingsLayoutOptimized || DataContext is not SettingsViewModel vm)
            return;

        TabControl? tabs = this.GetVisualDescendants().OfType<TabControl>().FirstOrDefault();
        if (tabs == null || tabs.Items.Count < 5)
            return;

        if (tabs.Items[0] is not TabItem iconsTab ||
            tabs.Items[1] is not TabItem iconCustomizationTab ||
            tabs.Items[2] is not TabItem dockCustomizationTab ||
            tabs.Items[3] is not TabItem positioningTab ||
            tabs.Items[4] is not TabItem generalTab)
            return;

        _settingsLayoutOptimized = true;

        OptimizeItemsPage(iconsTab, vm);
        OptimizeIconCustomizationPage(iconCustomizationTab);
        OptimizeDockCustomizationPage(dockCustomizationTab);
        OptimizePositioningPage(positioningTab, vm);
        OptimizeGeneralPage(generalTab, vm);

        // Workflow order: manage items first, then decide where the Dock lives,
        // then appearance, icon appearance, and finally lower-frequency settings.
        var orderedTabs = new object[]
        {
            iconsTab,
            positioningTab,
            dockCustomizationTab,
            iconCustomizationTab,
            generalTab
        };
        tabs.Items.Clear();
        foreach (object tab in orderedTabs)
            tabs.Items.Add(tab);
        tabs.SelectedIndex = 0;
    }

    private void OptimizeItemsPage(TabItem tab, SettingsViewModel vm)
    {
        if (tab.Content is not Grid root)
            return;

        root.Margin = new Thickness(8, 6);
        root.ColumnDefinitions.Clear();
        root.ColumnDefinitions.Add(StarColumn(5));
        root.ColumnDefinitions.Add(StarColumn(7));

        ItemsList.MinHeight = 300;
        ItemsList.MaxHeight = 380;

        StackPanel? actions = root.Children
            .OfType<StackPanel>()
            .FirstOrDefault(panel => Grid.GetColumn(panel) == 1);
        if (actions == null)
            return;

        var children = actions.Children.ToList();
        int customStart = children.FindIndex(control =>
            control is TextBlock text && string.Equals(text.Text, vm.CustomIconTitle, StringComparison.Ordinal));
        if (customStart <= 0)
            return;

        actions.Children.Clear();
        actions.Spacing = 0;
        actions.Margin = new Thickness(14, 0, 0, 0);

        var split = new Grid
        {
            ColumnDefinitions =
            {
                StarColumn(1),
                StarColumn(1)
            },
            ColumnSpacing = 14
        };

        var operations = new StackPanel { Spacing = 6 };
        var customIcons = new StackPanel { Spacing = 6 };

        for (int i = 0; i < children.Count; i++)
        {
            if (i < customStart)
                operations.Children.Add(children[i]);
            else
                customIcons.Children.Add(children[i]);
        }

        var operationsCard = CreateCard(operations);
        var customCard = CreateCard(customIcons);
        Grid.SetColumn(operationsCard, 0);
        Grid.SetColumn(customCard, 1);
        split.Children.Add(operationsCard);
        split.Children.Add(customCard);
        actions.Children.Add(split);
    }

    private static void OptimizeIconCustomizationPage(TabItem tab)
    {
        if (tab.Content is not StackPanel root || root.Children.Count < 3)
            return;

        var sections = root.Children.ToList();
        root.Children.Clear();
        root.Margin = new Thickness(12, 8);
        root.Spacing = 0;

        var grid = new Grid
        {
            ColumnDefinitions = { StarColumn(1), StarColumn(1) },
            RowDefinitions = { AutoRow(), AutoRow() },
            ColumnSpacing = 16,
            RowSpacing = 14
        };

        AddToGrid(grid, CreateCard(sections[0]), 0, 0);
        AddToGrid(grid, CreateCard(sections[1]), 1, 0);
        var tint = CreateCard(sections[2]);
        Grid.SetRow(tint, 1);
        Grid.SetColumnSpan(tint, 2);
        grid.Children.Add(tint);
        root.Children.Add(grid);
    }

    private static void OptimizeDockCustomizationPage(TabItem tab)
    {
        if (tab.Content is not StackPanel root || root.Children.Count < 4)
            return;

        var sections = root.Children.ToList();
        root.Children.Clear();
        root.Margin = new Thickness(12, 8);
        root.Spacing = 0;

        var grid = new Grid
        {
            ColumnDefinitions = { StarColumn(1), StarColumn(1), StarColumn(1) },
            RowDefinitions = { AutoRow(), AutoRow() },
            ColumnSpacing = 14,
            RowSpacing = 14
        };

        AddToGrid(grid, CreateCard(sections[0]), 0, 0);
        AddToGrid(grid, CreateCard(sections[1]), 1, 0);
        AddToGrid(grid, CreateCard(sections[2]), 2, 0);

        var background = CreateCard(sections[3]);
        Grid.SetRow(background, 1);
        Grid.SetColumnSpan(background, 3);
        grid.Children.Add(background);
        root.Children.Add(grid);
    }

    private void OptimizePositioningPage(TabItem tab, SettingsViewModel vm)
    {
        if (tab.Content is not StackPanel root)
            return;

        root.Margin = new Thickness(12, 8);
        root.Spacing = 8;

        if (_bottomAppBarPanel != null)
            _bottomAppBarPanel.Margin = new Thickness(0, 8, 0, 0);

        foreach (TextBlock text in root.GetVisualDescendants().OfType<TextBlock>())
        {
            if (string.Equals(text.Text, vm.AlignmentTitle, StringComparison.Ordinal))
                text.Margin = new Thickness(0, 10, 0, 8);
            else if (string.Equals(text.Text, vm.ScreenSpacingTitle, StringComparison.Ordinal))
                text.Margin = new Thickness(0, 14, 0, 4);
        }

        // The original page used 60/80 px bottom spacers to compensate for its
        // old small viewport. They are counterproductive in the larger layout.
        foreach (StackPanel panel in root.GetVisualDescendants().OfType<StackPanel>())
        {
            if (panel.Margin.Bottom >= 40)
                panel.Margin = new Thickness(panel.Margin.Left, panel.Margin.Top, panel.Margin.Right, 0);
        }
    }

    private static void OptimizeGeneralPage(TabItem tab, SettingsViewModel vm)
    {
        if (tab.Content is not StackPanel root || root.Children.Count < 2)
            return;

        var children = root.Children.ToList();
        int splitAt = children.FindIndex(control =>
            control is TextBlock text && string.Equals(text.Text, vm.DockInteractionTitle, StringComparison.Ordinal));
        if (splitAt <= 0)
            splitAt = (children.Count + 1) / 2;

        root.Children.Clear();
        root.Margin = new Thickness(12, 8);
        root.Spacing = 0;

        var left = new StackPanel { Spacing = 5 };
        var right = new StackPanel { Spacing = 5 };
        for (int i = 0; i < children.Count; i++)
        {
            if (i < splitAt)
                left.Children.Add(children[i]);
            else
                right.Children.Add(children[i]);
        }

        var grid = new Grid
        {
            ColumnDefinitions = { StarColumn(1), StarColumn(1) },
            ColumnSpacing = 16
        };
        var leftCard = CreateCard(left);
        var rightCard = CreateCard(right);
        Grid.SetColumn(leftCard, 0);
        Grid.SetColumn(rightCard, 1);
        grid.Children.Add(leftCard);
        grid.Children.Add(rightCard);
        root.Children.Add(grid);
    }

    private static Border CreateCard(Control content) => new()
    {
        Background = new SolidColorBrush(Color.Parse("#252525")),
        BorderBrush = new SolidColorBrush(Color.Parse("#343434")),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(7),
        Padding = new Thickness(12),
        Child = content
    };

    private static ColumnDefinition StarColumn(double value) =>
        new(new GridLength(value, GridUnitType.Star));

    private static RowDefinition AutoRow() => new(GridLength.Auto);

    private static void AddToGrid(Grid grid, Control control, int column, int row)
    {
        Grid.SetColumn(control, column);
        Grid.SetRow(control, row);
        grid.Children.Add(control);
    }
}
