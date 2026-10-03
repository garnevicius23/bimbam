using System.Net.Sockets;

namespace BimBam.Infrastructure.Sync;

/// <summary>
/// On Windows, when a UDP packet reaches a port nobody listens on, the reply makes the *next*
/// receive on the sending socket fail with "connection forcibly closed" (WSAECONNRESET), even
/// though UDP has no connection. Discovery deliberately sends to places where nobody may be
/// listening, so that behaviour is switched off on its sockets.
/// </summary>
internal static class UdpSocketOptions
{
    private const int SioUdpConnReset = unchecked((int)0x9800000C);

    public static void IgnoreConnectionResets(UdpClient client)
    {
        if (OperatingSystem.IsWindows())
        {
            client.Client.IOControl(SioUdpConnReset, [0, 0, 0, 0], null);
        }
    }
}
