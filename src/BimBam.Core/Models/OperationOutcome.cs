namespace BimBam.Core.Models;

/// <summary>What applying one operation changed, for user feedback.</summary>
public sealed class OperationOutcome
{
    public static readonly OperationOutcome None = new();

    /// <summary>Part numbers from an import that were merged into an already existing line.</summary>
    public IReadOnlyList<string> MergedPartNumbers { get; init; } = [];

    /// <summary>Pieces actually placed by an allocation (may be fewer than requested).</summary>
    public int AllocatedQuantity { get; init; }
}
