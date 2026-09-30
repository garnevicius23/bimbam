using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace BimBam.App.Converters;

/// <summary>Collapses the element when the bound value is null (used to show the "pick a session" hint).</summary>
public sealed class NullToInverseVisibilityConverter : IValueConverter
{
    public static readonly NullToInverseVisibilityConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is null ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
