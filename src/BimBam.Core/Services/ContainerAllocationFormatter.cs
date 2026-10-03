using BimBam.Core.Models;

namespace BimBam.Core.Services;

/// <summary>
/// Formats where a line's pieces were placed, e.g. "K-001: 3, K-002: 2, nepriskirta: 1".
/// </summary>
public static class ContainerAllocationFormatter
{
    public static string Format(OrderLine line, IEnumerable<Container> containers)
    {
        var codes = containers.ToDictionary(c => c.Id, c => c.Code);
        var parts = line.Allocations
            .Where(a => a.Quantity > 0)
            .Select(a => $"{(codes.TryGetValue(a.ContainerId, out var code) ? code : "?")}: {a.Quantity}")
            .ToList();

        if (line.UnallocatedQuantity > 0 && parts.Count > 0)
        {
            parts.Add($"nepriskirta: {line.UnallocatedQuantity}");
        }

        return parts.Count == 0 ? "-" : string.Join(", ", parts);
    }
}
