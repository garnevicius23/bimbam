namespace BimBam.Infrastructure.Sync.Models;

/// <summary>A laptop currently joined to this host.</summary>
public sealed class ConnectedClient
{
    public required string ConnectionId { get; init; }

    public Guid DeviceId { get; init; }

    public required string DeviceName { get; init; }

    public required string DeviceLetter { get; init; }
}
