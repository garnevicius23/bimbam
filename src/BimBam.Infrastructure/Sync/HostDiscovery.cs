using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using BimBam.Core.Models;
using BimBam.Infrastructure.Sync.Models;

namespace BimBam.Infrastructure.Sync;

/// <summary>
/// Finds hosts on the local network: broadcasts a small UDP request on every active network
/// adapter and collects the replies. Only the host needs an inbound firewall rule for this;
/// replies come back to the client's own outgoing socket.
/// </summary>
public static class HostDiscovery
{
    public static async Task<IReadOnlyList<DiscoveredHost>> DiscoverAsync(TimeSpan timeout, CancellationToken ct = default)
    {
        using var udp = new UdpClient(AddressFamily.InterNetwork) { EnableBroadcast = true };
        udp.Client.Bind(new IPEndPoint(IPAddress.Any, 0));
        var request = Encoding.UTF8.GetBytes(SyncProtocol.DiscoveryRequest);

        foreach (var target in BroadcastTargets())
        {
            try
            {
                await udp.SendAsync(request, new IPEndPoint(target, SyncProtocol.DiscoveryPort), ct);
            }
            catch (SocketException)
            {
                // An adapter that can't broadcast (e.g. disconnected) — try the others.
            }
        }

        var found = new Dictionary<string, DiscoveredHost>();
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(timeout);
        try
        {
            while (true)
            {
                var reply = await udp.ReceiveAsync(timeoutCts.Token);
                var announcement = TryParse(reply.Buffer);
                if (announcement is null || !LocalNetworkAddress.IsAllowed(reply.RemoteEndPoint.Address))
                {
                    continue;
                }

                var address = reply.RemoteEndPoint.Address.ToString();
                found[$"{address}:{announcement.Port}"] = new DiscoveredHost
                {
                    Address = address,
                    Port = announcement.Port,
                    HostName = announcement.HostName,
                    SessionId = announcement.SessionId,
                    SessionName = announcement.SessionName
                };
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // Time is up; return what answered.
        }

        return found.Values.OrderBy(h => h.HostName).ToList();
    }

    private static HostAnnouncement? TryParse(byte[] buffer)
    {
        try
        {
            return JsonSerializer.Deserialize<HostAnnouncement>(buffer);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>The generic broadcast address plus each adapter's own subnet broadcast address,
    /// since 255.255.255.255 typically leaves through only one adapter (e.g. not the hotspot's).</summary>
    private static IEnumerable<IPAddress> BroadcastTargets()
    {
        var targets = new HashSet<IPAddress> { IPAddress.Broadcast, IPAddress.Loopback };
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up)
            {
                continue;
            }

            foreach (var unicast in nic.GetIPProperties().UnicastAddresses)
            {
                if (unicast.Address.AddressFamily != AddressFamily.InterNetwork || unicast.IPv4Mask is null)
                {
                    continue;
                }

                var ip = unicast.Address.GetAddressBytes();
                var mask = unicast.IPv4Mask.GetAddressBytes();
                var broadcast = new byte[4];
                for (var i = 0; i < 4; i++)
                {
                    broadcast[i] = (byte)(ip[i] | ~mask[i]);
                }

                targets.Add(new IPAddress(broadcast));
            }
        }

        return targets;
    }
}
