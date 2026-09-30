namespace BimBam.Core.Enums;

/// <summary>
/// Configures what a second scan of the same part within the active window does.
/// </summary>
public enum RepeatScanBehavior
{
    /// <summary>Increments the actual quantity by one and restarts the timer.</summary>
    IncrementQuantity,

    /// <summary>Immediately triggers label printing and restarts the timer.</summary>
    TriggerPrint
}
