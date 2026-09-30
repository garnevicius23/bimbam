using System.Windows;
using System.Windows.Input;

namespace BimBam.App.Views;

/// <summary>Simple reusable prompt for a single line of text (session name, container code, etc.).</summary>
public partial class TextInputWindow : Window
{
    public string? Answer { get; private set; }

    public TextInputWindow(string prompt, string? defaultValue = null)
    {
        InitializeComponent();
        PromptText.Text = prompt;
        InputBox.Text = defaultValue ?? string.Empty;
        Loaded += (_, _) =>
        {
            InputBox.Focus();
            InputBox.SelectAll();
        };
    }

    public static string? Ask(Window owner, string prompt, string? defaultValue = null)
    {
        var dialog = new TextInputWindow(prompt, defaultValue) { Owner = owner };
        return dialog.ShowDialog() == true ? dialog.Answer : null;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        Answer = InputBox.Text.Trim();
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void InputBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Answer = InputBox.Text.Trim();
            DialogResult = true;
        }
    }
}
