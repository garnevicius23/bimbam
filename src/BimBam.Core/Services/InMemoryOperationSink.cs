using BimBam.Core.Interfaces;
using BimBam.Core.Models;

namespace BimBam.Core.Services;

/// <summary>Applies operations straight to a session with no log or persistence.</summary>
public sealed class InMemoryOperationSink(OrderSession session) : ISessionOperationSink
{
    public OperationOutcome Submit(SessionOperation operation)
    {
        operation.SessionId = session.Id;
        return SessionOperationApplier.Apply(session, operation);
    }
}
