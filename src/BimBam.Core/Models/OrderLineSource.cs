namespace BimBam.Core.Models;

/// <summary>
/// Records that part of an order line's expected quantity came from a specific source order file,
/// used to show the "merged" badge and its tooltip when the same part number appears in more than
/// one imported file within the same session.
/// </summary>
public sealed class OrderLineSource
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public Guid OrderLineId { get; init; }

    public required string OrderNumber { get; set; }

    public int Quantity { get; set; }
}
