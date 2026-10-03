namespace BimBam.Core.Models;

/// <summary>The parsed contents of one uploaded order Excel file.</summary>
public sealed class ImportedOrderFile
{
    public required string FileName { get; init; }

    public required string OrderNumber { get; init; }

    public List<ImportedOrderRow> Rows { get; init; } = [];
}
