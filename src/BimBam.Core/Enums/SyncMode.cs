namespace BimBam.Core.Enums;

/// <summary>How this laptop currently takes part in sharing the open session.</summary>
public enum SyncMode
{
    /// <summary>Not shared; works exactly like a single-laptop app.</summary>
    Local,
    Hosting,
    Connecting,
    Connected,

    /// <summary>Was connected, lost the host; keeps working and storing changes locally.</summary>
    Offline
}
