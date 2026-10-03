namespace BimBam.Core.Models;

/// <summary>
/// A physical container that reviewed parts are placed into, identified by its own printed barcode.
/// </summary>
public sealed class Container
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public Guid SessionId { get; init; }

    /// <summary>Barcode value printed on the container label, e.g. "K-001" or "K-A-001".</summary>
    public required string Code { get; set; }

    public string? DisplayName { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;

    /// <summary>Name of the laptop that created the container (empty for older sessions).</summary>
    public string CreatedBy { get; set; } = string.Empty;

    public Container Clone() => new()
    {
        Id = Id,
        SessionId = SessionId,
        Code = Code,
        DisplayName = DisplayName,
        CreatedAt = CreatedAt,
        CreatedBy = CreatedBy
    };
}
