using System.Globalization;
using System.Windows.Data;
using BimBam.Core.Enums;

namespace BimBam.App.Converters;

/// <summary>Converts a <see cref="LineStatus"/> value into its Lithuanian display text.</summary>
public sealed class LineStatusToTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        LineStatus.Matches => "Sutampa",
        LineStatus.Shortage => "Trūksta",
        LineStatus.Surplus => "Perteklius",
        _ => "Netikrinta"
    };

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
