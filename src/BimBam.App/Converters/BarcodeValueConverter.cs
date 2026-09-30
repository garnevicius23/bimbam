using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using BimBam.Infrastructure.Services;

namespace BimBam.App.Converters;

/// <summary>Renders a barcode value (part number, container code, or command code) as an image
/// for on-screen display, reusing the same renderer used for printed labels.</summary>
public sealed class BarcodeValueConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string text || string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return BarcodeImageFactory.Create(text, pixelWidth: 300, pixelHeight: 90);
    }

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
