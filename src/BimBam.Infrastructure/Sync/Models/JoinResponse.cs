using BimBam.Core.Models;

namespace BimBam.Infrastructure.Sync.Models;

/// <summary>Host's answer to a join: either a refusal reason or the full session log.</summary>
public sealed class JoinResponse
{
    public bool Accepted { get; init; }

    /// <summary>Reason shown to the user when refused (Lithuanian).</summary>
    public string? Error { get; init; }

    public Guid SessionId { get; init; }

    public string SessionName { get; init; } = string.Empty;

    public string Pin { get; init; } = string.Empty;

    /// <summary>Container-code letter the client should use (may differ from the one it asked for).</summary>
    public string AssignedLetter { get; init; } = string.Empty;

    public List<SessionOperation> Operations { get; init; } = [];
}
