using BimBam.Core.Models;

namespace BimBam.Core.Interfaces;

/// <summary>
/// Reads order lines from an uploaded Excel file (PARTNR, QTY, GEEVERS_NUMBER, ORDER, PRICE, TOTAL).
/// </summary>
public interface IExcelOrderImportService
{
    Task<ImportedOrderFile> ReadAsync(string excelFilePath, CancellationToken ct = default);
}
