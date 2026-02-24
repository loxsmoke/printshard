using System.Collections.Generic;

namespace PrintShard.Models;

public sealed class AppSettings
{
    // Print appearance
    public bool ShowTileLabel         { get; set; } = true;
    public CropMarkStyle CropMarkStyle { get; set; } = CropMarkStyle.Corners;

    // Overlay colors stored as #AARRGGBB hex strings
    public string OverlapShadingColor { get; set; } = "#66FFFF00";  // 40% yellow
    public string PageBorderColor     { get; set; } = "#FFFF0000";  // red

    // Layout defaults
    public double DefaultOverlapMm  { get; set; } = 10;
    public double DefaultMarginMm   { get; set; } = 5;
    public int    DefaultPagesWide  { get; set; } = 2;
    public int    DefaultPagesTall  { get; set; } = 2;
    public PageOrientation DefaultOrientation { get; set; } = PageOrientation.Portrait;
    public PrintOrder      DefaultPrintOrder  { get; set; } = PrintOrder.LeftToRightThenDown;

    // Recent files
    public int MaxRecentFiles { get; set; } = 10;
    public List<string> RecentFiles { get; set; } = [];

    // View toggles (persisted)
    public bool ShowPageBorders    { get; set; } = true;
}
