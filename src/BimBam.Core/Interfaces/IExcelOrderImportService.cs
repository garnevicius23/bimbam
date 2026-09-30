using BimBam.Core.Models;

namespace BimBam.Core.Interfaces;

/// <summary>
/// Imports order lines from an uploaded Excel file (PARTNR, QTY, GEEVERS_NUMBER, ORDER, PRICE, TOTAL)
/// and merges them into an existing session, indicating any parts that already existed.
/// </summary>
public interface IExcelOrderImportService
{
    /// <summary>
    /// Reads the given Excel file and adds its lines to the session, merging with any existing
    /// line that has the same part number (ignoring spaces and case).
    /// </summary>
    /// <returns>The set of part numbers that were merged with a pre-existing line.</returns>
    Task<IReadOnlyList<string>> ImportAsync(OrderSession session, string excelFilePath, CancellationToken ct = default);
}
