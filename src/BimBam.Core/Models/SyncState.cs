using BimBam.Core.Enums;

namespace BimBam.Core.Models;

/// <summary>Snapshot of the sharing status shown in the UI.</summary>
public sealed class SyncState
{
    public static readonly SyncState Local = new() { Mode = SyncMode.Local };

    public SyncMode Mode { get; init; }

    /// <summary>PIN other laptops must enter (shown while hosting).</summary>
    public string? Pin { get; init; }

    /// <summary>Names of laptops currently connected to this host.</summary>
    public IReadOnlyList<string> ConnectedDevices { get; init; } = [];

    /// <summary>Name/address of the host this client is connected to.</summary>
    public string? HostName { get; init; }

    /// <summary>This laptop's changes the host has not confirmed yet.</summary>
    public int PendingCount { get; init; }

    /// <summary>Latest problem worth telling the user about, if any.</summary>
    public string? Message { get; init; }
}
