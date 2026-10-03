using BimBam.Core.Models;
using BimBam.Core.Services;

namespace BimBam.Core.Interfaces;

/// <summary>
/// Shares the open session with other laptops on the local network: this laptop either hosts
/// it or joins another laptop's, keeping working offline and catching up when the connection
/// returns.
/// </summary>
public interface ISessionSyncService
{
    SyncState State { get; }

    event EventHandler? StateChanged;

    /// <summary>Starts sharing the given session from this laptop.</summary>
    Task StartHostingAsync(SessionController controller, CancellationToken ct = default);

    /// <summary>Finds laptops on the local network that are sharing a session.</summary>
    Task<IReadOnlyList<DiscoveredHost>> DiscoverAsync(TimeSpan timeout, CancellationToken ct = default);

    /// <summary>
    /// Joins a host's session. <paramref name="openLocalCopy"/> returns the controller for this
    /// laptop's own copy of that session (creating an empty one the first time).
    /// </summary>
    Task<SessionController> JoinAsync(
        string address,
        int port,
        string pin,
        Func<RemoteSessionInfo, Task<SessionController>> openLocalCopy,
        CancellationToken ct = default);

    /// <summary>Stops hosting or disconnects; the session keeps working locally.</summary>
    Task StopAsync();
}
