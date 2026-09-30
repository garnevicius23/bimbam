using BimBam.Core.Enums;
using BimBam.Core.Interfaces;
using BimBam.Core.Models;
using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.IO;

namespace BimBam.Infrastructure.Services;

/// <summary>
/// Builds the final order review report (PDF export, Excel export and direct A4 printing) from a
/// completed session: expected vs. actual quantities, prices, statuses and container assignments.
/// </summary>
public sealed class ReportService : IReportService
{
    static ReportService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public Task<string> ExportPdfAsync(OrderSession session, string outputPath, CancellationToken ct = default)
    {
        var containerNames = session.Containers.ToDictionary(c => c.Id, c => c.Code);

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(24);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Header().Text($"BimBam – Užsakymo ataskaita: {session.Name}")
                    .FontSize(16).Bold();

                page.Content().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(2.2f); // part number
                        c.RelativeColumn(1.4f); // geevers
                        c.RelativeColumn(0.8f); // expected qty
                        c.RelativeColumn(0.8f); // actual qty
                        c.RelativeColumn(1.1f); // status
                        c.RelativeColumn(0.9f); // container
                        c.RelativeColumn(0.9f); // unit price
                        c.RelativeColumn(1.0f); // expected total
                        c.RelativeColumn(1.0f); // actual total
                    });

                    void Header(string text) => table.Cell().Background(Colors.Grey.Lighten2).Padding(3).Text(text).Bold();
                    Header("Detalė"); Header("Geevers nr."); Header("Tikėtasi"); Header("Faktiškai");
                    Header("Būsena"); Header("Konteineris"); Header("Kaina"); Header("Suma (tikėtasi)"); Header("Suma (faktiškai)");

                    decimal expectedSum = 0, actualSum = 0;
                    foreach (var line in session.Lines.OrderBy(l => l.PartNumber))
                    {
                        var actualTotal = line.UnitPrice * line.ActualQuantity;
                        expectedSum += line.ExpectedTotal;
                        actualSum += actualTotal;
                        var containerName = line.ContainerId is not null && containerNames.TryGetValue(line.ContainerId.Value, out var c) ? c : "-";

                        table.Cell().Padding(3).Text(line.PartNumber + (line.IsMerged ? " (sujungta)" : string.Empty));
                        table.Cell().Padding(3).Text(line.GeeversNumber);
                        table.Cell().Padding(3).Text(line.ExpectedQuantity.ToString());
                        table.Cell().Padding(3).Text(line.ActualQuantity.ToString());
                        table.Cell().Padding(3).Text(StatusText(line.Status));
                        table.Cell().Padding(3).Text(containerName);
                        table.Cell().Padding(3).Text(line.UnitPrice.ToString("0.00"));
                        table.Cell().Padding(3).Text(line.ExpectedTotal.ToString("0.00"));
                        table.Cell().Padding(3).Text(actualTotal.ToString("0.00"));
                    }

                    table.Cell().ColumnSpan(7).Padding(3).AlignRight().Text("Iš viso:").Bold();
                    table.Cell().Padding(3).Text(expectedSum.ToString("0.00")).Bold();
                    table.Cell().Padding(3).Text(actualSum.ToString("0.00")).Bold();
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("Sugeneruota ");
                    x.Span(DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm"));
                });
            });
        }).GeneratePdf(outputPath);

        return Task.FromResult(outputPath);
    }

    public Task<string> ExportExcelAsync(OrderSession session, string outputPath, CancellationToken ct = default)
    {
        var containerNames = session.Containers.ToDictionary(c => c.Id, c => c.Code);

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Ataskaita");

        string[] headers =
        [
            "Detalė", "Geevers nr.", "Užsakymas", "Tikėtasi", "Faktiškai", "Būsena",
            "Konteineris", "Kaina", "Suma (tikėtasi)", "Suma (faktiškai)", "Sujungta"
        ];
        for (var i = 0; i < headers.Length; i++)
        {
            sheet.Cell(1, i + 1).Value = headers[i];
            sheet.Cell(1, i + 1).Style.Font.Bold = true;
        }

        var row = 2;
        foreach (var line in session.Lines.OrderBy(l => l.PartNumber))
        {
            var containerName = line.ContainerId is not null && containerNames.TryGetValue(line.ContainerId.Value, out var c) ? c : string.Empty;
            sheet.Cell(row, 1).Value = line.PartNumber;
            sheet.Cell(row, 2).Value = line.GeeversNumber;
            sheet.Cell(row, 3).Value = line.OrderNumber;
            sheet.Cell(row, 4).Value = line.ExpectedQuantity;
            sheet.Cell(row, 5).Value = line.ActualQuantity;
            sheet.Cell(row, 6).Value = StatusText(line.Status);
            sheet.Cell(row, 7).Value = containerName;
            sheet.Cell(row, 8).Value = line.UnitPrice;
            sheet.Cell(row, 9).Value = line.ExpectedTotal;
            sheet.Cell(row, 10).Value = line.UnitPrice * line.ActualQuantity;
            sheet.Cell(row, 11).Value = line.IsMerged ? "Taip" : string.Empty;
            row++;
        }

        sheet.Columns().AdjustToContents();
        workbook.SaveAs(outputPath);
        return Task.FromResult(outputPath);
    }

    public async Task PrintAsync(OrderSession session, string printerName, CancellationToken ct = default)
    {
        var tempPdf = Path.Combine(Path.GetTempPath(), $"BimBam_{session.Id}.pdf");
        await ExportPdfAsync(session, tempPdf, ct);

        // Hand the generated PDF to the Windows default PDF handler's print verb so it is sent
        // to the given printer without needing a bundled PDF rendering/printing engine.
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = tempPdf,
            Verb = "printto",
            Arguments = $"\"{printerName}\"",
            UseShellExecute = true,
            WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
        };
        using var process = System.Diagnostics.Process.Start(psi);
    }

    private static string StatusText(LineStatus status) => status switch
    {
        LineStatus.Matches => "Sutampa",
        LineStatus.Shortage => "Trūksta",
        LineStatus.Surplus => "Perteklius",
        _ => "Netikrinta"
    };
}
