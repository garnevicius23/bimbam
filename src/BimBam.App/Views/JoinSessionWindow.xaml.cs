using System.Windows;
using BimBam.App.ViewModels;

namespace BimBam.App.Views;

/// <summary>Dialog for joining another laptop's shared session; searches the network on open.</summary>
public partial class JoinSessionWindow : Window
{
    public JoinSessionWindow()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            PinBox.Focus();
            if (DataContext is JoinSessionViewModel vm)
            {
                await vm.SearchCommand.ExecuteAsync(null);
            }
        };
    }
}
