using BimBam.Core.Models;

namespace BimBam.Core.Interfaces;

/// <summary>Receives session changes produced by the scan engine and other UI actions.</summary>
public interface ISessionOperationSink
{
    /// <summary>Records and applies the operation immediately.</summary>
    OperationOutcome Submit(SessionOperation operation);
}
