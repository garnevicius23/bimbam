namespace BimBam.Core.Models;

/// <summary>
/// One uploaded source order file that has been imported into a session.
/// </summary>
public sealed class ImportedFile
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public Guid SessionId { get; init; }

    /// <summary>The order number as found in the file, e.g. "VLD1534647".</summary>
    public required string OrderNumber { get; set; }

    public required string FileName { get; set; }

    public DateTimeOffset ImportedAt { get; set; } = DateTimeOffset.Now;

    public int LineCount { get; set; }
}
