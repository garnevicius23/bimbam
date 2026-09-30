using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using BimBam.Core.Enums;

namespace BimBam.App.Converters;

/// <summary>Converts a <see cref="LineStatus"/> value into a background brush for the order grid.</summary>
public sealed class LineStatusToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        LineStatus.Matches => new SolidColorBrush(Color.FromRgb(0xDF, 0xF5, 0xDF)),
        LineStatus.Shortage => new SolidColorBrush(Color.FromRgb(0xFB, 0xE2, 0xE2)),
        LineStatus.Surplus => new SolidColorBrush(Color.FromRgb(0xFF, 0xF3, 0xCD)),
        _ => Brushes.White
    };

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
