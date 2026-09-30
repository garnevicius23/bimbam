namespace BimBam.Core.Enums;

/// <summary>
/// Category of a recorded scan-session event, used for the audit log and undo.
/// </summary>
public enum ScanEventType
{
    PartScanned,
    QuantityIncremented,
    QuantitySetManually,
    LabelPrinted,
    AssignedToContainer,
    ActionUndone,
    UnknownBarcodeScanned
}
