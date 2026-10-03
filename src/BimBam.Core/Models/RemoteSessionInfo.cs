namespace BimBam.Core.Models;

/// <summary>Identifies the session a host is sharing, before joining it.</summary>
public sealed class RemoteSessionInfo
{
    public Guid SessionId { get; init; }

    public required string SessionName { get; init; }

    public required string HostName { get; init; }
}
