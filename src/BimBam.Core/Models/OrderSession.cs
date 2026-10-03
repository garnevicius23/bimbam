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

    /// <summary>Full path to this session's snapshot file.</summary>
    public required string DataFilePath { get; set; }

    /// <summary>
    /// PIN other laptops must enter to join this session. Created the first time the session is
    /// shared and kept with the session, so whichever laptop hosts it later uses the same PIN and
    /// already-joined laptops can reconnect without asking the user again.
    /// </summary>
    public string? SharePin { get; set; }

    public List<ImportedFile> ImportedFiles { get; init; } = [];

    public List<OrderLine> Lines { get; init; } = [];

    public List<Container> Containers { get; init; } = [];

    /// <summary>
    /// Replaces lines, containers and imported files with those of <paramref name="source"/>,
    /// reusing existing line objects (matched by ID) so on-screen bindings stay attached.
    /// </summary>
    public void ReplaceStateFrom(OrderSession source)
    {
        var existing = Lines.ToDictionary(l => l.Id);
        var updated = new List<OrderLine>(source.Lines.Count);
        foreach (var sourceLine in source.Lines)
        {
            if (existing.TryGetValue(sourceLine.Id, out var line))
            {
                line.CopyStateFrom(sourceLine);
                updated.Add(line);
            }
            else
            {
                updated.Add(sourceLine.Clone());
            }
        }

        Lines.Clear();
        Lines.AddRange(updated);

        Containers.Clear();
        Containers.AddRange(source.Containers.Select(c => c.Clone()));

        ImportedFiles.Clear();
        ImportedFiles.AddRange(source.ImportedFiles.Select(f => f.Clone()));
    }
}
