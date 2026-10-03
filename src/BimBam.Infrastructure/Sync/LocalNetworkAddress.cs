using System.Net;
using System.Net.Sockets;

namespace BimBam.Infrastructure.Sync;

/// <summary>
/// Decides whether a remote address belongs to a local/private network. The host refuses
/// everything else, as a second line of defence behind the firewall rule.
/// </summary>
public static class LocalNetworkAddress
{
    public static bool IsAllowed(IPAddress? address)
    {
        if (address is null)
        {
            return false;
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (IPAddress.IsLoopback(address))
        {
            return true;
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            // Link-local (fe80::/10) and unique-local (fc00::/7) only.
            var first = address.GetAddressBytes()[0];
            return address.IsIPv6LinkLocal || (first & 0xFE) == 0xFC;
        }

        var b = address.GetAddressBytes();
        return b[0] == 10
            || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
            || (b[0] == 192 && b[1] == 168)
            || (b[0] == 169 && b[1] == 254);
    }
}
