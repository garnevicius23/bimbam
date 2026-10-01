using BimBam.Core.Enums;
using BimBam.Core.Interfaces;
using BimBam.Core.Models;

namespace BimBam.Core.Services;

/// <summary>
/// Default <see cref="IScanEngine"/> implementation: interprets each scanned barcode as a part,
/// a container, or one of the configured command barcodes, applying the repeat-scan window and
/// behavior from <see cref="AppSettings"/>. Every scan is reported to the optional
/// <see cref="IScanLogger"/> with what was scanned, what it was recognized as, and what actually
/// happened.
/// </summary>
public sealed class ScanEngine : IScanEngine
{
    private readonly OrderSession _session;
    private readonly AppSettings _settings;
    private readonly ISystemClock _clock;
    private readonly IScanLogger? _logger;

    private DateTimeOffset? _activeLineLastScanAt;

    public ScanEngine(OrderSession session, AppSettings settings, ISystemClock clock, IScanLogger? logger = null)
    {
        _session = session;
        _settings = settings;
        _clock = clock;
        _logger = logger;
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

        ActiveLine.ActualQuantity = quantity;
        UpdateStatus(ActiveLine);
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
        line.ActualQuantity = Math.Max(line.ActualQuantity, 1);
        line.LastScannedAt = _clock.UtcNow;
        _activeLineLastScanAt = _clock.UtcNow;
        UpdateStatus(line);

        return new ScanResult { EventType = ScanEventType.PartScanned, Line = line };
    }

    private ScanResult IncrementQuantity(OrderLine line)
    {
        line.ActualQuantity++;
        line.LastScannedAt = _clock.UtcNow;
        _activeLineLastScanAt = _clock.UtcNow;
        UpdateStatus(line);

        return new ScanResult { EventType = ScanEventType.QuantityIncremented, Line = line };
    }

    private ScanResult TriggerPrint()
    {
        if (ActiveLine is null)
        {
            return new ScanResult { EventType = ScanEventType.UnknownBarcodeScanned, IsError = true, Message = "Nėra pasirinktos detalės spausdinimui." };
        }

        ActiveLine.PrintCount++;
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
            ActiveLine.ActualQuantity--;
            UpdateStatus(ActiveLine);
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

        var container = _session.Containers.FirstOrDefault(c => c.Code.Equals(containerCode, StringComparison.OrdinalIgnoreCase));
        if (container is null)
        {
            return ScanResult.UnknownBarcode(containerCode);
        }

        ActiveLine.ContainerId = container.Id;
        _activeLineLastScanAt = _clock.UtcNow;

        return new ScanResult { EventType = ScanEventType.AssignedToContainer, Line = ActiveLine, Container = container };
    }

    private static void UpdateStatus(OrderLine line)
    {
        line.Status = line.ActualQuantity switch
        {
            var q when q == line.ExpectedQuantity => LineStatus.Matches,
            var q when q < line.ExpectedQuantity => LineStatus.Shortage,
            _ => LineStatus.Surplus
        };
    }

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
        ScanEventType.AssignedToContainer => "priskirta konteineriui",
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
