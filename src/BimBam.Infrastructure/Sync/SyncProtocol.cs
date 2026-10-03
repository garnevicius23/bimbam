namespace BimBam.Infrastructure.Sync;

/// <summary>Fixed network settings shared by host and clients.</summary>
public static class SyncProtocol
{
    public const int DefaultTcpPort = 47815;
    public const int DiscoveryPort = 47816;
    public const string HubPath = "/bimbam";
    public const string DiscoveryRequest = "BIMBAM_DISCOVER_V1";
    public const string JoinedGroup = "joined";
    public const string OperationsMethod = "Operations";

    /// <summary>A full operation log (imports included) can be large; SignalR's default limit is 32 KB.</summary>
    public const long MaxMessageBytes = 64L * 1024 * 1024;
}
