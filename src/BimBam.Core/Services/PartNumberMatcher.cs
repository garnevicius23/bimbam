namespace BimBam.Core.Services;

/// <summary>
/// Normalizes part numbers for matching, ignoring spacing and letter case differences such as
/// "D  004660M4" vs "d004660m4".
/// </summary>
public static class PartNumberMatcher
{
    public static string Normalize(string partNumber) =>
        new string(partNumber.Where(c => !char.IsWhiteSpace(c)).ToArray()).ToUpperInvariant();

    public static bool AreEqual(string left, string right) =>
        Normalize(left) == Normalize(right);
}
