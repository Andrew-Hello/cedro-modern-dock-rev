using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace CedroModernDock.Converters;

/// <summary>
/// Converts any appearance refresh signal into the dock's current padding.
/// Horizontal padding remains fixed at 10 px; vertical padding is user-configurable.
/// </summary>
public sealed class DockPaddingConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        int vertical = App.Services?.AppearanceService.GetDockVerticalPadding() ?? 4;
        vertical = Math.Clamp(vertical, 0, 20);
        return new Thickness(10, vertical);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
