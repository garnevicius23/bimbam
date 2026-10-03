namespace BimBam.Core.Models;

/// <summary>How many pieces of one order line were placed into one container.</summary>
public sealed class ContainerAllocation
{
    public Guid ContainerId { get; init; }

    public int Quantity { get; set; }
}
