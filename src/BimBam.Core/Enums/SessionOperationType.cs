namespace BimBam.Core.Enums;

/// <summary>Kind of change recorded in a session's operation log.</summary>
public enum SessionOperationType
{
    /// <summary>Full state of a session saved before operation logs existed; replaces all state.</summary>
    LegacyBaseline,
    ImportFile,
    AdjustQuantity,
    SetQuantity,
    AllocateToContainer,
    CreateContainer,
    LabelPrinted,

    /// <summary>A person checked a line flagged for review after a conflict.</summary>
    ReviewCleared
}
