using BimBam.Core.Interfaces;

namespace BimBam.App.ViewModels;

/// <summary>The application services a session workspace needs, injected as one unit.</summary>
public sealed class WorkspaceServices(
    ISettingsService settings,
    ISessionRepository repository,
    ISessionOperationStore operationStore,
    IExcelOrderImportService excelImport,
    ILabelPrintService labelPrint,
    IReportService report,
    ISystemClock clock,
    IScanLogger scanLogger,
    ISessionSyncService sync,
    IFirewallSetupService firewall,
    IMainThreadDispatcher dispatcher)
{
    public ISettingsService Settings { get; } = settings;
    public ISessionRepository Repository { get; } = repository;
    public ISessionOperationStore OperationStore { get; } = operationStore;
    public IExcelOrderImportService ExcelImport { get; } = excelImport;
    public ILabelPrintService LabelPrint { get; } = labelPrint;
    public IReportService Report { get; } = report;
    public ISystemClock Clock { get; } = clock;
    public IScanLogger ScanLogger { get; } = scanLogger;
    public ISessionSyncService Sync { get; } = sync;
    public IFirewallSetupService Firewall { get; } = firewall;
    public IMainThreadDispatcher Dispatcher { get; } = dispatcher;
}
