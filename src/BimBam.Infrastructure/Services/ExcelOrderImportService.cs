using System.IO;
using BimBam.Core.Interfaces;
using BimBam.Core.Models;
using ClosedXML.Excel;

namespace BimBam.Infrastructure.Services;

/// <summary>
/// Reads PARTNR/QTY/GEEVERS_NUMBER/ORDER/PRICE/TOTAL columns from an uploaded order Excel file.
/// Merging into the session happens when the resulting import operation is applied, so every
/// laptop merges the same rows identically.
/// </summary>
public sealed class ExcelOrderImportService : IExcelOrderImportService
{
    public Task<ImportedOrderFile> ReadAsync(string excelFilePath, CancellationToken ct = default)
    {
        using var workbook = new XLWorkbook(excelFilePath);
        var sheet = workbook.Worksheets.First();
        var header = sheet.Row(1).CellsUsed().ToDictionary(c => c.GetString().Trim().ToUpperInvariant(), c => c.Address.ColumnNumber);

        int Col(string name) => header.TryGetValue(name, out var col)
            ? col
            : throw new InvalidDataException($"Excel faile nerasta stulpelio \"{name}\".");

        var colPartNr = Col("PARTNR");
        var colQty = Col("QTY");
        var colGeevers = Col("GEEVERS_NUMBER");
        var colOrder = Col("ORDER");
        var colPrice = Col("PRICE");
        var colTotal = header.TryGetValue("TOTAL", out var t) ? t : (int?)null;

        var rows = new List<ImportedOrderRow>();
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;
        for (var r = 2; r <= lastRow; r++)
        {
            var row = sheet.Row(r);
            var partNr = row.Cell(colPartNr).GetString().Trim();
            if (partNr.Length == 0)
            {
                continue;
            }

            var qty = (int)row.Cell(colQty).GetDouble();
            var price = (decimal)row.Cell(colPrice).GetDouble();
            rows.Add(new ImportedOrderRow
            {
                PartNumber = partNr,
                GeeversNumber = row.Cell(colGeevers).GetString().Trim(),
                OrderNumber = row.Cell(colOrder).GetString().Trim(),
                Quantity = qty,
                UnitPrice = price,
                Total = colTotal is not null ? (decimal)row.Cell(colTotal.Value).GetDouble() : price * qty
            });
        }

        return Task.FromResult(new ImportedOrderFile
        {
            FileName = Path.GetFileName(excelFilePath),
            OrderNumber = rows.FirstOrDefault()?.OrderNumber ?? Path.GetFileNameWithoutExtension(excelFilePath),
            Rows = rows
        });
    }
}
