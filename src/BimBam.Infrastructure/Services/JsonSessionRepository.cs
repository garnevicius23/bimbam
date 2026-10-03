using System.IO;
using System.Text.Json;
using BimBam.Core.Interfaces;
using BimBam.Core.Models;

namespace BimBam.Infrastructure.Services;

/// <summary>
/// Persists each order session as its own folder under the configured sessions folder:
/// "sesija.json" (latest state, used for the session list), "operacijos.jsonl" (the
/// append-only operation log the state is rebuilt from) and "ivykiai.jsonl" (scan audit log).
/// All writes go through one lock so concurrent saves from the UI and the network can't
/// interleave and corrupt a file.
/// </summary>
public sealed class JsonSessionRepository : ISessionRepository, ISessionOperationStore
{
    private const string SessionFileName = "sesija.json";
    private const string OperationLogFileName = "operacijos.jsonl";
    private const string EventLogFileName = "ivykiai.jsonl";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly JsonSerializerOptions LineJsonOptions = new() { WriteIndented = false };

    private readonly object _writeGate = new();
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
                string json;
                lock (_writeGate)
                {
                    json = File.ReadAllText(file);
                }

                var session = JsonSerializer.Deserialize<OrderSession>(json, JsonOptions);
                if (session is not null)
                {
                    // The folder may have been moved or copied since it was saved.
                    session.DataFilePath = file;
                    foreach (var line in session.Lines)
                    {
                        line.MigrateLegacyContainer();
                    }

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

    public Task<OrderSession> CreateSessionAsync(string name, Guid? sessionId = null, CancellationToken ct = default)
    {
        var folder = Path.Combine(RootFolder, BuildFolderName(name));
        Directory.CreateDirectory(folder);

        var session = new OrderSession
        {
            Id = sessionId ?? Guid.NewGuid(),
            Name = name,
            DataFilePath = Path.Combine(folder, SessionFileName)
        };

        SaveSnapshot(session);
        return Task.FromResult(session);
    }

    public async Task<OrderSession> LoadSessionAsync(Guid sessionId, CancellationToken ct = default) =>
        await FindSessionAsync(sessionId, ct) ?? throw new FileNotFoundException($"Sesija {sessionId} nerasta.");

    public async Task<OrderSession?> FindSessionAsync(Guid sessionId, CancellationToken ct = default)
    {
        var sessions = await ListSessionsAsync(ct);
        return sessions.FirstOrDefault(s => s.Id == sessionId);
    }

    public Task SaveSessionAsync(OrderSession session, CancellationToken ct = default)
    {
        session.UpdatedAt = DateTimeOffset.Now;
        SaveSnapshot(session);
        return Task.CompletedTask;
    }

    public Task AppendScanEventAsync(OrderSession session, ScanEvent scanEvent, CancellationToken ct = default)
    {
        var line = JsonSerializer.Serialize(scanEvent, LineJsonOptions);
        lock (_writeGate)
        {
            var folder = Path.GetDirectoryName(session.DataFilePath)!;
            Directory.CreateDirectory(folder);
            File.AppendAllText(Path.Combine(folder, EventLogFileName), line + Environment.NewLine);
        }

        return Task.CompletedTask;
    }

    public IReadOnlyList<SessionOperation> LoadOperations(OrderSession session)
    {
        var path = OperationLogPath(session);
        var operations = new List<SessionOperation>();
        lock (_writeGate)
        {
            if (!File.Exists(path))
            {
                return operations;
            }

            foreach (var text in File.ReadLines(path))
            {
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                try
                {
                    if (JsonSerializer.Deserialize<SessionOperation>(text, LineJsonOptions) is { } op)
                    {
                        operations.Add(op);
                    }
                }
                catch (JsonException)
                {
                    // A line cut short by a crash mid-write: everything before it is still valid.
                }
            }
        }

        return operations;
    }

    public void AppendOperation(OrderSession session, SessionOperation operation)
    {
        var text = JsonSerializer.Serialize(operation, LineJsonOptions);
        lock (_writeGate)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(session.DataFilePath)!);
            File.AppendAllText(OperationLogPath(session), text + Environment.NewLine);
        }
    }

    public void RewriteOperations(OrderSession session, IReadOnlyList<SessionOperation> operations)
    {
        var lines = operations.Select(o => JsonSerializer.Serialize(o, LineJsonOptions));
        lock (_writeGate)
        {
            WriteAtomically(OperationLogPath(session), string.Join(Environment.NewLine, lines) + Environment.NewLine);
        }
    }

    public void SaveSnapshot(OrderSession session)
    {
        var json = JsonSerializer.Serialize(session, JsonOptions);
        lock (_writeGate)
        {
            WriteAtomically(session.DataFilePath, json);
        }
    }

    /// <summary>Writes to a temp file and swaps it in, so a crash never leaves a half-written file.</summary>
    private static void WriteAtomically(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        File.WriteAllText(temp, content);
        File.Move(temp, path, overwrite: true);
    }

    private static string OperationLogPath(OrderSession session) =>
        Path.Combine(Path.GetDirectoryName(session.DataFilePath)!, OperationLogFileName);

    private static string BuildFolderName(string sessionName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(sessionName.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        return $"{DateTime.Now:yyyy-MM-dd_HHmmss}_{sanitized}";
    }
}
