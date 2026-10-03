using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using BimBam.Core.Enums;
using BimBam.Core.Models;
using BimBam.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;

namespace BimBam.App.ViewModels;

/// <summary>
/// Top-level view model: the session list, creating/opening sessions, joining another laptop's
/// shared session, and the active workspace.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly WorkspaceServices _services;

    public MainViewModel(WorkspaceServices services)
    {
        _services = services;
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
        var selectedId = SelectedSession?.Id;
        Sessions.Clear();
        foreach (var session in await _services.Repository.ListSessionsAsync())
        {
            Sessions.Add(session);
        }

        SelectedSession = Sessions.FirstOrDefault(s => s.Id == selectedId);
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

        await LeaveSharedSessionAsync();
        var session = await _services.Repository.CreateSessionAsync(name);
        var controller = CreateController(session);
        var merged = new List<string>();
        foreach (var file in dialog.FileNames)
        {
            var imported = await _services.ExcelImport.ReadAsync(file);
            merged.AddRange(controller.Submit(new SessionOperation { Type = SessionOperationType.ImportFile, ImportFile = imported }).MergedPartNumbers);
        }

        await LoadSessionsAsync();
        SelectedSession = Sessions.FirstOrDefault(s => s.Id == session.Id);
        OpenWorkspace(controller);

        StatusMessage = merged.Count > 0
            ? $"Sesija sukurta. Sujungtos detalės (rastos keliuose failuose): {string.Join(", ", merged.Distinct())}"
            : "Sesija sukurta.";
    }

    [RelayCommand]
    private async Task OpenSessionAsync()
    {
        if (SelectedSession is null)
        {
            return;
        }

        await LeaveSharedSessionAsync();
        var session = await _services.Repository.LoadSessionAsync(SelectedSession.Id);
        OpenWorkspace(CreateController(session));
    }

    [RelayCommand]
    private async Task JoinSharedSessionAsync(Window owner)
    {
        var vm = new JoinSessionViewModel(_services.Sync, OpenLocalCopyAsync);
        var window = new Views.JoinSessionWindow { DataContext = vm, Owner = owner };
        if (window.ShowDialog() != true || vm.JoinedController is not { } controller)
        {
            return;
        }

        await LoadSessionsAsync();
        SelectedSession = Sessions.FirstOrDefault(s => s.Id == controller.Session.Id);
        OpenWorkspace(controller);
        StatusMessage = $"Prisijungta prie sesijos „{controller.Session.Name}“.";
    }

    [RelayCommand]
    private void OpenSettings(Window owner)
    {
        var vm = new SettingsViewModel(_services.Settings, _services.Firewall);
        var window = new Views.SettingsWindow { DataContext = vm, Owner = owner };
        window.ShowDialog();
    }

    /// <summary>Finds this laptop's copy of a shared session, or creates an empty one to fill from the host.</summary>
    private async Task<SessionController> OpenLocalCopyAsync(RemoteSessionInfo info)
    {
        if (Workspace?.Controller is { } current && current.Session.Id == info.SessionId)
        {
            return current;
        }

        var session = await _services.Repository.FindSessionAsync(info.SessionId)
            ?? await _services.Repository.CreateSessionAsync(info.SessionName, info.SessionId);
        return CreateController(session);
    }

    private SessionController CreateController(OrderSession session) =>
        new(session, _services.Settings.Current.ToDeviceIdentity(), _services.OperationStore);

    /// <summary>Switching to another session ends any sharing of the current one.</summary>
    private async Task LeaveSharedSessionAsync()
    {
        if (_services.Sync.State.Mode != SyncMode.Local)
        {
            await _services.Sync.StopAsync();
        }
    }

    private void OpenWorkspace(SessionController controller)
    {
        Workspace?.Dispose();
        Workspace = new SessionWorkspaceViewModel(controller, _services);
    }
}
