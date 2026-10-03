using BimBam.Core.Enums;

namespace BimBam.App.ViewModels;

/// <summary>One part's row in a container's contents table.</summary>
public sealed class ContainerContentRow
{
    public required string PartNumber { get; init; }

    public required string GeeversNumber { get; init; }

    public int QuantityInContainer { get; init; }

    public int ActualQuantity { get; init; }

    public int ExpectedQuantity { get; init; }

    public LineStatus Status { get; init; }

    /// <summary>Other containers holding pieces of the same part, e.g. "K-002: 2"; "-" if none.</summary>
    public required string OtherContainers { get; init; }
}
