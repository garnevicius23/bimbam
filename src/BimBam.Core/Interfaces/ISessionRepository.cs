using BimBam.Core.Models;

namespace BimBam.Core.Interfaces;

/// <summary>
/// Persists and loads order sessions, each stored as its own SQLite file under the configured
/// sessions folder.
/// </summary>
public interface ISessionRepository
{
    Task<IReadOnlyList<OrderSession>> ListSessionsAsync(CancellationToken ct = default);

    Task<OrderSession> CreateSessionAsync(string name, CancellationToken ct = default);

    Task<OrderSession> LoadSessionAsync(Guid sessionId, CancellationToken ct = default);

    Task SaveSessionAsync(OrderSession session, CancellationToken ct = default);

    Task AppendScanEventAsync(ScanEvent scanEvent, CancellationToken ct = default);
}
