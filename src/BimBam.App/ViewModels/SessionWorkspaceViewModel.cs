using System.Collections.ObjectModel;
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
/// Drives one open order session: the scan input, the live order table, containers, report
/// export/printing and sharing with other laptops. Every change goes through the session's
/// <see cref="SessionController"/>, so it is logged and (when shared) synchronised.
/// </summary>
public sealed partial class SessionWorkspaceViewModel : ObservableObject, IDisposable
{
    private readonly WorkspaceServices _services;
    private readonly IScanEngine _scanEngine;
    private readonly DispatcherTimer _timer;

    public SessionWorkspaceViewModel(SessionController controller, WorkspaceServices services)
    {
        Controller = controller;
        _services = services;
        _scanEngine = new ScanEngine(controller.Session, services.Settings.Current, services.Clock, services.ScanLogger, controller);

        Lines = new ObservableCollection<OrderLine>(Session.Lines);
        Containers = new ObservableCollection<Container>(Session.Containers);

        var commands = services.Settings.Current.CommandBarcodes;
        CommandBarcodeItems =
        [
            new BarcodeItem { Code = commands.Print, Caption = "Spausdinti etiketę" },
            new BarcodeItem { Code = commands.Undo, Caption = "Atšaukti paskutinį veiksmą" },
            new BarcodeItem { Code = commands.NextPart, Caption = "Kita detalė" },
            new BarcodeItem { Code = commands.EnterQuantity, Caption = "Įvesti kiekį" }
        ];

        controller.StateChanged += OnSessionStateChanged;
        services.Sync.StateChanged += OnSyncStateChanged;
        RefreshSyncState();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _timer.Tick += (_, _) => SecondsRemaining = _scanEngine.RemainingWindow is { } remaining ? (int)Math.Ceiling(remaining.TotalSeconds) : null;
        _timer.Start();
    }

    public SessionController Controller { get; }

    public OrderSession Session => Controller.Session;

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

    [ObservableProperty]
    private string quantityInput = string.Empty;

    [ObservableProperty]
    private SyncMode syncMode;

    [ObservableProperty]
    private string syncStatusText = string.Empty;

    [ObservableProperty]
    private string? syncWarning;

    [ObservableProperty]
    private bool isShareBusy;

    public bool IsShared => SyncMode != SyncMode.Local;

    public bool IsOffline => SyncMode == SyncMode.Offline;

    /// <summary>Hosting is possible when not hosting already; for an offline client this is
    /// "take over as host".</summary>
    public bool CanStartHosting => SyncMode is SyncMode.Local or SyncMode.Offline;

    public string StartHostingLabel => SyncMode == SyncMode.Offline ? "Tapti pagrindiniu kompiuteriu" : "Bendrinti sesiją";

    partial void OnSyncModeChanged(SyncMode value)
    {
        OnPropertyChanged(nameof(IsShared));
        OnPropertyChanged(nameof(IsOffline));
        OnPropertyChanged(nameof(CanStartHosting));
        OnPropertyChanged(nameof(StartHostingLabel));
    }

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
            ScanEventType.AssignedToContainer => $"{result.Line!.PartNumber}: {result.Quantity} vnt. priskirta konteineriui {result.Container!.Code} ({ContainerAllocationFormatter.Format(result.Line, Session.Containers)})",
            ScanEventType.ActionUndone => result.Message ?? "Veiksmas atšauktas",
            ScanEventType.UnknownBarcodeScanned => $"Nežinomas kodas: {result.Message}",
            _ => result.Message
        };

        // Labels always print on this laptop's own printer, never on another laptop's.
        if (result.EventType == ScanEventType.LabelPrinted && result.Line is not null)
        {
            _ = _services.LabelPrint.PrintPartLabelAsync(result.Line, _services.Settings.Current.LabelPrinter);
        }

        _ = _services.Repository.AppendScanEventAsync(Session, new ScanEvent
        {
            SessionId = Session.Id,
            OrderLineId = result.Line?.Id,
            ContainerId = result.Container?.Id,
            Type = result.EventType,
            RawBarcode = result.Message,
            QuantityAfter = result.Line?.ActualQuantity
        });
    }

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
    private void ClearReview(OrderLine line) =>
        Controller.Submit(new SessionOperation { Type = SessionOperationType.ReviewCleared, LineId = line.Id });

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

        var merged = new List<string>();
        foreach (var file in dialog.FileNames)
        {
            var imported = await _services.ExcelImport.ReadAsync(file);
            merged.AddRange(Controller.Submit(new SessionOperation { Type = SessionOperationType.ImportFile, ImportFile = imported }).MergedPartNumbers);
        }

        FeedbackIsError = false;
        FeedbackMessage = merged.Count > 0
            ? $"Failai importuoti. Sujungtos detalės: {string.Join(", ", merged.Distinct())}"
            : "Failai importuoti.";
    }

    [RelayCommand]
    private void AddContainer(Window owner)
    {
        // A single combined dialog (code + name together) avoids any gap between two sequential
        // prompts where a stray scanner input could otherwise land on the main scan box.
        var answer = Views.NewContainerWindow.Ask(owner, SuggestContainerCode());
        if (answer is not { } result)
        {
            return;
        }

        if (Session.Containers.Any(c => c.Code.Equals(result.Code, StringComparison.OrdinalIgnoreCase)))
        {
            FeedbackIsError = true;
            FeedbackMessage = $"Konteineris {result.Code} jau yra.";
            return;
        }

        Controller.Submit(new SessionOperation
        {
            Type = SessionOperationType.CreateContainer,
            ContainerId = Guid.NewGuid(),
            ContainerCode = result.Code,
            ContainerName = result.Name
        });

        FeedbackIsError = false;
        FeedbackMessage = $"Konteineris {result.Code} sukurtas. Etiketę spausdinkite rankiniu būdu, kai būsite pasiruošę.";
    }

    /// <summary>
    /// While shared, codes carry this laptop's letter (K-A-001) so laptops creating containers
    /// offline can never collide; a session used on one laptop keeps the plain K-001 format.
    /// </summary>
    private string SuggestContainerCode()
    {
        var prefix = _services.Settings.Current.ContainerBarcodePrefix;
        var stem = IsShared ? $"{prefix}{Controller.Device.DeviceLetter}-" : prefix;
        var used = Session.Containers.Select(c => c.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var number = Session.Containers.Count(c => c.Code.StartsWith(stem, StringComparison.OrdinalIgnoreCase)) + 1;
        while (used.Contains($"{stem}{number:000}"))
        {
            number++;
        }

        return $"{stem}{number:000}";
    }

    [RelayCommand]
    private Task PrintContainerLabelAsync(Container container) =>
        _services.LabelPrint.PrintContainerLabelAsync(container, _services.Settings.Current.LabelPrinter);

    [RelayCommand]
    private void ShowContainerContents(Container container)
    {
        var codes = Session.Containers.ToDictionary(c => c.Id, c => c.Code);
        var rows = Session.Lines
            .Where(l => l.QuantityIn(container.Id) > 0)
            .OrderBy(l => l.PartNumber)
            .Select(l =>
            {
                var others = l.Allocations
                    .Where(a => a.ContainerId != container.Id && a.Quantity > 0)
                    .Select(a => $"{(codes.TryGetValue(a.ContainerId, out var code) ? code : "?")}: {a.Quantity}")
                    .ToList();

                return new ContainerContentRow
                {
                    PartNumber = l.PartNumber,
                    GeeversNumber = l.GeeversNumber,
                    QuantityInContainer = l.QuantityIn(container.Id),
                    ActualQuantity = l.ActualQuantity,
                    ExpectedQuantity = l.ExpectedQuantity,
                    Status = l.Status,
                    OtherContainers = others.Count == 0 ? "-" : string.Join(", ", others)
                };
            })
            .ToList();

        var window = new Views.ContainerContentsWindow(container, rows) { Owner = Application.Current.MainWindow };
        window.Show();
    }

    [RelayCommand]
    private Task PrintControlSheetAsync() =>
        _services.LabelPrint.PrintControlSheetAsync(Session, _services.Settings.Current.CommandBarcodes, _services.Settings.Current.ReportPrinterName);

    [RelayCommand]
    private async Task ExportReportPdfAsync(Window owner)
    {
        var dialog = new SaveFileDialog { Filter = "PDF failai (*.pdf)|*.pdf", FileName = $"{Session.Name}.pdf" };
        if (dialog.ShowDialog(owner) == true)
        {
            await _services.Report.ExportPdfAsync(Session, dialog.FileName);
        }
    }

    [RelayCommand]
    private async Task ExportReportExcelAsync(Window owner)
    {
        var dialog = new SaveFileDialog { Filter = "Excel failai (*.xlsx)|*.xlsx", FileName = $"{Session.Name}.xlsx" };
        if (dialog.ShowDialog(owner) == true)
        {
            await _services.Report.ExportExcelAsync(Session, dialog.FileName);
        }
    }

    [RelayCommand]
    private async Task PrintReportAsync()
    {
        if (!string.IsNullOrWhiteSpace(_services.Settings.Current.ReportPrinterName))
        {
            await _services.Report.PrintAsync(Session, _services.Settings.Current.ReportPrinterName);
        }
    }

    [RelayCommand]
    private async Task StartHostingAsync(Window owner)
    {
        if (!_services.Firewall.IsConfigured())
        {
            var answer = MessageBox.Show(owner,
                "Kad kiti kompiuteriai galėtų prisijungti, šiame kompiuteryje reikia vienkartinio tinklo nustatymo.\n\n" +
                "Windows paprašys administratoriaus teisių. Prieiga bus leidžiama tik iš to paties vietinio tinklo (pvz., telefono interneto taško).\n\n" +
                "Atlikti nustatymą dabar?",
                "Tinklo nustatymas", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            if (answer == MessageBoxResult.Cancel)
            {
                return;
            }

            if (answer == MessageBoxResult.Yes && !await _services.Firewall.ConfigureAsync())
            {
                MessageBox.Show(owner,
                    "Tinklo nustatymas neatliktas. Sesija bus bendrinama, bet kiti kompiuteriai gali negalėti prisijungti.",
                    "Tinklo nustatymas", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        IsShareBusy = true;
        try
        {
            await _services.Sync.StartHostingAsync(Controller);
            FeedbackIsError = false;
            FeedbackMessage = $"Sesija bendrinama. Kitiems kompiuteriams nurodykite PIN: {Session.SharePin}";
        }
        catch (InvalidOperationException ex)
        {
            FeedbackIsError = true;
            FeedbackMessage = ex.Message;
        }
        finally
        {
            IsShareBusy = false;
        }
    }

    [RelayCommand]
    private async Task StopSharingAsync()
    {
        IsShareBusy = true;
        try
        {
            await _services.Sync.StopAsync();
        }
        finally
        {
            IsShareBusy = false;
        }
    }

    private void OnSessionStateChanged(object? sender, EventArgs e) =>
        _ = _services.Dispatcher.InvokeAsync(SyncCollections);

    /// <summary>Brings the bound collections in line with the session after any change,
    /// including lines and containers created on other laptops.</summary>
    private void SyncCollections()
    {
        SyncCollection(Lines, Session.Lines);
        SyncCollection(Containers, Session.Containers);
        RefreshSyncState();
    }

    private static void SyncCollection<T>(ObservableCollection<T> bound, List<T> source)
    {
        var sourceSet = source.ToHashSet();
        for (var i = bound.Count - 1; i >= 0; i--)
        {
            if (!sourceSet.Contains(bound[i]))
            {
                bound.RemoveAt(i);
            }
        }

        var boundSet = bound.ToHashSet();
        foreach (var item in source.Where(item => !boundSet.Contains(item)))
        {
            bound.Add(item);
        }
    }

    private void OnSyncStateChanged(object? sender, EventArgs e) =>
        _ = _services.Dispatcher.InvokeAsync(RefreshSyncState);

    private void RefreshSyncState()
    {
        var state = _services.Sync.State;
        SyncMode = state.Mode;
        SyncWarning = state.Mode == SyncMode.Offline || state.Mode == SyncMode.Hosting ? state.Message : null;
        SyncStatusText = state.Mode switch
        {
            SyncMode.Hosting => state.ConnectedDevices.Count == 0
                ? $"Bendrinama · PIN {state.Pin} · prisijungusių kompiuterių nėra"
                : $"Bendrinama · PIN {state.Pin} · prisijungę: {string.Join(", ", state.ConnectedDevices)}",
            SyncMode.Connected => state.PendingCount == 0
                ? $"Prisijungta prie „{state.HostName}“"
                : $"Prisijungta prie „{state.HostName}“ · siunčiama: {state.PendingCount}",
            SyncMode.Connecting => "Jungiamasi…",
            SyncMode.Offline => $"RYŠYS NUTRŪKO · neišsiųsta pakeitimų: {Controller.UnconfirmedCount}",
            _ => "Sesija nebendrinama"
        };
    }

    public void Dispose()
    {
        _timer.Stop();
        Controller.StateChanged -= OnSessionStateChanged;
        _services.Sync.StateChanged -= OnSyncStateChanged;
    }
}
