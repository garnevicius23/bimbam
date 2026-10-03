using BimBam.Core.Enums;
using BimBam.Core.Models;

namespace BimBam.Core.Services;

/// <summary>
/// Applies one <see cref="SessionOperation"/> to a session. This is the only code that changes
/// session state, and it is deterministic: replaying the same operations in the same order
/// always produces the same session on every laptop.
/// </summary>
public static class SessionOperationApplier
{
    public static OperationOutcome Apply(OrderSession session, SessionOperation op) => op.Type switch
    {
        SessionOperationType.LegacyBaseline => ApplyBaseline(session, op),
        SessionOperationType.ImportFile => ApplyImport(session, op),
        SessionOperationType.AdjustQuantity => ApplyAdjust(session, op),
        SessionOperationType.SetQuantity => ApplySet(session, op),
        SessionOperationType.AllocateToContainer => ApplyAllocate(session, op),
        SessionOperationType.CreateContainer => ApplyCreateContainer(session, op),
        SessionOperationType.LabelPrinted => ApplyLabelPrinted(session, op),
        SessionOperationType.ReviewCleared => ApplyReviewCleared(session, op),
        _ => OperationOutcome.None
    };

    /// <summary>Builds a fresh session state by replaying operations from scratch.</summary>
    public static OrderSession Replay(OrderSession template, IEnumerable<SessionOperation> operations)
    {
        var fresh = new OrderSession { Id = template.Id, Name = template.Name, DataFilePath = template.DataFilePath };
        foreach (var op in operations)
        {
            Apply(fresh, op);
        }

        return fresh;
    }

    public static void UpdateStatus(OrderLine line)
    {
        line.Status = line.ActualQuantity switch
        {
            0 when line.ExpectedQuantity > 0 && line.LastScannedAt is null => LineStatus.NotChecked,
            var q when q == line.ExpectedQuantity => LineStatus.Matches,
            var q when q < line.ExpectedQuantity => LineStatus.Shortage,
            _ => LineStatus.Surplus
        };
    }

    private static OperationOutcome ApplyBaseline(OrderSession session, SessionOperation op)
    {
        session.Lines.Clear();
        session.Lines.AddRange((op.BaselineLines ?? []).Select(l => l.Clone()));
        session.Containers.Clear();
        session.Containers.AddRange((op.BaselineContainers ?? []).Select(c => c.Clone()));
        session.ImportedFiles.Clear();
        session.ImportedFiles.AddRange((op.BaselineImportedFiles ?? []).Select(f => f.Clone()));
        return OperationOutcome.None;
    }

    private static OperationOutcome ApplyImport(OrderSession session, SessionOperation op)
    {
        if (op.ImportFile is not { } file)
        {
            return OperationOutcome.None;
        }

        var merged = new List<string>();
        for (var i = 0; i < file.Rows.Count; i++)
        {
            var row = file.Rows[i];
            var source = new OrderLineSource
            {
                Id = DeterministicGuid.Create(op.Id, $"source-{i}"),
                OrderNumber = row.OrderNumber,
                Quantity = row.Quantity
            };

            var existing = session.Lines.FirstOrDefault(l => PartNumberMatcher.AreEqual(l.PartNumber, row.PartNumber));
            if (existing is null)
            {
                var line = new OrderLine
                {
                    Id = DeterministicGuid.Create(session.Id, PartNumberMatcher.Normalize(row.PartNumber)),
                    SessionId = session.Id,
                    PartNumber = row.PartNumber,
                    GeeversNumber = row.GeeversNumber,
                    OrderNumber = row.OrderNumber,
                    ExpectedQuantity = row.Quantity,
                    UnitPrice = row.UnitPrice,
                    ExpectedTotal = row.Total
                };
                line.Sources.Add(new OrderLineSource { Id = source.Id, OrderLineId = line.Id, OrderNumber = source.OrderNumber, Quantity = source.Quantity });
                session.Lines.Add(line);
            }
            else
            {
                existing.ExpectedQuantity += row.Quantity;
                existing.ExpectedTotal += row.Total;
                existing.Sources.Add(new OrderLineSource { Id = source.Id, OrderLineId = existing.Id, OrderNumber = source.OrderNumber, Quantity = source.Quantity });
                if (existing.LastScannedAt is not null)
                {
                    UpdateStatus(existing);
                }

                merged.Add(row.PartNumber);
            }
        }

        session.ImportedFiles.Add(new ImportedFile
        {
            Id = op.Id,
            SessionId = session.Id,
            OrderNumber = file.OrderNumber,
            FileName = file.FileName,
            ImportedAt = op.CreatedAt,
            LineCount = file.Rows.Count
        });

        return new OperationOutcome { MergedPartNumbers = merged };
    }

    private static OperationOutcome ApplyAdjust(OrderSession session, SessionOperation op)
    {
        if (FindLine(session, op) is not { } line)
        {
            return OperationOutcome.None;
        }

        line.ActualQuantity = Math.Max(0, line.ActualQuantity + (op.Quantity ?? 0));
        line.LastScannedAt = op.CreatedAt;
        line.TrimAllocationsToActualQuantity();
        UpdateStatus(line);
        return OperationOutcome.None;
    }

    private static OperationOutcome ApplySet(OrderSession session, SessionOperation op)
    {
        if (FindLine(session, op) is not { } line)
        {
            return OperationOutcome.None;
        }

        // The laptop that typed this quantity saw a different value than this copy has now:
        // another laptop changed the same line in between, so the result needs a human check.
        if (op.BasedOnQuantity is { } seen && seen != line.ActualQuantity)
        {
            line.NeedsReview = true;
        }

        line.ActualQuantity = Math.Max(0, op.Quantity ?? 0);
        line.LastScannedAt = op.CreatedAt;
        line.TrimAllocationsToActualQuantity();
        UpdateStatus(line);
        return OperationOutcome.None;
    }

    private static OperationOutcome ApplyAllocate(OrderSession session, SessionOperation op)
    {
        if (FindLine(session, op) is not { } line || op.ContainerId is not { } containerId)
        {
            return OperationOutcome.None;
        }

        var requested = op.Quantity ?? 0;
        var placed = line.AllocateTo(containerId, requested);
        if (placed < requested)
        {
            line.NeedsReview = true;
        }

        return new OperationOutcome { AllocatedQuantity = placed };
    }

    private static OperationOutcome ApplyCreateContainer(OrderSession session, SessionOperation op)
    {
        if (op.ContainerId is not { } id || session.Containers.Any(c => c.Id == id))
        {
            return OperationOutcome.None;
        }

        session.Containers.Add(new Container
        {
            Id = id,
            SessionId = session.Id,
            Code = op.ContainerCode ?? "?",
            DisplayName = op.ContainerName,
            CreatedAt = op.CreatedAt,
            CreatedBy = op.DeviceName
        });
        return OperationOutcome.None;
    }

    private static OperationOutcome ApplyLabelPrinted(OrderSession session, SessionOperation op)
    {
        if (FindLine(session, op) is { } line)
        {
            line.PrintCount++;
        }

        return OperationOutcome.None;
    }

    private static OperationOutcome ApplyReviewCleared(OrderSession session, SessionOperation op)
    {
        if (FindLine(session, op) is { } line)
        {
            line.NeedsReview = false;
        }

        return OperationOutcome.None;
    }

    private static OrderLine? FindLine(OrderSession session, SessionOperation op) =>
        op.LineId is { } id ? session.Lines.FirstOrDefault(l => l.Id == id) : null;
}
