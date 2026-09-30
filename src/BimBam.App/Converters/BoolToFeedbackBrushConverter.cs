using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace BimBam.App.Converters;

/// <summary>Picks the feedback banner's background color based on whether the last scan was an error.</summary>
public sealed class BoolToFeedbackBrushConverter : IValueConverter
{
    public static readonly BoolToFeedbackBrushConverter Instance = new();

    private static readonly SolidColorBrush ErrorBrush = new(Color.FromRgb(0xFB, 0xE2, 0xE2));
    private static readonly SolidColorBrush OkBrush = new(Color.FromRgb(0xE3, 0xF2, 0xE1));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? ErrorBrush : OkBrush;

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
