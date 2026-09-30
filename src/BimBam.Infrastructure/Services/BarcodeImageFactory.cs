using System.Windows.Media.Imaging;
using ZXing;
using ZXing.Common;
using ZXing.Windows.Compatibility;

namespace BimBam.Infrastructure.Services;

/// <summary>
/// Renders Code128 barcodes (used for part numbers and container/command codes) as WPF bitmaps
/// for on-screen display and printing.
/// </summary>
public static class BarcodeImageFactory
{
    public static WriteableBitmap Create(string value, int pixelWidth = 400, int pixelHeight = 120)
    {
        var writer = new BarcodeWriterWriteableBitmap
        {
            Format = BarcodeFormat.CODE_128,
            Options = new EncodingOptions
            {
                Width = pixelWidth,
                Height = pixelHeight,
                Margin = 4,
                PureBarcode = false
            }
        };

        return writer.Write(value);
    }
}
