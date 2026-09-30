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
    public static IServiceProvider Services { get; private set; } = null!;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();
        services.AddSingleton<ISystemClock, SystemClock>();
        services.AddSingleton<ISettingsService, JsonSettingsService>();
        services.AddSingleton<ISessionRepository, JsonSessionRepository>();
        services.AddSingleton<IExcelOrderImportService, ExcelOrderImportService>();
        services.AddSingleton<ILabelPrintService, LabelPrintService>();
        services.AddSingleton<IReportService, ReportService>();
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
}
