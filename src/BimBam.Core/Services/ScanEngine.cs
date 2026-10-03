using BimBam.Core.Enums;
using BimBam.Core.Interfaces;
using BimBam.Core.Models;

namespace BimBam.Core.Services;

/// <summary>
/// Default <see cref="IScanEngine"/> implementation: interprets each scanned barcode as a part,
/// a container, or one of the configured command barcodes, applying the repeat-scan window and
/// behavior from <see cref="AppSettings"/>. It never changes session state itself: every change
/// is submitted as a <see cref="SessionOperation"/> so it can be logged and shared with other
/// laptops. The active part and repeat-scan timer are local to this laptop.
/// Every scan is reported to the optional <see cref="IScanLogger"/>.
/// </summary>
public sealed class ScanEngine : IScanEngine
{
    private readonly OrderSession _session;
    private readonly AppSettings _settings;
    private readonly ISystemClock _clock;
    private readonly IScanLogger? _logger;
    private readonly ISessionOperationSink _sink;

    private DateTimeOffset? _activeLineLastScanAt;

    public ScanEngine(OrderSession session, AppSettings settings, ISystemClock clock, IScanLogger? logger = null, ISessionOperationSink? sink = null)
    {
        _session = session;
        _settings = settings;
        _clock = clock;
        _logger = logger;
        _sink = sink ?? new InMemoryOperationSink(session);
    }

    public OrderLine? ActiveLine { get; private set; }

    public TimeSpan? RemainingWindow
    {
        get
        {
            if (ActiveLine is null || _activeLineLastScanAt is null)
            {
                return null;
            }

            var remaining = TimeSpan.FromSeconds(_settings.RepeatScanWindowSeconds) - (_clock.UtcNow - _activeLineLastScanAt.Value);
            return remaining > TimeSpan.Zero ? remaining : null;
        }
    }

    public ScanResult ProcessScan(string rawBarcode)
    {
        var trimmed = rawBarcode.Trim();
        var trigger = DescribeTrigger(trimmed);
        var result = ProcessScanCore(rawBarcode, trimmed);
        LogResult(rawBarcode, trigger, result);
        return result;
    }

    public ScanResult SetQuantity(int quantity)
    {
        if (ActiveLine is null)
        {
            var failure = new ScanResult { EventType = ScanEventType.UnknownBarcodeScanned, IsError = true, Message = "Nėra pasirinktos detalės." };
            LogResult($"(rankinis kiekis: {quantity})", "rankinis kiekio įvedimas", failure);
            return failure;
        }

        Submit(SessionOperationType.SetQuantity, ActiveLine, quantity: quantity, basedOn: ActiveLine.ActualQuantity);
        _activeLineLastScanAt = _clock.UtcNow;

        var result = new ScanResult { EventType = ScanEventType.QuantitySetManually, Line = ActiveLine };
        LogResult($"(rankinis kiekis: {quantity})", "rankinis kiekio įvedimas", result);
        return result;
    }

    public void ClearActiveLine()
    {
        ActiveLine = null;
        _activeLineLastScanAt = null;
    }

    private ScanResult ProcessScanCore(string rawBarcode, string trimmed)
    {
        if (trimmed.Length == 0)
        {
            return ScanResult.UnknownBarcode(rawBarcode);
        }

        var commands = _settings.CommandBarcodes;
        if (trimmed.Equals(commands.Print, StringComparison.OrdinalIgnoreCase))
        {
            return TriggerPrint();
        }

        if (trimmed.Equals(commands.Undo, StringComparison.OrdinalIgnoreCase))
        {
            return Undo();
        }

        if (trimmed.Equals(commands.NextPart, StringComparison.OrdinalIgnoreCase))
        {
            ClearActiveLine();
            return new ScanResult { EventType = ScanEventType.ActionUndone, Message = "Pasirinkite kitą detalę." };
        }

        if (trimmed.StartsWith(_settings.ContainerBarcodePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return AssignContainer(trimmed);
        }

        return ScanPart(trimmed);
    }

    private ScanResult ScanPart(string barcode)
    {
        var line = _session.Lines.FirstOrDefault(l => PartNumberMatcher.AreEqual(l.PartNumber, barcode));
        if (line is null)
        {
            return ScanResult.UnknownBarcode(barcode);
        }

        var isRepeatScan = ActiveLine is not null
            && ActiveLine.Id == line.Id
            && _activeLineLastScanAt is not null
            && _clock.UtcNow - _activeLineLastScanAt.Value <= TimeSpan.FromSeconds(_settings.RepeatScanWindowSeconds);

        if (isRepeatScan)
        {
            return _settings.RepeatScanBehavior switch
            {
                RepeatScanBehavior.TriggerPrint => TriggerPrint(),
                _ => IncrementQuantity(line)
            };
        }

        ActiveLine = line;

        // Coming back to a part whose counted pieces are all already placed means the user is
        // holding another piece of it (typically destined for a different container), so count it.
        var everythingPlaced = line.ActualQuantity > 0 && line.AllocatedQuantity > 0 && line.UnallocatedQuantity == 0;
        var delta = everythingPlaced || line.ActualQuantity == 0 ? 1 : 0;
        if (delta != 0)
        {
            Submit(SessionOperationType.AdjustQuantity, line, quantity: delta);
        }

        _activeLineLastScanAt = _clock.UtcNow;
        return new ScanResult { EventType = ScanEventType.PartScanned, Line = line };
    }

    private ScanResult IncrementQuantity(OrderLine line)
    {
        Submit(SessionOperationType.AdjustQuantity, line, quantity: 1);
        _activeLineLastScanAt = _clock.UtcNow;
        return new ScanResult { EventType = ScanEventType.QuantityIncremented, Line = line };
    }

    private ScanResult TriggerPrint()
    {
        if (ActiveLine is null)
        {
            return new ScanResult { EventType = ScanEventType.UnknownBarcodeScanned, IsError = true, Message = "Nėra pasirinktos detalės spausdinimui." };
        }

        Submit(SessionOperationType.LabelPrinted, ActiveLine);
        _activeLineLastScanAt = _clock.UtcNow;
        return new ScanResult { EventType = ScanEventType.LabelPrinted, Line = ActiveLine };
    }

    private ScanResult Undo()
    {
        if (ActiveLine is null)
        {
            return new ScanResult { EventType = ScanEventType.UnknownBarcodeScanned, IsError = true, Message = "Nėra ką atšaukti." };
        }

        if (ActiveLine.ActualQuantity > 0)
        {
            Submit(SessionOperationType.AdjustQuantity, ActiveLine, quantity: -1);
        }

        _activeLineLastScanAt = _clock.UtcNow;
        return new ScanResult { EventType = ScanEventType.ActionUndone, Line = ActiveLine };
    }

    private ScanResult AssignContainer(string containerCode)
    {
        if (ActiveLine is null)
        {
            return new ScanResult { EventType = ScanEventType.UnknownBarcodeScanned, IsError = true, Message = "Pirmiausia nuskenuokite detalę." };
        }

        var matches = _session.Containers.Where(c => c.Code.Equals(containerCode, StringComparison.OrdinalIgnoreCase)).ToList();
        if (matches.Count == 0)
        {
            return ScanResult.UnknownBarcode(containerCode);
        }

        if (matches.Count > 1)
        {
            return new ScanResult
            {
                EventType = ScanEventType.UnknownBarcodeScanned,
                IsError = true,
                Line = ActiveLine,
                Message = $"Konteinerio kodas {containerCode} sukurtas keliuose kompiuteriuose. Pervadinkite vieną iš jų."
            };
        }

        var container = matches[0];
        var unplaced = ActiveLine.UnallocatedQuantity;
        if (unplaced == 0)
        {
            return new ScanResult
            {
                EventType = ScanEventType.UnknownBarcodeScanned,
                IsError = true,
                Line = ActiveLine,
                Container = container,
                Message = "Visas suskaičiuotas šios detalės kiekis jau priskirtas konteineriams. Nuskenuokite detalę dar kartą."
            };
        }

        var outcome = Submit(SessionOperationType.AllocateToContainer, ActiveLine, quantity: unplaced, containerId: container.Id);
        _activeLineLastScanAt = _clock.UtcNow;

        return new ScanResult { EventType = ScanEventType.AssignedToContainer, Line = ActiveLine, Container = container, Quantity = outcome.AllocatedQuantity };
    }

    private OperationOutcome Submit(SessionOperationType type, OrderLine line, int? quantity = null, int? basedOn = null, Guid? containerId = null) =>
        _sink.Submit(new SessionOperation
        {
            Type = type,
            LineId = line.Id,
            Quantity = quantity,
            BasedOnQuantity = basedOn,
            ContainerId = containerId,
            CreatedAt = _clock.UtcNow
        });


    private string DescribeTrigger(string trimmed)
    {
        if (trimmed.Length == 0)
        {
            return "tuščias kodas";
        }

        var commands = _settings.CommandBarcodes;
        if (trimmed.Equals(commands.Print, StringComparison.OrdinalIgnoreCase))
        {
            return "veiksmo kodas SPAUSDINTI";
        }

        if (trimmed.Equals(commands.Undo, StringComparison.OrdinalIgnoreCase))
        {
            return "veiksmo kodas ATŠAUKTI";
        }

        if (trimmed.Equals(commands.NextPart, StringComparison.OrdinalIgnoreCase))
        {
            return "veiksmo kodas KITA DETALĖ";
        }

        if (trimmed.Equals(commands.EnterQuantity, StringComparison.OrdinalIgnoreCase))
        {
            return "veiksmo kodas ĮVESTI KIEKĮ";
        }

        if (trimmed.StartsWith(_settings.ContainerBarcodePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return "konteinerio kodas";
        }

        return "detalės kodas (PARTNR)";
    }

    private static string DescribeOutcome(ScanResult result) => result.EventType switch
    {
        ScanEventType.PartScanned => "detalė pasirinkta",
        ScanEventType.QuantityIncremented => "kiekis padidintas (+1)",
        ScanEventType.QuantitySetManually => "kiekis nustatytas",
        ScanEventType.LabelPrinted => "spausdinama etiketė",
        ScanEventType.AssignedToContainer => $"priskirta konteineriui ({result.Quantity} vnt.)",
        ScanEventType.ActionUndone => "veiksmas atšauktas / pereita prie kitos detalės",
        ScanEventType.UnknownBarcodeScanned => $"KLAIDA ({result.Message})",
        _ => result.Message ?? "-"
    };

    private void LogResult(string rawInput, string trigger, ScanResult result)
    {
        if (_logger is null)
        {
            return;
        }

        var partNumber = result.Line?.PartNumber ?? "-";
        var containerCode = result.Container?.Code;
        var containerSuffix = containerCode is null ? string.Empty : $" | Konteineris: {containerCode}";

        _logger.Log($"Skenuota: \"{rawInput}\" | Atpažinta kaip: {trigger} | Rezultatas: {DescribeOutcome(result)} | Detalės kodas: {partNumber}{containerSuffix}");
    }
}