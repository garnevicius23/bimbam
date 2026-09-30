namespace BimBam.Core.Interfaces;

/// <summary>Abstraction over the current time, so scan-timing logic can be unit tested.</summary>
public interface ISystemClock
{
    DateTimeOffset UtcNow { get; }
}
