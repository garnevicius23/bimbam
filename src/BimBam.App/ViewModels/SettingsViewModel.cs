using System.Printing;
using System.Windows;
using BimBam.Core.Enums;
using BimBam.Core.Interfaces;
using BimBam.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;

namespace BimBam.App.ViewModels;

/// <summary>
/// Edits the application-wide settings: printers, storage folder, scan timing and behavior,
/// and command/container barcode prefixes.
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;

    public SettingsViewModel(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        var current = settingsService.Current;

        SessionsFolderPath = current.SessionsFolderPath;
        LabelPrinterName = current.LabelPrinter.PrinterName;
        LabelWidthMm = current.LabelPrinter.LabelWidthMm;
        LabelHeightMm = current.LabelPrinter.LabelHeightMm;
        BarcodeHeightMm = current.LabelPrinter.BarcodeHeightMm;
        PartNumberFontSize = current.LabelPrinter.PartNumberFontSize;
        ReportPrinterName = current.ReportPrinterName;
        RepeatScanWindowSeconds = current.RepeatScanWindowSeconds;
        RepeatScanBehaviorIsPrint = current.RepeatScanBehavior == RepeatScanBehavior.TriggerPrint;
        ContainerBarcodePrefix = current.ContainerBarcodePrefix;
        SoundsEnabled = current.SoundsEnabled;
        PrintCommand = current.CommandBarcodes.Print;
        UndoCommand = current.CommandBarcodes.Undo;
        NextPartCommand = current.CommandBarcodes.NextPart;
        EnterQuantityCommand = current.CommandBarcodes.EnterQuantity;

        using var server = new LocalPrintServer();
        AvailablePrinters = server.GetPrintQueues().Select(q => q.FullName).ToList();
    }

    public List<string> AvailablePrinters { get; }

    [ObservableProperty] private string sessionsFolderPath = string.Empty;
    [ObservableProperty] private string? labelPrinterName;
    [ObservableProperty] private double labelWidthMm;
    [ObservableProperty] private double labelHeightMm;
    [ObservableProperty] private int barcodeHeightMm;
    [ObservableProperty] private int partNumberFontSize;
    [ObservableProperty] private string? reportPrinterName;
    [ObservableProperty] private int repeatScanWindowSeconds;
    [ObservableProperty] private bool repeatScanBehaviorIsPrint;
    [ObservableProperty] private string containerBarcodePrefix = "K-";
    [ObservableProperty] private bool soundsEnabled = true;
    [ObservableProperty] private string printCommand = "*SPAUSDINTI*";
    [ObservableProperty] private string undoCommand = "*ATSAUKTI*";
    [ObservableProperty] private string nextPartCommand = "*KITAS*";
    [ObservableProperty] private string enterQuantityCommand = "*KIEKIS*";

    [RelayCommand]
    private void BrowseFolder(Window owner)
    {
        var dialog = new OpenFolderDialog { Title = "Pasirinkite sesijų duomenų aplanką" };
        if (dialog.ShowDialog(owner) == true)
        {
            SessionsFolderPath = dialog.FolderName;
        }
    }

    [RelayCommand]
    private async Task SaveAsync(Window owner)
    {
        var settings = new AppSettings
        {
            SessionsFolderPath = SessionsFolderPath,
            ReportPrinterName = ReportPrinterName,
            RepeatScanWindowSeconds = RepeatScanWindowSeconds,
            RepeatScanBehavior = RepeatScanBehaviorIsPrint ? RepeatScanBehavior.TriggerPrint : RepeatScanBehavior.IncrementQuantity,
            ContainerBarcodePrefix = ContainerBarcodePrefix,
            SoundsEnabled = SoundsEnabled,
            LabelPrinter = new LabelSettings
            {
                PrinterName = LabelPrinterName,
                LabelWidthMm = LabelWidthMm,
                LabelHeightMm = LabelHeightMm,
                BarcodeHeightMm = BarcodeHeightMm,
                PartNumberFontSize = PartNumberFontSize
            },
            CommandBarcodes = new CommandBarcodes
            {
                Print = PrintCommand,
                Undo = UndoCommand,
                NextPart = NextPartCommand,
                EnterQuantity = EnterQuantityCommand
            }
        };

        await _settingsService.SaveAsync(settings);
        owner.Close();
    }
}
