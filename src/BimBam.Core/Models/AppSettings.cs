using BimBam.Core.Enums;

namespace BimBam.Core.Models;

/// <summary>
/// User-configurable application settings: printers, storage location, scan timing and behavior.
/// </summary>
public sealed class AppSettings
{
    public string SessionsFolderPath { get; set; } = string.Empty;

    public LabelSettings LabelPrinter { get; set; } = new();

    public string? ReportPrinterName { get; set; }

    public int RepeatScanWindowSeconds { get; set; } = 30;

    public RepeatScanBehavior RepeatScanBehavior { get; set; } = RepeatScanBehavior.IncrementQuantity;

    public CommandBarcodes CommandBarcodes { get; set; } = new();

    /// <summary>Prefix that identifies a scanned code as a container rather than a part, e.g. "K-".</summary>
    public string ContainerBarcodePrefix { get; set; } = "K-";

    public bool SoundsEnabled { get; set; } = true;
}
