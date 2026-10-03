using BimBam.Core.Enums;

namespace BimBam.Core.Models;

/// <summary>
/// A single replayable change to a session. Every laptop keeps the full ordered list of these;
/// the session's state is the result of applying them in order, which is what allows offline
/// work, merging changes from several laptops, and any laptop taking over as host.
/// </summary>
public sealed class SessionOperation
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public Guid SessionId { get; set; }

    public Guid DeviceId { get; set; }

    public string DeviceName { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;

    public SessionOperationType Type { get; init; }

    public Guid? LineId { get; init; }

    public Guid? ContainerId { get; init; }

    /// <summary>Delta for <see cref="SessionOperationType.AdjustQuantity"/>, absolute value for
    /// <see cref="SessionOperationType.SetQuantity"/>, pieces for allocation.</summary>
    public int? Quantity { get; init; }

    /// <summary>For <see cref="SessionOperationType.SetQuantity"/>: the quantity the laptop saw
    /// before overwriting it, used to detect that another laptop changed it in the meantime.</summary>
    public int? BasedOnQuantity { get; init; }

    public string? ContainerCode { get; init; }

    public string? ContainerName { get; init; }

    public ImportedOrderFile? ImportFile { get; init; }

    public List<OrderLine>? BaselineLines { get; init; }

    public List<Container>? BaselineContainers { get; init; }

    public List<ImportedFile>? BaselineImportedFiles { get; init; }
}
