using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using CedroModernDock.Core.Models;
using CedroModernDock.ViewModels;

namespace CedroModernDock.Views;

/// <summary>
/// Rebuilds the Positioning tab's old two-state selector into three explicit,
/// mutually exclusive modes. The Bottom AppBar section deliberately exposes only
/// Left / Center / Right; no free drag, vertical edge or spacing controls exist.
/// </summary>
public partial class SettingsWindow
{
    private bool _bottomAppBarPositioningUiInstalled;
    private RadioButton? _staticModeRadio;
    private RadioButton? _dynamicModeRadio;
    private RadioButton? _bottomAppBarModeRadio;
    private StackPanel? _staticPositioningPanel;
    private StackPanel? _dynamicPositioningPanel;
    private StackPanel? _bottomAppBarPanel;
    private RadioButton? _bottomLeftRadio;
    private RadioButton? _bottomCenterRadio;
    private RadioButton? _bottomRightRadio;

    private void InstallBottomAppBarPositioningControls()
    {
        if (_bottomAppBarPositioningUiInstalled || DataContext is not SettingsViewModel vm)
            return;

        TabControl? tabs = this.GetVisualDescendants().OfType<TabControl>().FirstOrDefault();
        if (tabs == null || tabs.Items.Count < 4 || tabs.Items[3] is not TabItem positioningTab ||
            positioningTab.Content is not StackPanel root || root.Children.Count < 5)
            return;

        if (root.Children[2] is not StackPanel modeRow ||
            root.Children[3] is not StackPanel staticPanel ||
            root.Children[4] is not StackPanel dynamicPanel)
            return;

        _bottomAppBarPositioningUiInstalled = true;
        _staticPositioningPanel = staticPanel;
        _dynamicPositioningPanel = dynamicPanel;

        modeRow.Children.Clear();
        _staticModeRadio = CreateModeRadio(vm.StaticText);
        _dynamicModeRadio = CreateModeRadio(vm.DynamicText);
        _bottomAppBarModeRadio = CreateModeRadio(vm.BottomAppBarText);

        _staticModeRadio.Click += (_, _) =>
        {
            vm.IsStaticPositioningSelected = true;
            RefreshBottomAppBarPositioningUi();
        };
        _dynamicModeRadio.Click += (_, _) =>
        {
            vm.IsDynamicPositioningSelected = true;
            RefreshBottomAppBarPositioningUi();
        };
        _bottomAppBarModeRadio.Click += (_, _) =>
        {
            vm.IsBottomAppBarMode = true;
            RefreshBottomAppBarPositioningUi();
        };

        modeRow.Children.Add(_staticModeRadio);
        modeRow.Children.Add(_dynamicModeRadio);
        modeRow.Children.Add(_bottomAppBarModeRadio);

        _bottomAppBarPanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Vertical,
            Spacing = 8,
            Margin = new Thickness(0, 8, 0, 60)
        };
        _bottomAppBarPanel.Children.Add(new TextBlock
        {
            Text = vm.BottomAppBarTitle,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.Parse("#CCCCCC")),
            Margin = new Thickness(0, 8, 0, 0)
        });
        _bottomAppBarPanel.Children.Add(new TextBlock
        {
            Text = vm.BottomAppBarHelper,
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.Parse("#888888")),
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 690
        });
        _bottomAppBarPanel.Children.Add(new TextBlock
        {
            Text = vm.BottomAppBarAlignmentTitle,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.Parse("#CCCCCC")),
            Margin = new Thickness(0, 12, 0, 2)
        });

        var alignmentRow = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Spacing = 18
        };
        var labels = vm.HorizontalAnchorOptions;
        _bottomLeftRadio = CreateAlignmentRadio(labels.First(x => x.Value == DockHorizontalAnchor.LEFT).Label);
        _bottomCenterRadio = CreateAlignmentRadio(labels.First(x => x.Value == DockHorizontalAnchor.MIDDLE).Label);
        _bottomRightRadio = CreateAlignmentRadio(labels.First(x => x.Value == DockHorizontalAnchor.RIGHT).Label);

        _bottomLeftRadio.Click += (_, _) => SetBottomAppBarAlignment(vm, DockHorizontalAnchor.LEFT);
        _bottomCenterRadio.Click += (_, _) => SetBottomAppBarAlignment(vm, DockHorizontalAnchor.MIDDLE);
        _bottomRightRadio.Click += (_, _) => SetBottomAppBarAlignment(vm, DockHorizontalAnchor.RIGHT);

        alignmentRow.Children.Add(_bottomLeftRadio);
        alignmentRow.Children.Add(_bottomCenterRadio);
        alignmentRow.Children.Add(_bottomRightRadio);
        _bottomAppBarPanel.Children.Add(alignmentRow);
        root.Children.Add(_bottomAppBarPanel);
    }

    private static RadioButton CreateModeRadio(string text) => new()
    {
        Content = text,
        Foreground = Brushes.White,
        GroupName = "CedroPositioningMode"
    };

    private static RadioButton CreateAlignmentRadio(string text) => new()
    {
        Content = text,
        Foreground = Brushes.White,
        GroupName = "CedroBottomAppBarAlignment"
    };

    private void SetBottomAppBarAlignment(SettingsViewModel vm, DockHorizontalAnchor anchor)
    {
        vm.HorizontalAnchor = anchor;
        RefreshBottomAppBarPositioningUi();
    }

    private void RefreshBottomAppBarPositioningUi()
    {
        if (!_bottomAppBarPositioningUiInstalled || DataContext is not SettingsViewModel vm)
            return;

        bool isStatic = vm.IsStaticPositioningSelected;
        bool isDynamic = vm.IsDynamicPositioningSelected;
        bool isBottomAppBar = vm.IsBottomAppBarMode;

        if (_staticModeRadio != null) _staticModeRadio.IsChecked = isStatic;
        if (_dynamicModeRadio != null) _dynamicModeRadio.IsChecked = isDynamic;
        if (_bottomAppBarModeRadio != null) _bottomAppBarModeRadio.IsChecked = isBottomAppBar;
        if (_staticPositioningPanel != null) _staticPositioningPanel.IsVisible = isStatic;
        if (_dynamicPositioningPanel != null) _dynamicPositioningPanel.IsVisible = isDynamic;
        if (_bottomAppBarPanel != null) _bottomAppBarPanel.IsVisible = isBottomAppBar;

        if (_bottomLeftRadio != null)
            _bottomLeftRadio.IsChecked = vm.HorizontalAnchor == DockHorizontalAnchor.LEFT;
        if (_bottomCenterRadio != null)
            _bottomCenterRadio.IsChecked = vm.HorizontalAnchor == DockHorizontalAnchor.MIDDLE;
        if (_bottomRightRadio != null)
            _bottomRightRadio.IsChecked = vm.HorizontalAnchor == DockHorizontalAnchor.RIGHT;

        // Bottom AppBar is intentionally isolated from vertical layout and the
        // four-edge auto-hide state machine. Keep those settings visible for the
        // other modes, but disable them while AppBar owns the bottom work area.
        foreach (CheckBox checkBox in this.GetVisualDescendants().OfType<CheckBox>())
        {
            string content = checkBox.Content?.ToString() ?? string.Empty;
            if (content == vm.ArrangeVerticalText ||
                content == vm.HorizontalEdgeAutoHideTitle ||
                content == vm.VerticalEdgeAutoHideTitle)
            {
                checkBox.IsEnabled = !isBottomAppBar;
            }
        }
    }
}
