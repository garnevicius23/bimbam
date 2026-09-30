namespace BimBam.Core.Models;

/// <summary>
/// The configurable barcode values printed on the A4 control sheet for hands-free operation
/// (print, undo, next part, open quantity entry).
/// </summary>
public sealed class CommandBarcodes
{
    public string Print { get; set; } = "*SPAUSDINTI*";

    public string Undo { get; set; } = "*ATSAUKTI*";

    public string NextPart { get; set; } = "*KITAS*";

    public string EnterQuantity { get; set; } = "*KIEKIS*";
}
