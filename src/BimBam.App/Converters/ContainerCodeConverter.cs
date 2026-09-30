using System.Globalization;
using System.Windows.Data;
using BimBam.Core.Models;

namespace BimBam.App.Converters;

/// <summary>Resolves a line's assigned container ID to its display code for the order grid.</summary>
public sealed class ContainerCodeConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values is not [Guid id, IEnumerable<Container> containers])
        {
            return "-";
        }

        var match = containers.FirstOrDefault(c => c.Id == id);
        return match?.Code ?? "-";
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
