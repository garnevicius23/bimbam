using System.Security.Cryptography;
using BimBam.Core.Enums;
using BimBam.Core.Interfaces;
using BimBam.Core.Models;
using BimBam.Core.Services;

namespace BimBam.Infrastructure.Sync;

/// <summary>
/// Default <see cref="ISessionSyncService"/>: this laptop is at most one of host or client at a
/// time, and switching (e.g. a client taking over as host) stops the previous role first.
/// </summary>
public sealed class SessionSyncService : ISessionSyncService
{
    private readonly IMainThreadDispatcher _dispatcher;
    private readonly ISettingsService _settings;
    private readonly ISessionRepository _repository;

    private SessionHostServer? _host;
    private SessionSyncClient? _client;
    private SessionController? _controller;
    private string? _hostError;

    public SessionSyncService(IMainThreadDispatcher dispatcher, ISettingsService settings, ISessionRepository repository)
    {
        _dispatcher = dispatcher;
        _settings = settings;
        _repository = repository;
    }

    public event EventHandler? StateChanged;

    /// <summary>Port the host listens on; overridable for tests.</summary>
    public int HostPort { get; init; } = SyncProtocol.DefaultTcpPort;

    public bool EnableDiscovery { get; init; } = true;

    /// <summary>Adapter the host listens on; null = all (normal). Loopback in tests.</summary>
    public System.Net.IPAddress? HostBindAddress { get; init; }

    public SyncState State
    {
        get
        {
            if (_host is not null)
            {
                return new SyncState
                {
                    Mode = SyncMode.Hosting,
                    Pin = _controller?.Session.SharePin,
                    ConnectedDevices = _host.ConnectedDeviceNames,
                    Message = _hostError
                };
            }

            if (_client is not null)
            {
                return new SyncState
                {
                    Mode = _client.Mode,
                    HostName = _client.HostName,
                    PendingCount = _controller?.UnconfirmedCount ?? 0,
                    Message = _client.Message
                };
            }

            return SyncState.Local;
        }
    }

    public async Task StartHostingAsync(SessionController controller, CancellationToken ct = default)
    {
        await StopAsync();

        if (EnableDiscovery)
        {
            var existing = (await HostDiscovery.DiscoverAsync(TimeSpan.FromSeconds(1.2), ct))
                .FirstOrDefault(h => h.SessionId == controller.Session.Id);
            if (existing is not null)
            {
                throw new InvalidOperationException($"Ši sesija jau bendrinama kompiuteryje „{existing.HostName}“. Prisijunkite prie jo.");
            }
        }

        if (string.IsNullOrWhiteSpace(controller.Session.SharePin))
        {
            controller.Session.SharePin = RandomNumberGenerator.GetInt32(1000, 10000).ToString();
            await _repository.SaveSessionAsync(controller.Session, ct);
        }

        controller.TrackConfirmations = false;
        controller.Device = _settings.Current.ToDeviceIdentity();

        var host = new SessionHostServer(controller, controller.Device, _dispatcher, HostPort, EnableDiscovery, HostBindAddress);
        try
        {
            await host.StartAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await host.DisposeAsync();
            throw new InvalidOperationException("Nepavyko pradėti bendrinti sesijos. Galbūt BimBam jau bendrina sesiją šiame kompiuteryje.", ex);
        }

        _host = host;
        _controller = controller;
        _host.ClientsChanged += OnChanged;
        OnChanged(this, EventArgs.Empty);
    }

    public Task<IReadOnlyList<DiscoveredHost>> DiscoverAsync(TimeSpan timeout, CancellationToken ct = default) =>
        HostDiscovery.DiscoverAsync(timeout, ct);

    public async Task<SessionController> JoinAsync(
        string address,
        int port,
        string pin,
        Func<RemoteSessionInfo, Task<SessionController>> openLocalCopy,
        CancellationToken ct = default)
    {
        await StopAsync();

        var client = new SessionSyncClient(_dispatcher, () => _settings.Current.ToDeviceIdentity(), SaveAssignedLetter, EnableDiscovery);
        try
        {
            var controller = await client.ConnectAsync(address, port, pin, openLocalCopy, ct);
            _client = client;
            _controller = controller;
            client.StatusChanged += OnChanged;
            controller.StateChanged += OnChanged;
            OnChanged(this, EventArgs.Empty);
            return controller;
        }
        catch
        {
            await client.DisposeAsync();
            throw;
        }
    }

    public async Task StopAsync()
    {
        if (_controller is not null)
        {
            _controller.StateChanged -= OnChanged;
        }

        if (_host is not null)
        {
            _host.ClientsChanged -= OnChanged;
            await _host.DisposeAsync();
            _host = null;
        }

        if (_client is not null)
        {
            _client.StatusChanged -= OnChanged;
            await _client.DisposeAsync();
            _client = null;
        }

        _controller = null;
        _hostError = null;
        OnChanged(this, EventArgs.Empty);
    }

    private void SaveAssignedLetter(string letter)
    {
        _settings.Current.DeviceLetter = letter;
        _ = _settings.SaveAsync(_settings.Current);
    }

    private void OnChanged(object? sender, EventArgs e) => StateChanged?.Invoke(this, EventArgs.Empty);
}
