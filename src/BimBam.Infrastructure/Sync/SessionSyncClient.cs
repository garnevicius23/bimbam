using BimBam.Core.Enums;
using BimBam.Core.Interfaces;
using BimBam.Core.Models;
using BimBam.Core.Services;
using BimBam.Infrastructure.Sync.Models;
using Microsoft.AspNetCore.SignalR.Client;

namespace BimBam.Infrastructure.Sync;

/// <summary>
/// This laptop's connection to a host. Every local change is applied immediately and sent to
/// the host when connected; while the host is unreachable changes simply accumulate in the
/// local operation log. A background loop keeps looking for the host (by session, so it also
/// finds a different laptop that took over hosting) and, on reconnect, uploads the whole log
/// and adopts the host's merged result.
/// </summary>
public sealed class SessionSyncClient : IAsyncDisposable
{
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(3);

    private readonly IMainThreadDispatcher _dispatcher;
    private readonly Func<DeviceIdentity> _device;
    private readonly Action<string> _saveAssignedLetter;
    private readonly bool _enableDiscovery;
    private readonly CancellationTokenSource _stopping = new();

    private HubConnection? _connection;
    private SessionController? _controller;
    private string _address = string.Empty;
    private int _port;
    private string _pin = string.Empty;
    private int _reconnectLoopRunning;

    public SessionSyncClient(IMainThreadDispatcher dispatcher, Func<DeviceIdentity> device, Action<string> saveAssignedLetter, bool enableDiscovery = true)
    {
        _dispatcher = dispatcher;
        _device = device;
        _saveAssignedLetter = saveAssignedLetter;
        _enableDiscovery = enableDiscovery;
    }

    public event EventHandler? StatusChanged;

    public SyncMode Mode { get; private set; } = SyncMode.Connecting;

    public string? HostName { get; private set; }

    public string? Message { get; private set; }

    public SessionController? Controller => _controller;

    public async Task<SessionController> ConnectAsync(
        string address,
        int port,
        string pin,
        Func<RemoteSessionInfo, Task<SessionController>> openLocalCopy,
        CancellationToken ct = default)
    {
        _address = address;
        _port = port;
        _pin = pin.Trim();

        var connection = await OpenConnectionAsync(address, port, ct);
        try
        {
            var hello = await connection.InvokeAsync<HostAnnouncement>(nameof(SessionHub.Hello), ct);
            _controller = await openLocalCopy(new RemoteSessionInfo
            {
                SessionId = hello.SessionId,
                SessionName = hello.SessionName,
                HostName = hello.HostName
            });
            _controller.LocalOperationSubmitted += SendLocalOperation;

            await JoinAsync(connection, ct);
            Attach(connection, hello.HostName);
            return _controller;
        }
        catch
        {
            if (_controller is not null)
            {
                _controller.LocalOperationSubmitted -= SendLocalOperation;
            }

            await connection.DisposeAsync();
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _stopping.CancelAsync();
        if (_controller is not null)
        {
            _controller.LocalOperationSubmitted -= SendLocalOperation;
        }

        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }

        _stopping.Dispose();
    }

    private async Task<HubConnection> OpenConnectionAsync(string address, int port, CancellationToken ct)
    {
        var host = address.Contains(':') ? $"[{address}]" : address;
        var connection = new HubConnectionBuilder()
            .WithUrl($"http://{host}:{port}{SyncProtocol.HubPath}")
            .Build();

        // Notice a silently dropped Wi-Fi within seconds rather than the default 30.
        connection.KeepAliveInterval = TimeSpan.FromSeconds(3);
        connection.ServerTimeout = TimeSpan.FromSeconds(10);
        connection.On<List<SessionOperation>>(SyncProtocol.OperationsMethod, ApplyRemoteOperationsAsync);

        await connection.StartAsync(ct);
        return connection;
    }

    private async Task JoinAsync(HubConnection connection, CancellationToken ct)
    {
        var controller = _controller!;
        var device = _device();
        var response = await connection.InvokeAsync<JoinResponse>(nameof(SessionHub.Join), new JoinRequest
        {
            Pin = _pin,
            SessionId = controller.Session.Id,
            DeviceId = device.DeviceId,
            DeviceName = device.DeviceName,
            DeviceLetter = device.DeviceLetter,
            Operations = controller.Operations.ToList()
        }, ct);

        if (!response.Accepted)
        {
            throw new InvalidOperationException(response.Error ?? "Prisijungti nepavyko.");
        }

        var missingOnHost = await _dispatcher.InvokeAsync(() =>
        {
            controller.TrackConfirmations = true;
            controller.Session.SharePin = response.Pin;
            controller.Device = new DeviceIdentity
            {
                DeviceId = device.DeviceId,
                DeviceName = device.DeviceName,
                DeviceLetter = response.AssignedLetter
            };
            return controller.AdoptHostLog(response.Operations);
        });

        _pin = response.Pin;
        if (!string.Equals(response.AssignedLetter, device.DeviceLetter, StringComparison.OrdinalIgnoreCase))
        {
            _saveAssignedLetter(response.AssignedLetter);
        }

        if (missingOnHost.Count > 0)
        {
            await SubmitAsync(connection, missingOnHost.ToList());
        }
    }

    private void Attach(HubConnection connection, string hostName)
    {
        _connection = connection;
        connection.Closed += OnConnectionClosed;
        HostName = hostName;
        SetStatus(SyncMode.Connected, null);
    }

    private Task ApplyRemoteOperationsAsync(List<SessionOperation> operations) =>
        _controller is null
            ? Task.CompletedTask
            : _dispatcher.InvokeAsync(() =>
            {
                foreach (var op in operations)
                {
                    _controller.ApplyRemote(op);
                }
            });

    private void SendLocalOperation(SessionOperation operation)
    {
        if (Mode == SyncMode.Connected && _connection is { } connection)
        {
            _ = SubmitAsync(connection, [operation]);
        }
    }

    private async Task SubmitAsync(HubConnection connection, List<SessionOperation> operations)
    {
        try
        {
            var acknowledged = await connection.InvokeAsync<List<Guid>>(nameof(SessionHub.Submit), operations);
            await _dispatcher.InvokeAsync(() => _controller?.Confirm(acknowledged));
            StatusChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception)
        {
            // Stays unconfirmed in the local log; the whole log is uploaded on reconnect.
        }
    }

    private Task OnConnectionClosed(Exception? error)
    {
        if (_stopping.IsCancellationRequested)
        {
            return Task.CompletedTask;
        }

        SetStatus(SyncMode.Offline, "Ryšys su pagrindiniu kompiuteriu nutrūko – duomenys saugomi šiame kompiuteryje.");
        if (Interlocked.Exchange(ref _reconnectLoopRunning, 1) == 0)
        {
            _ = Task.Run(ReconnectLoopAsync);
        }

        return Task.CompletedTask;
    }

    private async Task ReconnectLoopAsync()
    {
        var token = _stopping.Token;
        try
        {
            if (_connection is { } old)
            {
                old.Closed -= OnConnectionClosed;
                _connection = null;
                await old.DisposeAsync();
            }

            while (!token.IsCancellationRequested)
            {
                await Task.Delay(ReconnectDelay, token);
                HubConnection? connection = null;
                try
                {
                    var (address, port) = await LocateHostAsync(token);
                    connection = await OpenConnectionAsync(address, port, token);
                    var hello = await connection.InvokeAsync<HostAnnouncement>(nameof(SessionHub.Hello), token);
                    if (hello.SessionId != _controller!.Session.Id)
                    {
                        await connection.DisposeAsync();
                        continue;
                    }

                    _pin = _controller.Session.SharePin ?? _pin;
                    await JoinAsync(connection, token);
                    _address = address;
                    _port = port;
                    Attach(connection, hello.HostName);
                    return;
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    if (connection is not null)
                    {
                        await connection.DisposeAsync();
                    }

                    return;
                }
                catch (Exception ex)
                {
                    if (connection is not null)
                    {
                        await connection.DisposeAsync();
                    }

                    var reason = ex is InvalidOperationException ? $" ({ex.Message})" : string.Empty;
                    SetStatus(SyncMode.Offline, $"Ryšys su pagrindiniu kompiuteriu nutrūko – duomenys saugomi šiame kompiuteryje. Bandoma prisijungti iš naujo…{reason}");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Stopped by the user.
        }
        finally
        {
            Interlocked.Exchange(ref _reconnectLoopRunning, 0);
        }
    }

    /// <summary>Prefers whoever currently announces this session (the host may have changed
    /// laptop or IP address); falls back to the last known address.</summary>
    private async Task<(string Address, int Port)> LocateHostAsync(CancellationToken ct)
    {
        if (!_enableDiscovery)
        {
            return (_address, _port);
        }

        var hosts = await HostDiscovery.DiscoverAsync(TimeSpan.FromSeconds(1.5), ct);
        var match = hosts.FirstOrDefault(h => h.SessionId == _controller!.Session.Id);
        return match is null ? (_address, _port) : (match.Address, match.Port);
    }

    private void SetStatus(SyncMode mode, string? message)
    {
        Mode = mode;
        Message = message;
        StatusChanged?.Invoke(this, EventArgs.Empty);
    }
}
