using BimBam.Core.Models;

namespace BimBam.Core.Interfaces;

/// <summary>
/// Builds and exports the final review report for a session (PDF, printout, and Excel export).
/// </summary>
public interface IReportService
{
    Task<string> ExportPdfAsync(OrderSession session, string outputPath, CancellationToken ct = default);

    Task<string> ExportExcelAsync(OrderSession session, string outputPath, CancellationToken ct = default);

    Task PrintAsync(OrderSession session, string printerName, CancellationToken ct = default);
}
