using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using BimBam.Core.Interfaces;
using BimBam.Core.Models;
using BimBam.Core.Services;
using BimBam.Infrastructure.Sync.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BimBam.Infrastructure.Sync;

/// <summary>
/// Hosts one session for other laptops on the local network: an embedded web server with a
/// SignalR hub, plus a UDP responder so clients can find it. The host's own session controller
/// is the single authority — it receives every laptop's operations, applies them once, and
/// relays them to everyone else.
/// </summary>
public sealed class SessionHostServer : IAsyncDisposable
{
    private readonly SessionController _controller;
    private readonly DeviceIdentity _hostDevice;
    private readonly IMainThreadDispatcher _dispatcher;
    private readonly int _requestedPort;
    private readonly bool _enableDiscovery;
    private readonly IPAddress? _bindAddress;
    private readonly ConcurrentDictionary<string, ConnectedClient> _clients = new();
    private readonly CancellationTokenSource _stopping = new();

    private WebApplication? _app;
    private IHubContext<SessionHub>? _hub;
    private UdpClient? _discoveryListener;

    public SessionHostServer(
        SessionController controller,
        DeviceIdentity hostDevice,
        IMainThreadDispatcher dispatcher,
        int port = SyncProtocol.DefaultTcpPort,
        bool enableDiscovery = true,
        IPAddress? bindAddress = null)
    {
        _controller = controller;
        _hostDevice = hostDevice;
        _dispatcher = dispatcher;
        _requestedPort = port;
        _enableDiscovery = enableDiscovery;
        _bindAddress = bindAddress;
    }

    /// <summary>Raised whenever a laptop joins or leaves.</summary>
    public event EventHandler? ClientsChanged;

    /// <summary>Actual TCP port (useful when started with port 0).</summary>
    public int Port { get; private set; }

    public IReadOnlyList<string> ConnectedDeviceNames =>
        _clients.Values.Select(c => $"{c.DeviceName} ({c.DeviceLetter})").OrderBy(n => n).ToList();

    public HostAnnouncement Announcement => new()
    {
        HostName = _hostDevice.DeviceName,
        SessionId = _controller.Session.Id,
        SessionName = _controller.Session.Name,
        Port = Port
    };

    public async Task StartAsync(CancellationToken ct = default)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(SessionHostServer).Assembly.GetName().Name,
            ContentRootPath = AppContext.BaseDirectory
        });
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(o =>
        {
            // Null = all adapters (normal hosting); a specific address (e.g. loopback) is for tests.
            if (_bindAddress is null)
            {
                o.ListenAnyIP(_requestedPort);
            }
            else
            {
                o.Listen(_bindAddress, _requestedPort);
            }
        });
        builder.Services.AddSingleton(this);
        builder.Services.AddSignalR(o =>
        {
            o.MaximumReceiveMessageSize = SyncProtocol.MaxMessageBytes;

            // Must stay well below the clients' 10 s server timeout so idle clients don't drop.
            o.KeepAliveInterval = TimeSpan.FromSeconds(3);
            o.ClientTimeoutInterval = TimeSpan.FromSeconds(10);
        });

        var app = builder.Build();
        app.Use(async (context, next) =>
        {
            if (!LocalNetworkAddress.IsAllowed(context.Connection.RemoteIpAddress))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }

            await next();
        });
        app.MapHub<SessionHub>(SyncProtocol.HubPath);

        await app.StartAsync(ct);
        _app = app;
        _hub = app.Services.GetRequiredService<IHubContext<SessionHub>>();
        Port = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses
            .Select(a => new Uri(a).Port)
            .First();

        _controller.LocalOperationSubmitted += BroadcastLocalOperation;

        if (_enableDiscovery)
        {
            _discoveryListener = new UdpClient(new IPEndPoint(IPAddress.Any, SyncProtocol.DiscoveryPort));
            _ = Task.Run(() => AnswerDiscoveryAsync(_discoveryListener, _stopping.Token));
        }
    }

    public async Task<JoinResponse> JoinAsync(string connectionId, JoinRequest request)
    {
        var session = _controller.Session;
        if (!string.Equals(request.Pin?.Trim(), session.SharePin, StringComparison.Ordinal))
        {
            return new JoinResponse { Accepted = false, Error = "Neteisingas PIN kodas." };
        }

        if (request.SessionId != Guid.Empty && request.SessionId != session.Id)
        {
            return new JoinResponse { Accepted = false, Error = "Šis kompiuteris bendrina kitą sesiją." };
        }

        var letter = AssignLetter(request.DeviceId, request.DeviceLetter);
        _clients[connectionId] = new ConnectedClient
        {
            ConnectionId = connectionId,
            DeviceId = request.DeviceId,
            DeviceName = request.DeviceName,
            DeviceLetter = letter
        };

        var (newOperations, log) = await _dispatcher.InvokeAsync(() =>
        {
            var applied = request.Operations.Where(_controller.ApplyRemote).ToList();
            return (applied, _controller.Operations.ToList());
        });

        await BroadcastAsync(newOperations, exceptConnectionId: connectionId);
        ClientsChanged?.Invoke(this, EventArgs.Empty);

        return new JoinResponse
        {
            Accepted = true,
            SessionId = session.Id,
            SessionName = session.Name,
            Pin = session.SharePin!,
            AssignedLetter = letter,
            Operations = log
        };
    }

    public async Task<List<Guid>> SubmitAsync(string connectionId, List<SessionOperation> operations)
    {
        if (!_clients.ContainsKey(connectionId))
        {
            throw new HubException("Pirmiausia prisijunkite prie sesijos.");
        }

        var newOperations = await _dispatcher.InvokeAsync(() => operations.Where(_controller.ApplyRemote).ToList());
        await BroadcastAsync(newOperations, exceptConnectionId: connectionId);
        return operations.Select(o => o.Id).ToList();
    }

    public void RemoveClient(string connectionId)
    {
        if (_clients.TryRemove(connectionId, out _))
        {
            ClientsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public async ValueTask DisposeAsync()
    {
        _controller.LocalOperationSubmitted -= BroadcastLocalOperation;
        await _stopping.CancelAsync();
        _discoveryListener?.Dispose();
        if (_app is not null)
        {
            using var stopTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await _app.StopAsync(stopTimeout.Token);
            await _app.DisposeAsync();
        }

        _clients.Clear();
        _stopping.Dispose();
    }

    private string AssignLetter(Guid deviceId, string requested)
    {
        var taken = _clients.Values.Where(c => c.DeviceId != deviceId).Select(c => c.DeviceLetter)
            .Append(_hostDevice.DeviceLetter)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var wanted = string.IsNullOrWhiteSpace(requested) ? "A" : requested.Trim().ToUpperInvariant();
        if (!taken.Contains(wanted))
        {
            return wanted;
        }

        return Enumerable.Range('A', 26).Select(c => ((char)c).ToString()).First(l => !taken.Contains(l));
    }

    private void BroadcastLocalOperation(SessionOperation operation) =>
        _ = BroadcastAsync([operation], exceptConnectionId: null);

    private async Task BroadcastAsync(List<SessionOperation> operations, string? exceptConnectionId)
    {
        if (_hub is null || operations.Count == 0)
        {
            return;
        }

        var clients = exceptConnectionId is null
            ? _hub.Clients.Group(SyncProtocol.JoinedGroup)
            : _hub.Clients.GroupExcept(SyncProtocol.JoinedGroup, exceptConnectionId);
        try
        {
            await clients.SendAsync(SyncProtocol.OperationsMethod, operations);
        }
        catch (Exception)
        {
            // A client that missed this catches up from the full log when it rejoins.
        }
    }

    private async Task AnswerDiscoveryAsync(UdpClient listener, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var request = await listener.ReceiveAsync(ct);
                if (!LocalNetworkAddress.IsAllowed(request.RemoteEndPoint.Address)
                    || Encoding.UTF8.GetString(request.Buffer) != SyncProtocol.DiscoveryRequest)
                {
                    continue;
                }

                var reply = JsonSerializer.SerializeToUtf8Bytes(Announcement);
                await listener.SendAsync(reply, request.RemoteEndPoint, ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (SocketException)
            {
                // Ignore a bad packet or transient adapter change and keep listening.
            }
        }
    }
}
