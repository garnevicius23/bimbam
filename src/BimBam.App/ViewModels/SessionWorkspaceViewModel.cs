using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using BimBam.Core.Enums;
using BimBam.Core.Interfaces;
using BimBam.Core.Models;
using BimBam.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;

namespace BimBam.App.ViewModels;

/// <summary>
/// Drives one open order session: the scan input, the live order table, containers, and report
/// export/printing, backed by the <see cref="Core.Services.ScanEngine"/> state machine.
/// </summary>
public sealed partial class SessionWorkspaceViewModel : ObservableObject, IDisposable
{
    private readonly ISettingsService _settingsService;
    private readonly ISessionRepository _sessionRepository;
    private readonly IExcelOrderImportService _excelImportService;
    private readonly ILabelPrintService _labelPrintService;
    private readonly IReportService _reportService;
    private readonly IScanEngine _scanEngine;
    private readonly DispatcherTimer _timer;

    public SessionWorkspaceViewModel(
        OrderSession session,
        ISettingsService settingsService,
        ISessionRepository sessionRepository,
        IExcelOrderImportService excelImportService,
        ILabelPrintService labelPrintService,
        IReportService reportService,
        ISystemClock clock)
    {
        Session = session;
        _settingsService = settingsService;
        _sessionRepository = sessionRepository;
        _excelImportService = excelImportService;
        _labelPrintService = labelPrintService;
        _reportService = reportService;
        _scanEngine = new ScanEngine(session, settingsService.Current, clock);

        Lines = new ObservableCollection<OrderLine>(session.Lines);
        Containers = new ObservableCollection<Container>(session.Containers);

        var commands = settingsService.Current.CommandBarcodes;
        CommandBarcodeItems =
        [
            new BarcodeItem { Code = commands.Print, Caption = "Spausdinti etiketę" },
            new BarcodeItem { Code = commands.Undo, Caption = "Atšaukti paskutinį veiksmą" },
            new BarcodeItem { Code = commands.NextPart, Caption = "Kita detalė" },
            new BarcodeItem { Code = commands.EnterQuantity, Caption = "Įvesti kiekį" }
        ];


        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _timer.Tick += (_, _) => SecondsRemaining = _scanEngine.RemainingWindow is { } remaining ? (int)Math.Ceiling(remaining.TotalSeconds) : null;
        _timer.Start();
    }


    public OrderSession Session { get; }

    public ObservableCollection<OrderLine> Lines { get; }

    public ObservableCollection<Container> Containers { get; }

    public List<BarcodeItem> CommandBarcodeItems { get; }

    [ObservableProperty]
    private string scanBuffer = string.Empty;

    [ObservableProperty]
    private OrderLine? activeLine;

    [ObservableProperty]
    private int? secondsRemaining;

    [ObservableProperty]
    private string? feedbackMessage;

    [ObservableProperty]
    private bool feedbackIsError;

    public void SubmitScan()
    {
        var raw = ScanBuffer;
        ScanBuffer = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return;
        }

        var result = _scanEngine.ProcessScan(raw);
        ActiveLine = _scanEngine.ActiveLine;
        ApplyResult(result);
    }

    private void ApplyResult(ScanResult result)
    {
        FeedbackIsError = result.IsError;
        FeedbackMessage = result.EventType switch
        {
            ScanEventType.PartScanned => $"Pasirinkta detalė: {result.Line!.PartNumber}",
            ScanEventType.QuantityIncremented => $"Kiekis: {result.Line!.ActualQuantity}",
            ScanEventType.QuantitySetManually => $"Kiekis nustatytas: {result.Line!.ActualQuantity}",
            ScanEventType.LabelPrinted => $"Spausdinama etiketė: {result.Line!.PartNumber}",
            ScanEventType.AssignedToContainer => $"{result.Line!.PartNumber} priskirta konteineriui {result.Container!.Code}",
            ScanEventType.ActionUndone => result.Message ?? "Veiksmas atšauktas",
            ScanEventType.UnknownBarcodeScanned => $"Nežinomas kodas: {result.Message}",
            _ => result.Message
        };

        if (result.EventType == ScanEventType.LabelPrinted && result.Line is not null)
        {
            _ = _labelPrintService.PrintPartLabelAsync(result.Line, _settingsService.Current.LabelPrinter);
        }

        _ = _sessionRepository.AppendScanEventAsync(new ScanEvent
        {
            SessionId = Session.Id,
            OrderLineId = result.Line?.Id,
            ContainerId = result.Container?.Id,
            Type = result.EventType,
            RawBarcode = result.Message,
            QuantityAfter = result.Line?.ActualQuantity
        });

        _ = _sessionRepository.SaveSessionAsync(Session);
    }

    [ObservableProperty]
    private string quantityInput = string.Empty;

    [RelayCommand]
    private void ApplyQuantity()
    {
        if (!int.TryParse(QuantityInput, out var quantity))
        {
            FeedbackIsError = true;
            FeedbackMessage = "Įveskite skaičių.";
            return;
        }

        var result = _scanEngine.SetQuantity(quantity);
        ActiveLine = _scanEngine.ActiveLine;
        ApplyResult(result);
        QuantityInput = string.Empty;
    }

    [RelayCommand]
    private async Task AddFilesToSessionAsync(Window owner)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Pasirinkite papildomą(-us) užsakymo Excel failą(-us)",
            Filter = "Excel failai (*.xlsx)|*.xlsx",
            Multiselect = true
        };

        if (dialog.ShowDialog(owner) != true)
        {
            return;
        }

        var mergedTotal = new List<string>();
        foreach (var file in dialog.FileNames)
        {
            var merged = await _excelImportService.ImportAsync(Session, file);
            mergedTotal.AddRange(merged);
        }

        // Lines that merged into an already-existing part number are updated in place (and the
        // DataGrid picks that up via INotifyPropertyChanged); only genuinely new part numbers
        // need to be added to the bound collection here.
        var knownIds = Lines.Select(l => l.Id).ToHashSet();
        foreach (var line in Session.Lines.Where(l => !knownIds.Contains(l.Id)))
        {
            Lines.Add(line);
        }

        await _sessionRepository.SaveSessionAsync(Session);

        FeedbackIsError = false;
        FeedbackMessage = mergedTotal.Count > 0
            ? $"Failai importuoti. Sujungtos detalės: {string.Join(", ", mergedTotal.Distinct())}"
            : "Failai importuoti.";
    }

    [RelayCommand]
    private async Task AddContainerAsync(Window owner)
    {
        var prefix = _settingsService.Current.ContainerBarcodePrefix;
        var nextNumber = Containers.Count + 1;
        var suggestedCode = $"{prefix}{nextNumber:000}";

        var code = Views.TextInputWindow.Ask(owner, "Naujo konteinerio kodas:", suggestedCode);
        if (string.IsNullOrWhiteSpace(code))
        {
            return;
        }

        var displayName = Views.TextInputWindow.Ask(owner, "Konteinerio pavadinimas (neprivaloma):");

        var container = new Container { SessionId = Session.Id, Code = code, DisplayName = displayName };
        Session.Containers.Add(container);
        Containers.Add(container);
        await _sessionRepository.SaveSessionAsync(Session);

        await _labelPrintService.PrintContainerLabelAsync(container, _settingsService.Current.LabelPrinter);
    }

    [RelayCommand]
    private Task PrintContainerLabelAsync(Container container) =>
        _labelPrintService.PrintContainerLabelAsync(container, _settingsService.Current.LabelPrinter);

    [RelayCommand]
    private void ShowContainerContents(Container container)
    {
        var lines = Session.Lines.Where(l => l.ContainerId == container.Id).ToList();
        var window = new Views.ContainerContentsWindow(container, lines) { Owner = Application.Current.MainWindow };
        window.Show();
    }

    [RelayCommand]
    private Task PrintControlSheetAsync() =>
        _labelPrintService.PrintControlSheetAsync(Session, _settingsService.Current.CommandBarcodes, _settingsService.Current.ReportPrinterName);

    [RelayCommand]
    private async Task ExportReportPdfAsync(Window owner)
    {
        var dialog = new SaveFileDialog { Filter = "PDF failai (*.pdf)|*.pdf", FileName = $"{Session.Name}.pdf" };
        if (dialog.ShowDialog(owner) == true)
        {
            await _reportService.ExportPdfAsync(Session, dialog.FileName);
        }
    }

    [RelayCommand]
    private async Task ExportReportExcelAsync(Window owner)
    {
        var dialog = new SaveFileDialog { Filter = "Excel failai (*.xlsx)|*.xlsx", FileName = $"{Session.Name}.xlsx" };
        if (dialog.ShowDialog(owner) == true)
        {
            await _reportService.ExportExcelAsync(Session, dialog.FileName);
        }
    }

    [RelayCommand]
    private async Task PrintReportAsync()
    {
        if (!string.IsNullOrWhiteSpace(_settingsService.Current.ReportPrinterName))
        {
            await _reportService.PrintAsync(Session, _settingsService.Current.ReportPrinterName);
        }
    }

    public void Dispose() => _timer.Stop();
}
