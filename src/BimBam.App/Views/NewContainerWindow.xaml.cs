using System.Windows;
using System.Windows.Input;

namespace BimBam.App.Views;

/// <summary>
/// Collects both the container code and its optional display name in a single dialog, so there
/// is no gap between two sequential prompts where a stray barcode scan could land on the main
/// scan input while the user is still filling in the container details.
/// </summary>
public partial class NewContainerWindow : Window
{
    public string? ContainerCode { get; private set; }

    public string? ContainerName { get; private set; }

    public NewContainerWindow(string suggestedCode)
    {
        InitializeComponent();
        CodeBox.Text = suggestedCode;
        Loaded += (_, _) =>
        {
            CodeBox.Focus();
            CodeBox.SelectAll();
        };
    }

    /// <summary>Shows the dialog and returns the entered (code, name) pair, or null if cancelled
    /// or no code was entered.</summary>
    public static (string Code, string? Name)? Ask(Window owner, string suggestedCode)
    {
        var dialog = new NewContainerWindow(suggestedCode) { Owner = owner };
        if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.ContainerCode))
        {
            return null;
        }

        return (dialog.ContainerCode!, dialog.ContainerName);
    }

    private void Ok_Click(object sender, RoutedEventArgs e) => Accept();

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void NameBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Accept();
        }
    }

    private void Accept()
    {
        ContainerCode = CodeBox.Text.Trim();
        ContainerName = string.IsNullOrWhiteSpace(NameBox.Text) ? null : NameBox.Text.Trim();
        DialogResult = true;
    }
}
