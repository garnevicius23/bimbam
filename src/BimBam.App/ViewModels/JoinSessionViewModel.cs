using System.Collections.ObjectModel;
using System.Windows;
using BimBam.Core.Interfaces;
using BimBam.Core.Models;
using BimBam.Core.Services;
using BimBam.Infrastructure.Sync;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BimBam.App.ViewModels;

/// <summary>Finds laptops sharing a session on the local network and joins one with its PIN.</summary>
public sealed partial class JoinSessionViewModel : ObservableObject
{
    private readonly ISessionSyncService _sync;
    private readonly Func<RemoteSessionInfo, Task<SessionController>> _openLocalCopy;

    public JoinSessionViewModel(ISessionSyncService sync, Func<RemoteSessionInfo, Task<SessionController>> openLocalCopy)
    {
        _sync = sync;
        _openLocalCopy = openLocalCopy;
    }

    public ObservableCollection<DiscoveredHost> Hosts { get; } = [];

    public SessionController? JoinedController { get; private set; }

    [ObservableProperty]
    private DiscoveredHost? selectedHost;

    [ObservableProperty]
    private string manualAddress = string.Empty;

    [ObservableProperty]
    private string pin = string.Empty;

    [ObservableProperty]
    private string? status;

    [ObservableProperty]
    private bool isBusy;

    partial void OnSelectedHostChanged(DiscoveredHost? value)
    {
        if (value is not null)
        {
            ManualAddress = string.Empty;
        }
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        IsBusy = true;
        Status = "Ieškoma kompiuterių tinkle…";
        try
        {
            var found = await _sync.DiscoverAsync(TimeSpan.FromSeconds(2));
            Hosts.Clear();
            foreach (var host in found)
            {
                Hosts.Add(host);
            }

            SelectedHost ??= Hosts.FirstOrDefault();
            Status = Hosts.Count == 0
                ? "Nerasta. Patikrinkite, ar kitas kompiuteris bendrina sesiją ir ar abu prijungti prie to paties tinklo. Galite įvesti adresą rankiniu būdu."
                : $"Rasta: {Hosts.Count}.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task JoinAsync(Window window)
    {
        var (address, port) = SelectedHost is { } host && string.IsNullOrWhiteSpace(ManualAddress)
            ? (host.Address, host.Port)
            : (ManualAddress.Trim(), SyncProtocol.DefaultTcpPort);

        if (string.IsNullOrWhiteSpace(address))
        {
            Status = "Pasirinkite kompiuterį sąraše arba įveskite jo adresą.";
            return;
        }

        if (string.IsNullOrWhiteSpace(Pin))
        {
            Status = "Įveskite PIN kodą, rodomą bendrinančiame kompiuteryje.";
            return;
        }

        IsBusy = true;
        Status = "Jungiamasi…";
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            JoinedController = await _sync.JoinAsync(address, port, Pin, _openLocalCopy, timeout.Token);
            window.DialogResult = true;
        }
        catch (InvalidOperationException ex)
        {
            Status = ex.Message;
        }
        catch (Exception)
        {
            Status = $"Nepavyko prisijungti prie {address}. Patikrinkite adresą ir ar abu kompiuteriai tame pačiame tinkle.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
