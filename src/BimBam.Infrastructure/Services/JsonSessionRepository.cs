using System.Text.Json;
using BimBam.Core.Interfaces;
using BimBam.Core.Models;
using System.IO;

namespace BimBam.Infrastructure.Services;

/// <summary>
/// Persists each order session as its own folder (named after the session) containing a
/// "sesija.json" snapshot and an append-only "ivykiai.jsonl" scan event log, under the
/// configured sessions folder.
/// </summary>
public sealed class JsonSessionRepository : ISessionRepository
{
    private const string SessionFileName = "sesija.json";
    private const string EventLogFileName = "ivykiai.jsonl";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly ISettingsService _settings;

    public JsonSessionRepository(ISettingsService settings)
    {
        _settings = settings;
    }

    private string RootFolder => _settings.Current.SessionsFolderPath;

    public Task<IReadOnlyList<OrderSession>> ListSessionsAsync(CancellationToken ct = default)
    {
        Directory.CreateDirectory(RootFolder);
        var sessions = new List<OrderSession>();

        foreach (var dir in Directory.EnumerateDirectories(RootFolder))
        {
            var file = Path.Combine(dir, SessionFileName);
            if (!File.Exists(file))
            {
                continue;
            }

            try
            {
                var json = File.ReadAllText(file);
                var session = JsonSerializer.Deserialize<OrderSession>(json, JsonOptions);
                if (session is not null)
                {
                    sessions.Add(session);
                }
            }
            catch (JsonException)
            {
                // Skip a corrupted session file rather than failing the whole list.
            }
        }

        return Task.FromResult<IReadOnlyList<OrderSession>>(sessions.OrderByDescending(s => s.UpdatedAt).ToList());
    }

    public async Task<OrderSession> CreateSessionAsync(string name, CancellationToken ct = default)
    {
        var folderName = BuildFolderName(name);
        var folder = Path.Combine(RootFolder, folderName);
        Directory.CreateDirectory(folder);

        var session = new OrderSession
        {
            Name = name,
            DataFilePath = Path.Combine(folder, SessionFileName)
        };

        await SaveSessionAsync(session, ct);
        return session;
    }

    public async Task<OrderSession> LoadSessionAsync(Guid sessionId, CancellationToken ct = default)
    {
        var sessions = await ListSessionsAsync(ct);
        return sessions.FirstOrDefault(s => s.Id == sessionId)
            ?? throw new FileNotFoundException($"Sesija {sessionId} nerasta.");
    }

    public async Task SaveSessionAsync(OrderSession session, CancellationToken ct = default)
    {
        session.UpdatedAt = DateTimeOffset.Now;
        var folder = Path.GetDirectoryName(session.DataFilePath)!;
        Directory.CreateDirectory(folder);

        var json = JsonSerializer.Serialize(session, JsonOptions);
        await File.WriteAllTextAsync(session.DataFilePath, json, ct);
    }

    public async Task AppendScanEventAsync(ScanEvent scanEvent, CancellationToken ct = default)
    {
        var session = await ListSessionsAsync(ct);
        var match = session.FirstOrDefault(s => s.Id == scanEvent.SessionId);
        if (match is null)
        {
            return;
        }

        var folder = Path.GetDirectoryName(match.DataFilePath)!;
        var logPath = Path.Combine(folder, EventLogFileName);
        var line = JsonSerializer.Serialize(scanEvent);
        await File.AppendAllTextAsync(logPath, line + Environment.NewLine, ct);
    }

    private static string BuildFolderName(string sessionName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(sessionName.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        return $"{DateTime.Now:yyyy-MM-dd_HHmmss}_{sanitized}";
    }
}
