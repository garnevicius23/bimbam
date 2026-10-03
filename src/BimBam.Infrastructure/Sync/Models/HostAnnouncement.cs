namespace BimBam.Infrastructure.Sync.Models;

/// <summary>Reply to a discovery broadcast, and to the hub's Hello call.</summary>
public sealed class HostAnnouncement
{
    public required string HostName { get; init; }

    public Guid SessionId { get; init; }

    public required string SessionName { get; init; }

    public int Port { get; init; }
}
