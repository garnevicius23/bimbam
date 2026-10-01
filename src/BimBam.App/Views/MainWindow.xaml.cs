using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
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
        // Don't steal keyboard focus from any other window (settings, container contents,
        // print dialogs, etc.) — only ever refocus the scan box while this window is the active one.
        if (!IsActive)
        {
            return;
        }

        if (Keyboard.FocusedElement is TextBox { Name: "ScanInput" })
        {
            return;
        }

        // Only steal focus back when nothing else clearly wants it (e.g. the user isn't typing
        // into another text field), so the scanner keeps working hands-free.
        if (Keyboard.FocusedElement is TextBox)
        {
            return;
        }

        // Let the user select and copy cells in the order grid (e.g. Ctrl+C a part number)
        // without the scan box yanking focus back every second.
        if (Keyboard.FocusedElement is DependencyObject focused && IsDescendantOf(focused, OrderGrid))
        {
            return;
        }

        ScanInput?.Focus();
    }

    private static bool IsDescendantOf(DependencyObject? element, DependencyObject ancestor)
    {
        var current = element;
        while (current is not null)
        {
            if (ReferenceEquals(current, ancestor))
            {
                return true;
            }

            current = VisualTreeHelper.GetParent(current) ?? LogicalTreeHelper.GetParent(current);
        }

        return false;
    }
}
