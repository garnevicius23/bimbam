namespace BimBam.Core.Interfaces;

/// <summary>
/// Sink for human-readable scan activity log lines (what was scanned, what action it matched,
/// and what actually happened), so the operator or a developer can watch the scan engine work.
/// </summary>
public interface IScanLogger
{
    void Log(string message);
}
