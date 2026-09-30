using BimBam.Core.Models;

namespace BimBam.Core.Interfaces;

/// <summary>
/// Prints part and container labels on the configured label printer (Brother QL-800).
/// </summary>
public interface ILabelPrintService
{
    Task PrintPartLabelAsync(OrderLine line, LabelSettings settings, CancellationToken ct = default);

    Task PrintContainerLabelAsync(Container container, LabelSettings settings, CancellationToken ct = default);

    /// <summary>Prints the A4 control sheet listing every container barcode plus the command barcodes.</summary>
    Task PrintControlSheetAsync(OrderSession session, CommandBarcodes commands, string? printerName = null, CancellationToken ct = default);
}
