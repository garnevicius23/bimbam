namespace BimBam.Core.Models;

/// <summary>A laptop on the local network currently sharing a session.</summary>
public sealed class DiscoveredHost
{
    public required string Address { get; init; }

    public int Port { get; init; }

    public required string HostName { get; init; }

    public Guid SessionId { get; init; }

    public required string SessionName { get; init; }

    public override string ToString() => $"{HostName} – {SessionName} ({Address})";
}
