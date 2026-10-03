using BimBam.Core.Models;

namespace BimBam.Core.Interfaces;

/// <summary>
/// Persists and loads order sessions, each stored as its own folder under the configured
/// sessions folder.
/// </summary>
public interface ISessionRepository
{
    Task<IReadOnlyList<OrderSession>> ListSessionsAsync(CancellationToken ct = default);

    /// <param name="sessionId">Pass the ID of a session shared by another laptop to create the
    /// local copy under the same ID; omit for a brand new session.</param>
    Task<OrderSession> CreateSessionAsync(string name, Guid? sessionId = null, CancellationToken ct = default);

    Task<OrderSession> LoadSessionAsync(Guid sessionId, CancellationToken ct = default);

    /// <returns>The local copy of the session, or null when this laptop has never had it.</returns>
    Task<OrderSession?> FindSessionAsync(Guid sessionId, CancellationToken ct = default);

    Task SaveSessionAsync(OrderSession session, CancellationToken ct = default);

    Task AppendScanEventAsync(OrderSession session, ScanEvent scanEvent, CancellationToken ct = default);
}
