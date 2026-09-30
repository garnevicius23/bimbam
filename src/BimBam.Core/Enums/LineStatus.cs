namespace BimBam.Core.Enums;

/// <summary>
/// Review status of a single order line, derived from expected vs. actual quantity.
/// </summary>
public enum LineStatus
{
    NotChecked,
    Matches,
    Shortage,
    Surplus
}
