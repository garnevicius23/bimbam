namespace BimBam.Core.Models;

/// <summary>One row of an uploaded order Excel file, as read from the file before being merged.</summary>
public sealed class ImportedOrderRow
{
    public required string PartNumber { get; init; }

    public required string GeeversNumber { get; init; }

    public required string OrderNumber { get; init; }

    public int Quantity { get; init; }

    public decimal UnitPrice { get; init; }

    public decimal Total { get; init; }
}
