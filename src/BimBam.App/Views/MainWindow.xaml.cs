using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using BimBam.App.ViewModels;

namespace BimBam.App.Views;

/// <summary>
/// Main application window: session list on the left, the active session's scan/containers/report
/// tabs on the right. Keeps the scan input focused so a USB barcode scanner (keyboard emulation)
/// always types into it without the user having to click the window.
/// </summary>
public partial class MainWindow : Window
{
    private readonly DispatcherTimer _refocusTimer;

    public MainWindow()
    {
        InitializeComponent();

        _refocusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _refocusTimer.Tick += (_, _) => RefocusScanInputIfIdle();
        _refocusTimer.Start();
    }

    private void ScanInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        if (DataContext is MainViewModel { Workspace: { } workspace })
        {
            workspace.SubmitScan();
            RevealActiveLine(workspace);
        }
    }

    private void QuantityInputBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        if (DataContext is MainViewModel { Workspace: { } workspace } && workspace.ApplyQuantityCommand.CanExecute(null))
        {
            workspace.ApplyQuantityCommand.Execute(null);
            RevealActiveLine(workspace);
        }
    }

    private void RevealActiveLine(SessionWorkspaceViewModel workspace)
    {
        if (workspace.ActiveLine is null)
        {
            return;
        }

        OrderGrid.SelectedItem = workspace.ActiveLine;
        OrderGrid.ScrollIntoView(workspace.ActiveLine);
    }

    private void RefocusScanInputIfIdle()
    {
        if (Keyboard.FocusedElement is TextBox { Name: "ScanInput" })
        {
            return;
        }

        // Only steal focus back when nothing else clearly wants it (e.g. no dialog is open and
        // the user isn't typing into another text field), so the scanner keeps working hands-free.
        if (Keyboard.FocusedElement is TextBox)
        {
            return;
        }

        ScanInput?.Focus();
    }
}
