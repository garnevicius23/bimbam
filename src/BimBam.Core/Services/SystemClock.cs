using BimBam.Core.Interfaces;

namespace BimBam.Core.Services;

/// <summary>Default <see cref="ISystemClock"/> backed by the real system clock.</summary>
public sealed class SystemClock : ISystemClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
