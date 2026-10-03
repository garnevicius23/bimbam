using BimBam.Core.Models;

namespace BimBam.Core.Interfaces;

/// <summary>Durable storage for a session's operation log and snapshot.</summary>
public interface ISessionOperationStore
{
    IReadOnlyList<SessionOperation> LoadOperations(OrderSession session);

    void AppendOperation(OrderSession session, SessionOperation operation);

    void RewriteOperations(OrderSession session, IReadOnlyList<SessionOperation> operations);

    void SaveSnapshot(OrderSession session);
}
