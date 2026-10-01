using System.Runtime.InteropServices;
using System.IO;
using System.Windows;
using BimBam.App.ViewModels;
using BimBam.App.Views;
using BimBam.Core.Interfaces;
using BimBam.Core.Services;
using BimBam.Infrastructure.Services;
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

        var services = new ServiceCollection();
        services.AddSingleton<ISystemClock, SystemClock>();
        services.AddSingleton<ISettingsService, JsonSettingsService>();
        services.AddSingleton<ISessionRepository, JsonSessionRepository>();
        services.AddSingleton<IExcelOrderImportService, ExcelOrderImportService>();
        services.AddSingleton<ILabelPrintService, LabelPrintService>();
        services.AddSingleton<IReportService, ReportService>();
        services.AddSingleton<IScanLogger, ConsoleScanLogger>();
        services.AddSingleton<MainViewModel>();
        services.AddTransient<SettingsViewModel>();

        Services = services.BuildServiceProvider();

        var settings = Services.GetRequiredService<ISettingsService>();
        await settings.LoadAsync();

        var mainViewModel = Services.GetRequiredService<MainViewModel>();
        await mainViewModel.LoadSessionsAsync();

        var window = new MainWindow { DataContext = mainViewModel };
        window.Show();
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
