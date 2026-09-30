namespace BimBam.Core.Models;

/// <summary>
/// Label layout and printer settings for the Brother QL-800 (62 mm continuous roll).
/// </summary>
public sealed class LabelSettings
{
    public string? PrinterName { get; set; }

    public double LabelWidthMm { get; set; } = 62;

    public double LabelHeightMm { get; set; } = 29;

    public int BarcodeHeightMm { get; set; } = 14;

    public int PartNumberFontSize { get; set; } = 18;
}
