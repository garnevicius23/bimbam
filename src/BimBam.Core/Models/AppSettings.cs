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

    /// <summary>Generated once per laptop; identifies it when sharing sessions.</summary>
    public Guid DeviceId { get; set; }

    /// <summary>Name other laptops see (defaults to the Windows computer name).</summary>
    public string DeviceName { get; set; } = string.Empty;

    /// <summary>Letter used in container codes this laptop creates while sharing (K-A-001).
    /// The host hands out a free letter if two laptops pick the same one.</summary>
    public string DeviceLetter { get; set; } = "A";

    public DeviceIdentity ToDeviceIdentity() => new()
    {
        DeviceId = DeviceId,
        DeviceName = string.IsNullOrWhiteSpace(DeviceName) ? Environment.MachineName : DeviceName,
        DeviceLetter = string.IsNullOrWhiteSpace(DeviceLetter) ? "A" : DeviceLetter.Trim().ToUpperInvariant()
    };
}
