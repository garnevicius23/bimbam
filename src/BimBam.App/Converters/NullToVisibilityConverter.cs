using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace BimBam.App.Converters;

/// <summary>Shows the element only when the bound value is null (the "pick a session" hint).</summary>
public sealed class NullToVisibilityConverter : IValueConverter
{
    public static readonly NullToVisibilityConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is null ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
