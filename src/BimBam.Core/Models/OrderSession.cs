using BimBam.Core.Enums;

namespace BimBam.Core.Models;

/// <summary>
/// An order review session: one or more imported order files, tracked and reviewed together.
/// </summary>
public sealed class OrderSession
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public required string Name { get; set; }

    public SessionStatus Status { get; set; } = SessionStatus.InProgress;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;

    /// <summary>Full path to this session's own SQLite database file.</summary>
    public required string DataFilePath { get; set; }

    public List<ImportedFile> ImportedFiles { get; init; } = [];

    public List<OrderLine> Lines { get; init; } = [];

    public List<Container> Containers { get; init; } = [];
}
