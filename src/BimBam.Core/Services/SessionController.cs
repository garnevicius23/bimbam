using BimBam.Core.Enums;
using BimBam.Core.Interfaces;
using BimBam.Core.Models;

namespace BimBam.Core.Services;

/// <summary>
/// Owns one open session on this laptop: applies operations to it, keeps and persists the full
/// operation log, and tracks which of this laptop's operations the host has not confirmed yet.
/// Thread-safe; network callbacks and UI actions may both call into it.
/// </summary>
public sealed class SessionController : ISessionOperationSink
{
    private readonly object _gate = new();
    private readonly ISessionOperationStore _store;
    private readonly List<SessionOperation> _log = [];
    private readonly HashSet<Guid> _known = [];
    private readonly HashSet<Guid> _unconfirmed = [];

    public SessionController(OrderSession session, DeviceIdentity device, ISessionOperationStore store)
    {
        Session = session;
        Device = device;
        _store = store;

        var operations = store.LoadOperations(session).ToList();
        if (operations.Count == 0 && (session.Lines.Count > 0 || session.Containers.Count > 0))
        {
            // Session saved before operation logs existed: its current state becomes the
            // starting point everyone replays from.
            foreach (var line in session.Lines)
            {
                line.MigrateLegacyContainer();
            }

            var baseline = new SessionOperation
            {
                Type = SessionOperationType.LegacyBaseline,
                SessionId = session.Id,
                DeviceId = device.DeviceId,
                DeviceName = device.DeviceName,
                BaselineLines = session.Lines.Select(l => l.Clone()).ToList(),
                BaselineContainers = session.Containers.Select(c => c.Clone()).ToList(),
                BaselineImportedFiles = session.ImportedFiles.Select(f => f.Clone()).ToList()
            };
            operations.Add(baseline);
            store.AppendOperation(session, baseline);
        }

        _log.AddRange(operations);
        _known.UnionWith(operations.Select(o => o.Id));
        session.ReplaceStateFrom(SessionOperationApplier.Replay(session, _log));
    }

    public OrderSession Session { get; }

    public DeviceIdentity Device { get; set; }

    /// <summary>When true (this laptop is a client), own operations count as unconfirmed until
    /// the host acknowledges them.</summary>
    public bool TrackConfirmations { get; set; }

    /// <summary>Raised after this laptop created an operation (sync layer sends it on).</summary>
    public event Action<SessionOperation>? LocalOperationSubmitted;

    /// <summary>Raised after any change to the session state, local or remote.</summary>
    public event EventHandler? StateChanged;

    public int UnconfirmedCount
    {
        get
        {
            lock (_gate)
            {
                return _unconfirmed.Count;
            }
        }
    }

    public IReadOnlyList<SessionOperation> Operations
    {
        get
        {
            lock (_gate)
            {
                return _log.ToList();
            }
        }
    }

    public OperationOutcome Submit(SessionOperation operation)
    {
        OperationOutcome outcome;
        lock (_gate)
        {
            operation.SessionId = Session.Id;
            operation.DeviceId = Device.DeviceId;
            operation.DeviceName = Device.DeviceName;

            outcome = SessionOperationApplier.Apply(Session, operation);
            _log.Add(operation);
            _known.Add(operation.Id);
            if (TrackConfirmations)
            {
                _unconfirmed.Add(operation.Id);
            }

            Persist(operation);
        }

        LocalOperationSubmitted?.Invoke(operation);
        StateChanged?.Invoke(this, EventArgs.Empty);
        return outcome;
    }

    /// <summary>Applies an operation received from another laptop (ignored if already known).</summary>
    /// <returns>True when it was new and has been applied.</returns>
    public bool ApplyRemote(SessionOperation operation)
    {
        lock (_gate)
        {
            if (!_known.Add(operation.Id))
            {
                _unconfirmed.Remove(operation.Id);
                return false;
            }

            SessionOperationApplier.Apply(Session, operation);
            _log.Add(operation);
            Persist(operation);
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public void Confirm(IEnumerable<Guid> operationIds)
    {
        lock (_gate)
        {
            _unconfirmed.ExceptWith(operationIds);
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Adopts the host's full operation log after (re)joining. Any operations of this laptop
    /// the host does not have yet are kept on top and returned so they can be sent again.
    /// </summary>
    public IReadOnlyList<SessionOperation> AdoptHostLog(IReadOnlyList<SessionOperation> hostOperations)
    {
        List<SessionOperation> missingOnHost;
        lock (_gate)
        {
            var hostIds = hostOperations.Select(o => o.Id).ToHashSet();
            missingOnHost = _log.Where(o => !hostIds.Contains(o.Id)).ToList();

            _log.Clear();
            _log.AddRange(hostOperations);
            _log.AddRange(missingOnHost);
            _known.Clear();
            _known.UnionWith(_log.Select(o => o.Id));
            _unconfirmed.Clear();
            _unconfirmed.UnionWith(missingOnHost.Select(o => o.Id));

            Session.ReplaceStateFrom(SessionOperationApplier.Replay(Session, _log));
            _store.RewriteOperations(Session, _log);
            _store.SaveSnapshot(Session);
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
        return missingOnHost;
    }

    private void Persist(SessionOperation operation)
    {
        _store.AppendOperation(Session, operation);
        Session.UpdatedAt = DateTimeOffset.Now;
        _store.SaveSnapshot(Session);
    }
}
