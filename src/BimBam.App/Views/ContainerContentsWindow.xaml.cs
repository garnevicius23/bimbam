using System.Windows;
using BimBam.App.ViewModels;
using BimBam.Core.Models;

namespace BimBam.App.Views;

/// <summary>Non-modal window listing the parts currently placed in a container, as a table
/// (so it stays legible and open while the user keeps scanning) rather than a popup message.</summary>
public partial class ContainerContentsWindow : Window
{
    public ContainerContentsWindow(Container container, IReadOnlyList<ContainerContentRow> rows)
    {
        InitializeComponent();
        var name = string.IsNullOrWhiteSpace(container.DisplayName) ? container.Code : $"{container.Code} – {container.DisplayName}";
        TitleText.Text = rows.Count == 0
            ? $"Konteineris {name} – tuščias"
            : $"Konteineris {name} – {rows.Count} detalė(s), {rows.Sum(r => r.QuantityInContainer)} vnt.";
        ContentsGrid.ItemsSource = rows;
    }
}
