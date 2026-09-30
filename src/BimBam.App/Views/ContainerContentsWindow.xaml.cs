using System.Windows;
using BimBam.Core.Models;

namespace BimBam.App.Views;

/// <summary>Non-modal window listing the parts currently assigned to a container, as a table
/// (so it stays legible and open while the user keeps scanning) rather than a popup message.</summary>
public partial class ContainerContentsWindow : Window
{
    public ContainerContentsWindow(Container container, IReadOnlyList<OrderLine> lines)
    {
        InitializeComponent();
        var name = string.IsNullOrWhiteSpace(container.DisplayName) ? container.Code : $"{container.Code} – {container.DisplayName}";
        TitleText.Text = lines.Count == 0
            ? $"Konteineris {name} – tuščias"
            : $"Konteineris {name} – {lines.Count} detalė(s)";
        ContentsGrid.ItemsSource = lines;
    }
}
