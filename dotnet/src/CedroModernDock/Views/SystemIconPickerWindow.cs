using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CedroModernDock.Infrastructure.Windows.Native;

namespace CedroModernDock.Views;

/// <summary>
/// Lightweight visual browser for the built-in Windows SHELL32 icon library.
/// Previews are loaded asynchronously in small batches so opening Settings does
/// not block while hundreds of native icon resources are decoded.
/// </summary>
public sealed class SystemIconPickerWindow : Window
{
    private readonly string _libraryPath;
    private readonly string _loadedTemplate;
    private readonly string _failedText;
    private readonly WrapPanel _iconsPanel;
    private readonly TextBlock _statusText;
    private readonly CancellationTokenSource _loadCts = new();
    private readonly List<Bitmap> _previewBitmaps = new();

    public SystemIconPickerWindow(
        string title,
        string subtitle,
        string loadingText,
        string loadedTemplate,
        string failedText,
        string cancelText)
    {
        _libraryPath = SystemIconLibrary.DefaultShell32Path;
        _loadedTemplate = loadedTemplate;
        _failedText = failedText;

        Title = title;
        Width = 760;
        Height = 610;
        MinWidth = 560;
        MinHeight = 420;
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
            TextWrapping = TextWrapping.Wrap
        });
        header.Children.Add(new TextBlock
        {
            Text = @"%SystemRoot%\System32\SHELL32.dll",
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.Parse("#777777")),
            Margin = new Thickness(0, 2, 0, 8)
        });
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

        Opened += async (_, _) => await LoadIconsAsync(_loadCts.Token);
        Closed += (_, _) =>
        {
            _loadCts.Cancel();
            foreach (Bitmap bitmap in _previewBitmaps)
                bitmap.Dispose();
            _previewBitmaps.Clear();
            _loadCts.Dispose();
        };
    }

    private async Task LoadIconsAsync(CancellationToken token)
    {
        try
        {
            int iconCount = await Task.Run(() => SystemIconLibrary.GetIconCount(_libraryPath), token);
            if (iconCount <= 0)
            {
                _statusText.Text = _failedText;
                return;
            }

            const int batchSize = 20;
            var batch = new List<(int Index, byte[] Png)>(batchSize);
            int shown = 0;

            for (int index = 0; index < iconCount; index++)
            {
                token.ThrowIfCancellationRequested();
                byte[]? png = await Task.Run(
                    () => SystemIconLibrary.ExtractPngBytes(_libraryPath, index, 64), token);
                if (png is not { Length: > 0 })
                    continue;

                batch.Add((index, png));
                if (batch.Count < batchSize && index < iconCount - 1)
                    continue;

                var toAdd = batch.ToArray();
                batch.Clear();
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    foreach (var entry in toAdd)
                    {
                        AddIconButton(entry.Index, entry.Png);
                        shown++;
                    }
                    _statusText.Text = string.Format(_loadedTemplate, shown, iconCount);
                });
            }

            _statusText.Text = string.Format(_loadedTemplate, shown, iconCount);
        }
        catch (OperationCanceledException)
        {
            // Normal when the user closes the picker before all previews load.
        }
        catch
        {
            if (!token.IsCancellationRequested)
                _statusText.Text = _failedText;
        }
    }

    private void AddIconButton(int iconIndex, byte[] previewPng)
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

        ToolTip.SetTip(button, $"SHELL32.dll #{iconIndex}");
        button.Click += async (_, _) => await SelectIconAsync(button, iconIndex);
        _iconsPanel.Children.Add(button);
    }

    private async Task SelectIconAsync(Button button, int iconIndex)
    {
        button.IsEnabled = false;
        try
        {
            string? data = await Task.Run(
                () => SystemIconLibrary.ExtractPngBase64(_libraryPath, iconIndex, 256));
            if (!string.IsNullOrWhiteSpace(data))
            {
                Close(data);
                return;
            }

            _statusText.Text = _failedText;
        }
        catch
        {
            _statusText.Text = _failedText;
        }
        finally
        {
            if (IsVisible)
                button.IsEnabled = true;
        }
    }
}
