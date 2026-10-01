using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using BimBam.Core.Interfaces;
using BimBam.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;

namespace BimBam.App.ViewModels;

/// <summary>
/// Top-level view model: the session list, creating/opening sessions, and the active workspace.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly ISessionRepository _sessionRepository;
    private readonly IExcelOrderImportService _excelImportService;
    private readonly ISettingsService _settingsService;
    private readonly ILabelPrintService _labelPrintService;
    private readonly IReportService _reportService;
    private readonly ISystemClock _clock;
    private readonly IScanLogger _scanLogger;

    public MainViewModel(
        ISessionRepository sessionRepository,
        IExcelOrderImportService excelImportService,
        ISettingsService settingsService,
        ILabelPrintService labelPrintService,
        IReportService reportService,
        ISystemClock clock,
        IScanLogger scanLogger)
    {
        _sessionRepository = sessionRepository;
        _excelImportService = excelImportService;
        _settingsService = settingsService;
        _labelPrintService = labelPrintService;
        _reportService = reportService;
        _clock = clock;
        _scanLogger = scanLogger;
    }

    public ObservableCollection<OrderSession> Sessions { get; } = [];

    [ObservableProperty]
    private OrderSession? selectedSession;

    [ObservableProperty]
    private SessionWorkspaceViewModel? workspace;

    [ObservableProperty]
    private string? statusMessage;

    public async Task LoadSessionsAsync()
    {
        Sessions.Clear();
        foreach (var session in await _sessionRepository.ListSessionsAsync())
        {
            Sessions.Add(session);
        }
    }

    [RelayCommand]
    private async Task NewSessionAsync(Window owner)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Pasirinkite užsakymo Excel failą(-us)",
            Filter = "Excel failai (*.xlsx)|*.xlsx",
            Multiselect = true
        };

        if (dialog.ShowDialog(owner) != true)
        {
            return;
        }

        var defaultName = Path.GetFileNameWithoutExtension(dialog.FileNames[0]);
        var name = Views.TextInputWindow.Ask(owner, "Sesijos pavadinimas:", defaultName);
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        var session = await _sessionRepository.CreateSessionAsync(name);
        var mergedTotal = new List<string>();
        foreach (var file in dialog.FileNames)
        {
            var merged = await _excelImportService.ImportAsync(session, file);
            mergedTotal.AddRange(merged);
        }

        await _sessionRepository.SaveSessionAsync(session);
        Sessions.Insert(0, session);
        SelectedSession = session;
        OpenWorkspace(session);

        StatusMessage = mergedTotal.Count > 0
            ? $"Sesija sukurta. Sujungtos detalės (rastos keliuose failuose): {string.Join(", ", mergedTotal.Distinct())}"
            : "Sesija sukurta.";
    }

    [RelayCommand]
    private async Task OpenSessionAsync()
    {
        if (SelectedSession is null)
        {
            return;
        }

        var full = await _sessionRepository.LoadSessionAsync(SelectedSession.Id);
        OpenWorkspace(full);
    }

    [RelayCommand]
    private void OpenSettings(Window owner)
    {
        var vm = new SettingsViewModel(_settingsService);
        var window = new Views.SettingsWindow { DataContext = vm, Owner = owner };
        window.ShowDialog();
    }

    private void OpenWorkspace(OrderSession session)
    {
        Workspace = new SessionWorkspaceViewModel(session, _settingsService, _sessionRepository, _excelImportService, _labelPrintService, _reportService, _clock, _scanLogger);
    }
}
