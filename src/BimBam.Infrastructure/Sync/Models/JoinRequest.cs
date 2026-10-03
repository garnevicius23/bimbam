using BimBam.Core.Models;

namespace BimBam.Infrastructure.Sync.Models;

/// <summary>Sent by a client to join the hosted session, carrying its whole operation log so
/// anything it did offline (or while it was the host) reaches the new host.</summary>
public sealed class JoinRequest
{
    public required string Pin { get; init; }

    /// <summary>Session the client already has a copy of; empty on its first join.</summary>
    public Guid SessionId { get; init; }

    public Guid DeviceId { get; init; }

    public required string DeviceName { get; init; }

    public required string DeviceLetter { get; init; }

    public List<SessionOperation> Operations { get; init; } = [];
}
