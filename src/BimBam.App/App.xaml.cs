using System.Runtime.InteropServices;
using System.IO;
using System.Windows;
using BimBam.App.Services;
using BimBam.App.ViewModels;
using BimBam.App.Views;
using BimBam.Core.Interfaces;
using BimBam.Core.Services;
using BimBam.Infrastructure.Services;
using BimBam.Infrastructure.Sync;
using Microsoft.Extensions.DependencyInjection;

namespace BimBam.App;

/// <summary>Application entry point: wires up dependency injection and shows the main window.</summary>
public partial class App : Application
{
    private const int AttachParentProcess = -1;

    public static IServiceProvider Services { get; private set; } = null!;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AllocConsole();

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        EnsureConsoleWindow();
        RegisterCrashGuards();

        var services = new ServiceCollection();
        services.AddSingleton<ISystemClock, SystemClock>();
        services.AddSingleton<ISettingsService, JsonSettingsService>();
        services.AddSingleton<JsonSessionRepository>();
        services.AddSingleton<ISessionRepository>(sp => sp.GetRequiredService<JsonSessionRepository>());
        services.AddSingleton<ISessionOperationStore>(sp => sp.GetRequiredService<JsonSessionRepository>());
        services.AddSingleton<IExcelOrderImportService, ExcelOrderImportService>();
        services.AddSingleton<ILabelPrintService, LabelPrintService>();
        services.AddSingleton<IReportService, ReportService>();
        services.AddSingleton<IScanLogger, ConsoleScanLogger>();
        services.AddSingleton<IMainThreadDispatcher, WpfMainThreadDispatcher>();
        services.AddSingleton<ISessionSyncService, SessionSyncService>();
        services.AddSingleton<IFirewallSetupService, FirewallSetupService>();
        services.AddSingleton<WorkspaceServices>();
        services.AddSingleton<MainViewModel>();

        Services = services.BuildServiceProvider();

        var settings = Services.GetRequiredService<ISettingsService>();
        await settings.LoadAsync();

        var mainViewModel = Services.GetRequiredService<MainViewModel>();
        await mainViewModel.LoadSessionsAsync();

        var window = new MainWindow { DataContext = mainViewModel };
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Close network connections cleanly so other laptops notice right away.
        if (Services?.GetService<ISessionSyncService>() is { } sync)
        {
            sync.StopAsync().Wait(TimeSpan.FromSeconds(3));
        }

        base.OnExit(e);
    }

    /// <summary>
    /// Last-resort safety net: an unexpected error (typically a network hiccup) is logged and
    /// reported instead of closing the app mid-scan. Session data is saved after every change,
    /// so nothing is lost either way.
    /// </summary>
    private void RegisterCrashGuards()
    {
        DispatcherUnhandledException += (_, args) =>
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] NETIKĖTA KLAIDA: {args.Exception}");
            MessageBox.Show(
                $"Įvyko netikėta klaida, bet programa tęsia darbą. Duomenys išsaugoti.\n\n{args.Exception.Message}",
                "BimBam", MessageBoxButton.OK, MessageBoxImage.Warning);
            args.Handled = true;
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Foninė klaida: {args.Exception}");
            args.SetObserved();
        };
    }

    /// <summary>
    /// This is a WPF (GUI-subsystem) app, so it has no console by default. If it was launched
    /// from a terminal (e.g. "dotnet run"), attach to that terminal so scan log lines show up
    /// there; otherwise (double-clicked from Explorer) open a dedicated console window for them.
    /// </summary>
    private static void EnsureConsoleWindow()
    {
        if (!AttachConsole(AttachParentProcess))
        {
            AllocConsole();
        }

        var stdOut = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };
        Console.SetOut(stdOut);
        Console.WriteLine("BimBam paleista. Čia bus rodomas skenavimo žurnalas (kas nuskenuota, kaip atpažinta, kas įvyko).");
    }
}
