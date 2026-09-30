using BimBam.Core.Enums;
using BimBam.Core.Interfaces;
using BimBam.Core.Models;

namespace BimBam.Core.Services;

/// <summary>
/// Default <see cref="IScanEngine"/> implementation: interprets each scanned barcode as a part,
/// a container, or one of the configured command barcodes, applying the repeat-scan window and
/// behavior from <see cref="AppSettings"/>.
/// </summary>
public sealed class ScanEngine : IScanEngine
{
    private readonly OrderSession _session;
    private readonly AppSettings _settings;
    private readonly ISystemClock _clock;

    private DateTimeOffset? _activeLineLastScanAt;

    public ScanEngine(OrderSession session, AppSettings settings, ISystemClock clock)
    {
        _session = session;
        _settings = settings;
        _clock = clock;
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

    public ScanResult SetQuantity(int quantity)
    {
        if (ActiveLine is null)
        {
            return new ScanResult { EventType = ScanEventType.UnknownBarcodeScanned, IsError = true, Message = "Nėra pasirinktos detalės." };
        }

        ActiveLine.ActualQuantity = quantity;
        UpdateStatus(ActiveLine);
        _activeLineLastScanAt = _clock.UtcNow;

        return new ScanResult { EventType = ScanEventType.QuantitySetManually, Line = ActiveLine };
    }

    public void ClearActiveLine()
    {
        ActiveLine = null;
        _activeLineLastScanAt = null;
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
}
