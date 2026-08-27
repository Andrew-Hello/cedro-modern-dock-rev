using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CedroModernDock.Infrastructure.Windows.Native;

namespace CedroModernDock.Views;

/// <summary>
/// Visual browser for a curated set of Windows icon resource libraries. The
/// user switches libraries from the selector at the top; each available DLL/EXE
/// is scanned and rendered as a grid of thumbnails. Selection returns only the
/// resource expression + ordinal, not copied PNG data.
/// </summary>
public sealed class SystemIconPickerWindow : Window
{
    private readonly string _loadedTemplate;
    private readonly string _loadingText;
    private readonly string _failedText;
    private readonly string _noLibrariesText;
    private readonly Func<string, string> _categoryName;
    private readonly ComboBox _librarySelector;
    private readonly TextBlock _pathText;
    private readonly WrapPanel _iconsPanel;
    private readonly TextBlock _statusText;
    private readonly CancellationTokenSource _windowCts = new();
    private CancellationTokenSource? _libraryLoadCts;
    private readonly List<Bitmap> _previewBitmaps = new();
    private bool _opened;

    public SystemIconPickerWindow(
        string title,
        string subtitle,
        string libraryLabel,
        string loadingText,
        string loadedTemplate,
        string failedText,
        string noLibrariesText,
        string cancelText,
        Func<string, string> categoryName)
    {
        _loadedTemplate = loadedTemplate;
        _loadingText = loadingText;
        _failedText = failedText;
        _noLibrariesText = noLibrariesText;
        _categoryName = categoryName;

        Title = title;
        Width = 800;
        Height = 640;
        MinWidth = 600;
        MinHeight = 440;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.Parse("#202020"));

        var root = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*,Auto"),
            Margin = new Thickness(18)
        };

        var header = new StackPanel { Spacing = 4 };
        header.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 20,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brushes.White
        });
        header.Children.Add(new TextBlock
        {
            Text = subtitle,
            Foreground = new SolidColorBrush(Color.Parse("#AAAAAA")),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 6)
        });

        var selectorRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            ColumnSpacing = 10
        };
        selectorRow.Children.Add(new TextBlock
        {
            Text = libraryLabel,
            Foreground = new SolidColorBrush(Color.Parse("#CCCCCC")),
            VerticalAlignment = VerticalAlignment.Center
        });

        _librarySelector = new ComboBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MinWidth = 320
        };
        Grid.SetColumn(_librarySelector, 1);
        selectorRow.Children.Add(_librarySelector);
        header.Children.Add(selectorRow);

        _pathText = new TextBlock
        {
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.Parse("#777777")),
            Margin = new Thickness(0, 2, 0, 8),
            TextWrapping = TextWrapping.Wrap
        };
        header.Children.Add(_pathText);
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        _iconsPanel = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Top
        };

        var scroll = new ScrollViewer
        {
            Content = _iconsPanel,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            Background = new SolidColorBrush(Color.Parse("#181818")),
            Padding = new Thickness(8)
        };
        Grid.SetRow(scroll, 1);
        root.Children.Add(scroll);

        var footer = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Margin = new Thickness(0, 10, 0, 0)
        };
        _statusText = new TextBlock
        {
            Text = loadingText,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = new SolidColorBrush(Color.Parse("#999999"))
        };
        footer.Children.Add(_statusText);

        var cancel = new Button
        {
            Content = cancelText,
            Padding = new Thickness(16, 7),
            Background = new SolidColorBrush(Color.Parse("#444444")),
            Foreground = Brushes.White,
            BorderBrush = Brushes.Transparent
        };
        cancel.Click += (_, _) => Close(null);
        Grid.SetColumn(cancel, 1);
        footer.Children.Add(cancel);
        Grid.SetRow(footer, 2);
        root.Children.Add(footer);

        Content = root;
        PopulateLibrarySelector();

        _librarySelector.SelectionChanged += (_, _) =>
        {
            UpdateLibraryPathText();
            if (_opened)
                _ = LoadSelectedLibraryAsync();
        };

        Opened += (_, _) =>
        {
            _opened = true;
            if (_librarySelector.SelectedItem != null)
                _ = LoadSelectedLibraryAsync();
            else
                _statusText.Text = _noLibrariesText;
        };

        Closed += (_, _) =>
        {
            _opened = false;
            _windowCts.Cancel();
            _libraryLoadCts?.Cancel();
            _libraryLoadCts?.Dispose();
            DisposePreviews();
            _windowCts.Dispose();
        };
    }

    private void PopulateLibrarySelector()
    {
        var available = SystemIconLibrary.AvailableLibraries;
        foreach (SystemIconLibraryDescriptor descriptor in available)
        {
            _librarySelector.Items.Add(new ComboBoxItem
            {
                Content = $"{_categoryName(descriptor.Category)} · {descriptor.FileName}",
                Tag = descriptor
            });
        }

        if (_librarySelector.Items.Count > 0)
            _librarySelector.SelectedIndex = 0;
        else
            _librarySelector.IsEnabled = false;

        UpdateLibraryPathText();
    }

    private SystemIconLibraryDescriptor? SelectedLibrary
        => (_librarySelector.SelectedItem as ComboBoxItem)?.Tag as SystemIconLibraryDescriptor;

    private void UpdateLibraryPathText()
    {
        SystemIconLibraryDescriptor? library = SelectedLibrary;
        _pathText.Text = library == null
            ? string.Empty
            : $"{library.SourceExpression}   →   {library.ResolvedPath}";
    }

    private async Task LoadSelectedLibraryAsync()
    {
        SystemIconLibraryDescriptor? library = SelectedLibrary;
        if (library == null)
        {
            _statusText.Text = _noLibrariesText;
            return;
        }

        _libraryLoadCts?.Cancel();
        _libraryLoadCts?.Dispose();
        _libraryLoadCts = CancellationTokenSource.CreateLinkedTokenSource(_windowCts.Token);
        CancellationToken token = _libraryLoadCts.Token;

        DisposePreviews();
        _iconsPanel.Children.Clear();
        _statusText.Text = _loadingText;

        try
        {
            int iconCount = await Task.Run(
                () => SystemIconLibrary.GetIconCount(library.SourceExpression), token);
            if (iconCount <= 0)
            {
                if (!token.IsCancellationRequested)
                    _statusText.Text = _failedText;
                return;
            }

            const int batchSize = 24;
            int shown = 0;

            for (int start = 0; start < iconCount; start += batchSize)
            {
                token.ThrowIfCancellationRequested();
                int batchStart = start;
                int batchEnd = Math.Min(iconCount, start + batchSize);

                var entries = await Task.Run(() =>
                {
                    var result = new List<(int Index, byte[] Png)>();
                    for (int index = batchStart; index < batchEnd; index++)
                    {
                        token.ThrowIfCancellationRequested();
                        byte[]? png = SystemIconLibrary.ExtractPngBytes(
                            library.SourceExpression, index, 64);
                        if (png is { Length: > 0 })
                            result.Add((index, png));
                    }
                    return result;
                }, token);

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (token.IsCancellationRequested)
                        return;

                    foreach (var entry in entries)
                    {
                        AddIconButton(library, entry.Index, entry.Png);
                        shown++;
                    }
                    _statusText.Text = string.Format(
                        _loadedTemplate, shown, iconCount, library.FileName);
                });
            }

            if (!token.IsCancellationRequested)
                _statusText.Text = string.Format(
                    _loadedTemplate, shown, iconCount, library.FileName);
        }
        catch (OperationCanceledException)
        {
            // Normal when switching resource libraries or closing the picker.
        }
        catch
        {
            if (!token.IsCancellationRequested)
                _statusText.Text = _failedText;
        }
    }

    private void AddIconButton(
        SystemIconLibraryDescriptor library, int iconIndex, byte[] previewPng)
    {
        using var stream = new MemoryStream(previewPng, writable: false);
        var bitmap = new Bitmap(stream);
        _previewBitmaps.Add(bitmap);

        var image = new Image
        {
            Source = bitmap,
            Width = 48,
            Height = 48,
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        var indexLabel = new TextBlock
        {
            Text = $"#{iconIndex}",
            FontSize = 10,
            Foreground = new SolidColorBrush(Color.Parse("#A8A8A8")),
            HorizontalAlignment = HorizontalAlignment.Center
        };

        var content = new StackPanel
        {
            Spacing = 4,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        content.Children.Add(image);
        content.Children.Add(indexLabel);

        var button = new Button
        {
            Content = content,
            Width = 76,
            Height = 76,
            Margin = new Thickness(3),
            Padding = new Thickness(6),
            Background = new SolidColorBrush(Color.Parse("#2B2B2B")),
            BorderBrush = new SolidColorBrush(Color.Parse("#3A3A3A")),
            BorderThickness = new Thickness(1),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };

        ToolTip.SetTip(button, $"{library.FileName} #{iconIndex}\n{library.SourceExpression},{iconIndex}");
        button.Click += (_, _) => Close(
            new SystemIconSelection(library.SourceExpression, iconIndex));
        _iconsPanel.Children.Add(button);
    }

    private void DisposePreviews()
    {
        foreach (Bitmap bitmap in _previewBitmaps)
            bitmap.Dispose();
        _previewBitmaps.Clear();
    }
}
