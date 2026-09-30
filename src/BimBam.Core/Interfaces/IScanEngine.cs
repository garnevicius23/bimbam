using BimBam.Core.Models;

namespace BimBam.Core.Interfaces;

/// <summary>
/// Drives the scan state machine for one active session: interprets each scanned barcode as a
/// part, a container, or a command, applying the configured repeat-scan and timer behavior.
/// </summary>
public interface IScanEngine
{
    /// <summary>The order line currently active (awaiting a repeat scan, container, or command), if any.</summary>
    OrderLine? ActiveLine { get; }

    /// <summary>
    /// Time remaining in the repeat-scan window for <see cref="ActiveLine"/>, or null when there is
    /// no active line or the window has already expired.
    /// </summary>
    TimeSpan? RemainingWindow { get; }

    /// <summary>Feeds one scanned barcode value into the engine and returns the resulting action.</summary>
    ScanResult ProcessScan(string rawBarcode);

    /// <summary>Manually sets the actual quantity for the active line.</summary>
    ScanResult SetQuantity(int quantity);

    /// <summary>Cancels the currently active line without recording a quantity change.</summary>
    void ClearActiveLine();
}
