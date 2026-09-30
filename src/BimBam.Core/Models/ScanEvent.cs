using BimBam.Core.Enums;

namespace BimBam.Core.Models;

/// <summary>
/// An audit-log entry for a single scan-session action, used for history review and undo.
/// </summary>
public sealed class ScanEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public Guid SessionId { get; init; }

    public Guid? OrderLineId { get; init; }

    public Guid? ContainerId { get; init; }

    public required ScanEventType Type { get; set; }

    /// <summary>Raw barcode value that was scanned, if this event originated from a scan.</summary>
    public string? RawBarcode { get; set; }

    public int? QuantityAfter { get; set; }

    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.Now;
}
