using BimBam.Core.Interfaces;
using BimBam.Core.Models;
using BimBam.Core.Services;
using ClosedXML.Excel;
using System.IO;

namespace BimBam.Infrastructure.Services;

/// <summary>
/// Reads PARTNR/QTY/GEEVERS_NUMBER/ORDER/PRICE/TOTAL columns from an uploaded order Excel file
/// and merges them into a session, tracking per-file sources so parts that appear in more than
/// one uploaded file can be flagged as merged.
/// </summary>
public sealed class ExcelOrderImportService : IExcelOrderImportService
{
    public Task<IReadOnlyList<string>> ImportAsync(OrderSession session, string excelFilePath, CancellationToken ct = default)
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

        var merged = new List<string>();
        string? orderNumber = null;
        var lineCount = 0;

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
            var geevers = row.Cell(colGeevers).GetString().Trim();
            var order = row.Cell(colOrder).GetString().Trim();
            var price = (decimal)row.Cell(colPrice).GetDouble();
            var total = colTotal is not null ? (decimal)row.Cell(colTotal.Value).GetDouble() : price * qty;

            orderNumber ??= order;
            lineCount++;

            var existing = session.Lines.FirstOrDefault(l => PartNumberMatcher.AreEqual(l.PartNumber, partNr));
            if (existing is null)
            {
                var newLine = new OrderLine
                {
                    SessionId = session.Id,
                    PartNumber = partNr,
                    GeeversNumber = geevers,
                    OrderNumber = order,
                    ExpectedQuantity = qty,
                    UnitPrice = price,
                    ExpectedTotal = total
                };
                newLine.Sources.Add(new OrderLineSource { OrderLineId = newLine.Id, OrderNumber = order, Quantity = qty });
                session.Lines.Add(newLine);
            }
            else
            {
                existing.ExpectedQuantity += qty;
                existing.ExpectedTotal += total;
                existing.Sources.Add(new OrderLineSource { OrderLineId = existing.Id, OrderNumber = order, Quantity = qty });
                merged.Add(partNr);
            }
        }

        session.ImportedFiles.Add(new ImportedFile
        {
            SessionId = session.Id,
            OrderNumber = orderNumber ?? Path.GetFileNameWithoutExtension(excelFilePath),
            FileName = Path.GetFileName(excelFilePath),
            LineCount = lineCount
        });
        session.UpdatedAt = DateTimeOffset.Now;

        return Task.FromResult<IReadOnlyList<string>>(merged);
    }
}
