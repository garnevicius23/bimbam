using System.Globalization;
using System.Windows.Data;
using BimBam.Core.Models;
using BimBam.Core.Services;

namespace BimBam.App.Converters;

/// <summary>
/// Shows how many pieces of a line went into which container, e.g. "K-001: 3, K-002: 2".
/// Bound to the line itself plus its Allocations/ActualQuantity so it re-renders on every change.
/// </summary>
public sealed class ContainerCodeConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values is not [OrderLine line, IEnumerable<Container> containers, ..])
        {
            return "-";
        }

        return ContainerAllocationFormatter.Format(line, containers);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
