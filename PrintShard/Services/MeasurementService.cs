using System.Globalization;

namespace PrintShard.Services;

/// <summary>
/// Provides measurement unit detection and conversion based on system settings.
/// </summary>
public static class MeasurementService
{
    /// <summary>
    /// Returns true if the system uses metric (mm), false if imperial (inches).
    /// </summary>
    public static bool IsMetric
    {
        get
        {
            var region = RegionInfo.CurrentRegion;
            return region.IsMetric;
        }
    }

    /// <summary>
    /// Gets the unit suffix for display ("mm" or "in").
    /// </summary>
    public static string UnitSuffix => IsMetric ? "mm" : "in";

    /// <summary>
    /// Converts millimeters to the display unit (mm or inches).
    /// </summary>
    public static double MmToDisplayUnit(double mm)
    {
        return IsMetric ? mm : mm / 25.4;
    }

    /// <summary>
    /// Converts display units (mm or inches) to millimeters.
    /// </summary>
    public static double DisplayUnitToMm(double value)
    {
        return IsMetric ? value : value * 25.4;
    }

    /// <summary>
    /// Formats a dimension in millimeters for display with the appropriate unit.
    /// </summary>
    public static string FormatDimension(double mm, string format = "F1")
    {
        double value = MmToDisplayUnit(mm);
        return $"{value.ToString(format)} {UnitSuffix}";
    }

    /// <summary>
    /// Formats paper dimensions (width × height) for display.
    /// </summary>
    public static string FormatPaperSize(double widthMm, double heightMm)
    {
        double w = MmToDisplayUnit(widthMm);
        double h = MmToDisplayUnit(heightMm);
        string format = IsMetric ? "F0" : "F1";
        return $"{w.ToString(format)} × {h.ToString(format)} {UnitSuffix}";
    }
}
