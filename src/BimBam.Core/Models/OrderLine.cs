using System.Collections.ObjectModel;
using System.ComponentModel;
using BimBam.Core.Enums;

namespace BimBam.Core.Models;

/// <summary>
/// A single car part line within an order session, matched against the uploaded Excel order(s).
/// Raises <see cref="PropertyChanged"/> so the WPF order grid updates immediately as scans and
/// merges happen, without requiring the row to scroll back into view.
/// </summary>
public sealed class OrderLine : INotifyPropertyChanged
{
    private int expectedQuantity;
    private int actualQuantity;
    private decimal expectedTotal;
    private LineStatus status = LineStatus.NotChecked;
    private DateTimeOffset? lastScannedAt;
    private int printCount;
    private bool needsReview;

    public OrderLine()
    {
        Sources.CollectionChanged += (_, _) => OnPropertyChanged(nameof(IsMerged));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public Guid Id { get; init; } = Guid.NewGuid();

    public Guid SessionId { get; init; }

    /// <summary>Part number as printed on the physical part's barcode (matched ignoring spaces/case).</summary>
    public required string PartNumber { get; set; }

    public required string GeeversNumber { get; set; }

    /// <summary>Order number this line belongs to. For merged lines, see <see cref="Sources"/>.</summary>
    public required string OrderNumber { get; set; }

    public int ExpectedQuantity
    {
        get => expectedQuantity;
        set => SetField(ref expectedQuantity, value);
    }

    public int ActualQuantity
    {
        get => actualQuantity;
        set
        {
            SetField(ref actualQuantity, value);
            OnPropertyChanged(nameof(UnallocatedQuantity));
        }
    }

    public decimal UnitPrice { get; set; }

    /// <summary>
    /// Set when changes from different laptops collided on this line (e.g. one laptop entered a
    /// quantity manually while another was counting it), so a person should double-check it.
    /// </summary>
    public bool NeedsReview
    {
        get => needsReview;
        set => SetField(ref needsReview, value);
    }

    public decimal ExpectedTotal
    {
        get => expectedTotal;
        set => SetField(ref expectedTotal, value);
    }

    /// <summary>
    /// Pieces of this part placed per container. The same part can be split across several
    /// containers; anything counted but not yet placed is <see cref="UnallocatedQuantity"/>.
    /// </summary>
    public List<ContainerAllocation> Allocations { get; init; } = [];

    /// <summary>
    /// Only read when loading sessions saved before per-container quantities existed, when a line
    /// could belong to a single container. Migrated into <see cref="Allocations"/> on load.
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("ContainerId")]
    public Guid? LegacyContainerId { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public int AllocatedQuantity => Allocations.Sum(a => a.Quantity);

    [System.Text.Json.Serialization.JsonIgnore]
    public int UnallocatedQuantity => Math.Max(0, ActualQuantity - AllocatedQuantity);

    public int QuantityIn(Guid containerId) =>
        Allocations.Where(a => a.ContainerId == containerId).Sum(a => a.Quantity);

    /// <summary>Places all not-yet-placed pieces into the given container.</summary>
    /// <returns>The number of pieces placed (0 when everything counted is already placed).</returns>
    public int AllocateUnallocatedTo(Guid containerId) => AllocateTo(containerId, UnallocatedQuantity);

    /// <summary>Places up to <paramref name="quantity"/> not-yet-placed pieces into the container.</summary>
    /// <returns>The number of pieces actually placed (never more than are unplaced).</returns>
    public int AllocateTo(Guid containerId, int quantity)
    {
        quantity = Math.Min(quantity, UnallocatedQuantity);
        if (quantity <= 0)
        {
            return 0;
        }

        var existing = Allocations.FirstOrDefault(a => a.ContainerId == containerId);
        if (existing is null)
        {
            Allocations.Add(new ContainerAllocation { ContainerId = containerId, Quantity = quantity });
        }
        else
        {
            existing.Quantity += quantity;
        }

        RaiseAllocationsChanged();
        return quantity;
    }

    /// <summary>
    /// Overwrites this line's state with another copy's, keeping this object (and so every UI
    /// binding to it) alive. Used when a laptop resynchronises with the host.
    /// </summary>
    public void CopyStateFrom(OrderLine other)
    {
        PartNumber = other.PartNumber;
        GeeversNumber = other.GeeversNumber;
        OrderNumber = other.OrderNumber;
        UnitPrice = other.UnitPrice;
        ExpectedQuantity = other.ExpectedQuantity;
        ActualQuantity = other.ActualQuantity;
        ExpectedTotal = other.ExpectedTotal;
        Status = other.Status;
        LastScannedAt = other.LastScannedAt;
        PrintCount = other.PrintCount;
        NeedsReview = other.NeedsReview;

        Allocations.Clear();
        Allocations.AddRange(other.Allocations.Select(a => new ContainerAllocation { ContainerId = a.ContainerId, Quantity = a.Quantity }));
        RaiseAllocationsChanged();

        Sources.Clear();
        foreach (var source in other.Sources)
        {
            Sources.Add(new OrderLineSource { Id = source.Id, OrderLineId = Id, OrderNumber = source.OrderNumber, Quantity = source.Quantity });
        }
    }

    public OrderLine Clone()
    {
        var copy = new OrderLine
        {
            Id = Id,
            SessionId = SessionId,
            PartNumber = PartNumber,
            GeeversNumber = GeeversNumber,
            OrderNumber = OrderNumber
        };
        copy.CopyStateFrom(this);
        return copy;
    }

    /// <summary>
    /// Shrinks placements (most recent container first) so they never exceed the counted quantity,
    /// e.g. after an undo or a lower manually entered quantity.
    /// </summary>
    public void TrimAllocationsToActualQuantity()
    {
        var excess = AllocatedQuantity - ActualQuantity;
        if (excess <= 0)
        {
            return;
        }

        for (var i = Allocations.Count - 1; i >= 0 && excess > 0; i--)
        {
            var take = Math.Min(excess, Allocations[i].Quantity);
            Allocations[i].Quantity -= take;
            excess -= take;
            if (Allocations[i].Quantity == 0)
            {
                Allocations.RemoveAt(i);
            }
        }

        RaiseAllocationsChanged();
    }

    public void MigrateLegacyContainer()
    {
        if (LegacyContainerId is { } legacy && Allocations.Count == 0 && ActualQuantity > 0)
        {
            Allocations.Add(new ContainerAllocation { ContainerId = legacy, Quantity = ActualQuantity });
        }

        LegacyContainerId = null;
    }

    private void RaiseAllocationsChanged()
    {
        OnPropertyChanged(nameof(Allocations));
        OnPropertyChanged(nameof(AllocatedQuantity));
        OnPropertyChanged(nameof(UnallocatedQuantity));
    }

    public LineStatus Status
    {
        get => status;
        set => SetField(ref status, value);
    }

    /// <summary>True when this line's expected quantity was merged from more than one imported file.</summary>
    public bool IsMerged => Sources.Count > 1;

    public ObservableCollection<OrderLineSource> Sources { get; init; } = [];

    public DateTimeOffset? LastScannedAt
    {
        get => lastScannedAt;
        set => SetField(ref lastScannedAt, value);
    }

    public int PrintCount
    {
        get => printCount;
        set => SetField(ref printCount, value);
    }

    private void SetField<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        OnPropertyChanged(propertyName);
    }

    private void OnPropertyChanged(string? propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
