using BimBam.Core.Models;

namespace BimBam.Core.Interfaces;

/// <summary>
/// Reads and writes the application-wide settings (printers, storage folder, timing, behavior).
/// </summary>
public interface ISettingsService
{
    AppSettings Current { get; }

    Task LoadAsync(CancellationToken ct = default);

    Task SaveAsync(AppSettings settings, CancellationToken ct = default);
}
