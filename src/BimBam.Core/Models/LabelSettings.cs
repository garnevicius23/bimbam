namespace BimBam.Core.Models;

/// <summary>
/// Label layout and printer settings for the Brother QL-800 (62 mm continuous roll).
/// </summary>
public sealed class LabelSettings
{
    public string? PrinterName { get; set; }

    public double LabelWidthMm { get; set; } = 62;

    public double LabelHeightMm { get; set; } = 29;

    public int BarcodeHeightMm { get; set; } = 14;

    public int PartNumberFontSize { get; set; } = 18;

    /// <summary>
    /// Many continuous-roll thermal label drivers (e.g. Brother QL-800) model the roll's media
    /// size with the feed direction and tape width swapped relative to how the label is designed
    /// on screen, which makes labels print sideways/not fit unless the print job is rotated to
    /// compensate. Defaults to true (landscape) since that matches the QL-800's continuous-roll
    /// driver; flip it in Nustatymai if your printer prints labels rotated the wrong way.
    /// </summary>
    public bool LandscapeOrientation { get; set; } = true;
}
