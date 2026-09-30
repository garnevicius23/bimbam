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
    private Guid? containerId;
    private LineStatus status = LineStatus.NotChecked;
    private DateTimeOffset? lastScannedAt;
    private int printCount;

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
        set => SetField(ref actualQuantity, value);
    }

    public decimal UnitPrice { get; set; }

    public decimal ExpectedTotal
    {
        get => expectedTotal;
        set => SetField(ref expectedTotal, value);
    }

    public Guid? ContainerId
    {
        get => containerId;
        set => SetField(ref containerId, value);
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
