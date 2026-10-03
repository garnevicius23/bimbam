using BimBam.Core.Enums;

namespace BimBam.Core.Models;

/// <summary>
/// Outcome of feeding one scanned barcode into the scan engine, used by the UI to update the
/// screen and play the appropriate sound.
/// </summary>
public sealed class ScanResult
{
    public required ScanEventType EventType { get; init; }

    public OrderLine? Line { get; init; }

    public Container? Container { get; init; }

    public string? Message { get; init; }

    /// <summary>Pieces affected by the action, e.g. how many were placed into a container.</summary>
    public int? Quantity { get; init; }

    public bool IsError { get; init; }

    public static ScanResult UnknownBarcode(string raw) => new()
    {
        EventType = ScanEventType.UnknownBarcodeScanned,
        Message = raw,
        IsError = true
    };
}
