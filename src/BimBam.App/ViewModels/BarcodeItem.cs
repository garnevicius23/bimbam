namespace BimBam.App.ViewModels;

/// <summary>A printable/on-screen barcode with its caption, used for the command barcode cards.</summary>
public sealed class BarcodeItem
{
    public required string Code { get; init; }

    public required string Caption { get; init; }
}
