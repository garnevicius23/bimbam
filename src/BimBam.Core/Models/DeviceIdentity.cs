namespace BimBam.Core.Models;

/// <summary>Who this laptop is when sharing a session with others.</summary>
public sealed class DeviceIdentity
{
    public Guid DeviceId { get; init; }

    public required string DeviceName { get; init; }

    /// <summary>Letter embedded in container codes this laptop creates (e.g. K-A-001), so two
    /// laptops creating containers while offline can never produce the same code.</summary>
    public required string DeviceLetter { get; init; }
}
