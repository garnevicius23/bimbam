using System.Text.Json;
using BimBam.Core.Interfaces;
using BimBam.Core.Models;
using System.IO;

namespace BimBam.Infrastructure.Services;

/// <summary>
/// Stores application settings as a single JSON file under the user's AppData folder.
/// </summary>
public sealed class JsonSettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _settingsFilePath;

    public JsonSettingsService()
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BimBam");
        Directory.CreateDirectory(folder);
        _settingsFilePath = Path.Combine(folder, "settings.json");
        Current = new AppSettings
        {
            SessionsFolderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "BimBam", "Sesijos")
        };
    }

    public AppSettings Current { get; private set; }

    public async Task LoadAsync(CancellationToken ct = default)
    {
        if (!File.Exists(_settingsFilePath))
        {
            Directory.CreateDirectory(Current.SessionsFolderPath);
            await SaveAsync(Current, ct);
            return;
        }

        await using var stream = File.OpenRead(_settingsFilePath);
        var loaded = await JsonSerializer.DeserializeAsync<AppSettings>(stream, JsonOptions, ct);
        Current = loaded ?? Current;
        Directory.CreateDirectory(Current.SessionsFolderPath);
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken ct = default)
    {
        Current = settings;
        Directory.CreateDirectory(Current.SessionsFolderPath);
        await using var stream = File.Create(_settingsFilePath);
        await JsonSerializer.SerializeAsync(stream, settings, JsonOptions, ct);
    }
}
