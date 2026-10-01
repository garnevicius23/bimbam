using BimBam.Core.Interfaces;

namespace BimBam.Infrastructure.Services;

/// <summary>Writes scan activity log lines to the console window, timestamped.</summary>
public sealed class ConsoleScanLogger : IScanLogger
{
    public void Log(string message) => Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}");
}
