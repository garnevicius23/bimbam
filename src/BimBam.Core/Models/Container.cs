namespace BimBam.Core.Models;

/// <summary>
/// A physical container that reviewed parts are placed into, identified by its own printed barcode.
/// </summary>
public sealed class Container
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public Guid SessionId { get; init; }

    /// <summary>Barcode value printed on the container label, e.g. "K-001".</summary>
    public required string Code { get; set; }

    public string? DisplayName { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
}
