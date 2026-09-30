using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using BimBam.Core.Interfaces;
using BimBam.Core.Models;
using PrintDialog = System.Windows.Controls.PrintDialog;

namespace BimBam.Infrastructure.Services;

/// <summary>
/// Prints part labels and container labels on the configured label printer (Brother QL-800,
/// 62 mm continuous roll), and the A4 control sheet of container and command barcodes on the
/// report printer.
/// </summary>
public sealed class LabelPrintService : ILabelPrintService
{
    private const double MmToDip = 96.0 / 25.4;

    public Task PrintPartLabelAsync(OrderLine line, LabelSettings settings, CancellationToken ct = default)
    {
        var visual = BuildLabelVisual(line.PartNumber, $"{line.PartNumber}  ({line.GeeversNumber})", settings);
        Print(visual, settings.LabelWidthMm, settings.LabelHeightMm, settings.PrinterName, $"BimBam etiketė {line.PartNumber}");
        return Task.CompletedTask;
    }

    public Task PrintContainerLabelAsync(Container container, LabelSettings settings, CancellationToken ct = default)
    {
        var caption = string.IsNullOrWhiteSpace(container.DisplayName) ? container.Code : $"{container.Code} – {container.DisplayName}";
        var visual = BuildLabelVisual(container.Code, caption, settings);
        Print(visual, settings.LabelWidthMm, settings.LabelHeightMm, settings.PrinterName, $"BimBam konteineris {container.Code}");
        return Task.CompletedTask;
    }

    public Task PrintControlSheetAsync(OrderSession session, CommandBarcodes commands, string? printerName = null, CancellationToken ct = default)
    {
        var doc = new FlowDocument
        {
            PageWidth = 210 * MmToDip,
            PageHeight = 297 * MmToDip,
            PagePadding = new Thickness(20),
            FontFamily = new FontFamily("Segoe UI")
        };

        doc.Blocks.Add(new Paragraph(new Run($"BimBam – Valdymo lapas: {session.Name}"))
        {
            FontSize = 20,
            FontWeight = FontWeights.Bold
        });

        doc.Blocks.Add(new Paragraph(new Run("Veiksmų kodai")) { FontSize = 16, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 16, 0, 0) });
        AddBarcodeParagraph(doc, commands.Print, "Spausdinti etiketę");
        AddBarcodeParagraph(doc, commands.Undo, "Atšaukti paskutinį veiksmą");
        AddBarcodeParagraph(doc, commands.NextPart, "Kita detalė");
        AddBarcodeParagraph(doc, commands.EnterQuantity, "Įvesti kiekį");

        doc.Blocks.Add(new Paragraph(new Run("Konteinerių kodai")) { FontSize = 16, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 16, 0, 0) });
        foreach (var container in session.Containers.OrderBy(c => c.Code))
        {
            var caption = string.IsNullOrWhiteSpace(container.DisplayName) ? container.Code : $"{container.Code} – {container.DisplayName}";
            AddBarcodeParagraph(doc, container.Code, caption);
        }

        var paginator = ((IDocumentPaginatorSource)doc).DocumentPaginator;
        var dialog = new PrintDialog();

        // Pre-select the configured report printer only as a starting suggestion; the sheet is
        // printed to whichever printer the user confirms in the dialog below, never silently, so
        // it can never end up on the label/sticker printer by mistake.
        TryPreselectPrintQueue(dialog, printerName);

        if (dialog.ShowDialog() != true)
        {
            return Task.CompletedTask;
        }

        paginator.PageSize = new Size(dialog.PrintableAreaWidth, dialog.PrintableAreaHeight);
        dialog.PrintDocument(paginator, $"BimBam valdymo lapas – {session.Name}");
        return Task.CompletedTask;
    }

    private static void AddBarcodeParagraph(FlowDocument doc, string value, string caption)
    {
        var bitmap = BarcodeImageFactory.Create(value, pixelWidth: 500, pixelHeight: 90);
        var image = new Image { Source = bitmap, Width = 260, Height = 47, Stretch = Stretch.Fill };

        var paragraph = new Paragraph { Margin = new Thickness(0, 0, 0, 10) };
        paragraph.Inlines.Add(new InlineUIContainer(image));
        paragraph.Inlines.Add(new LineBreak());
        paragraph.Inlines.Add(new Run(caption) { FontSize = 14 });
        doc.Blocks.Add(paragraph);
    }

    private static FrameworkElement BuildLabelVisual(string barcodeValue, string captionText, LabelSettings settings)
    {
        var widthDip = settings.LabelWidthMm * MmToDip;
        var heightDip = settings.LabelHeightMm * MmToDip;
        var barcodeHeightDip = settings.BarcodeHeightMm * MmToDip;

        var bitmap = BarcodeImageFactory.Create(barcodeValue, pixelWidth: 600, pixelHeight: 150);
        var image = new Image
        {
            Source = bitmap,
            Width = widthDip - 8,
            Height = barcodeHeightDip,
            Stretch = Stretch.Fill,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        var text = new TextBlock
        {
            Text = captionText,
            FontSize = settings.PartNumberFontSize,
            FontWeight = FontWeights.Bold,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        var panel = new StackPanel
        {
            Width = widthDip,
            Height = heightDip,
            Margin = new Thickness(4),
            Children = { image, text }
        };

        panel.Measure(new Size(widthDip, heightDip));
        panel.Arrange(new Rect(0, 0, widthDip, heightDip));
        return panel;
    }

    private static void Print(Visual visual, double widthMm, double heightMm, string? printerName, string description)
    {
        var dialog = new PrintDialog();
        if (!TrySetPrintQueue(dialog, printerName))
        {
            return;
        }

        dialog.PrintTicket.PageMediaSize = new System.Printing.PageMediaSize(widthMm * MmToDip, heightMm * MmToDip);
        dialog.PrintVisual(visual, description);
    }

    private static bool TrySetPrintQueue(PrintDialog dialog, string? printerName)
    {
        if (string.IsNullOrWhiteSpace(printerName))
        {
            return true;
        }

        var server = new System.Printing.LocalPrintServer();
        var queue = server.GetPrintQueues().FirstOrDefault(q => q.FullName.Equals(printerName, StringComparison.OrdinalIgnoreCase));
        if (queue is null)
        {
            return false;
        }

        dialog.PrintQueue = queue;
        return true;
    }

    private static void TryPreselectPrintQueue(PrintDialog dialog, string? printerName)
    {
        if (string.IsNullOrWhiteSpace(printerName))
        {
            return;
        }

        var server = new System.Printing.LocalPrintServer();
        var queue = server.GetPrintQueues().FirstOrDefault(q => q.FullName.Equals(printerName, StringComparison.OrdinalIgnoreCase));
        if (queue is not null)
        {
            dialog.PrintQueue = queue;
        }
    }
}
