namespace PrintShard.Models;

/// <summary>
/// Immutable snapshot of a computed tile layout.
/// Call <see cref="Compute"/> to build a new instance from the current parameters.
/// </summary>
public sealed class TileLayout
{
    // ── Inputs ──────────────────────────────────────────────────────────────
    public int PagesWide { get; init; }
    public int PagesTall { get; init; }
    public PageOrientation Orientation { get; init; }
    public double OverlapMm { get; init; }
    public double MarginMm { get; init; }
    public PrintOrder PrintOrder { get; init; }
    public double PaperWidthMm { get; init; }
    public double PaperHeightMm { get; init; }

    // ── Derived (filled by Compute) ──────────────────────────────────────────
    /// <summary>Actual printable width in mm (paper minus unprintable margins).</summary>
    public double PrintableWidthMm { get; private set; }
    /// <summary>Actual printable height in mm (paper minus unprintable margins).</summary>
    public double PrintableHeightMm { get; private set; }
    public double TotalWidthMm { get; private set; }
    public double TotalHeightMm { get; private set; }
    /// <summary>Millimetres per source-image pixel.</summary>
    public double MmPerPx { get; private set; }
    /// <summary>Effective print DPI based on the scale applied.</summary>
    public double EffectiveDpi { get; private set; }

    public IReadOnlyList<TileInfo> Tiles { get; private set; } = [];

    // ── Factory ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Compute tile geometry for an image of <paramref name="imageWidthPx"/> ×
    /// <paramref name="imageHeightPx"/> pixels and return a populated layout.
    /// </summary>
    /// <param name="printableWidthMm">Actual printable width (paper width minus unprintable margins).</param>
    /// <param name="printableHeightMm">Actual printable height (paper height minus unprintable margins).</param>
    public static TileLayout Compute(
        int imageWidthPx, int imageHeightPx,
        int pagesWide, int pagesTall,
        PageOrientation orientation,
        double overlapMm, double marginMm,
        PrintOrder printOrder,
        double paperWidthMm, double paperHeightMm,
        double printableWidthMm, double printableHeightMm)
    {
        var layout = new TileLayout
        {
            PagesWide = pagesWide,
            PagesTall = pagesTall,
            Orientation = orientation,
            OverlapMm = overlapMm,
            MarginMm = marginMm,
            PrintOrder = printOrder,
            PaperWidthMm = paperWidthMm,
            PaperHeightMm = paperHeightMm
        };
        layout.ComputeInternal(imageWidthPx, imageHeightPx, printableWidthMm, printableHeightMm);
        return layout;
    }

    private void ComputeInternal(int imageWidthPx, int imageHeightPx, double printableW, double printableH)
    {
        // Apply orientation to printable dimensions
        PrintableWidthMm = Orientation == PageOrientation.Portrait ? printableW : printableH;
        PrintableHeightMm = Orientation == PageOrientation.Portrait ? printableH : printableW;

        PrintableWidthMm = Math.Max(1, PrintableWidthMm);
        PrintableHeightMm = Math.Max(1, PrintableHeightMm);

        // Content area per tile: printable area minus user margins on all sides
        // This is the actual area available for image content on each page
        double contentWidthMm = Math.Max(1, PrintableWidthMm - 2 * MarginMm);
        double contentHeightMm = Math.Max(1, PrintableHeightMm - 2 * MarginMm);

        // Total coverage in mm across all tiles (tiles share overlap at internal edges)
        // Use content dimensions, not printable dimensions, since margins reduce usable area
        TotalWidthMm  = PagesWide  * contentWidthMm  - (PagesWide  - 1) * OverlapMm;
        TotalHeightMm = PagesTall * contentHeightMm - (PagesTall - 1) * OverlapMm;

        TotalWidthMm  = Math.Max(1, TotalWidthMm);
        TotalHeightMm = Math.Max(1, TotalHeightMm);

        // Scale: mm per source pixel
        double scaleX = TotalWidthMm  / imageWidthPx;
        double scaleY = TotalHeightMm / imageHeightPx;

        MmPerPx = Math.Min(scaleX, scaleY);

        if (MmPerPx <= 0) MmPerPx = 0.001;

        // Effective print resolution: 25.4 mm/inch ÷ (mm/px) = px/inch
        EffectiveDpi = 25.4 / MmPerPx;

        // Tile size in source pixels (based on content area, not full printable area)
        double tileWPx = contentWidthMm / MmPerPx;
        double tileHPx = contentHeightMm / MmPerPx;

        // Step between tile origins in source pixels (overlap reduces the stride)
        double stepXPx = (contentWidthMm - OverlapMm) / MmPerPx;
        double stepYPx = (contentHeightMm - OverlapMm) / MmPerPx;

        if (stepXPx <= 0) stepXPx = tileWPx;
        if (stepYPx <= 0) stepYPx = tileHPx;

        // Image bounds in source pixels for filtering empty tiles
        var imageBounds = new System.Windows.Rect(0, 0, imageWidthPx, imageHeightPx);

        // Build tile list based on print order, skipping tiles that don't intersect the image
        var tiles = new List<TileInfo>(PagesWide * PagesTall);

        if (PrintOrder == PrintOrder.LeftToRightThenDown)
        {
            // Row-major order: left to right, then top to bottom
            for (int r = 0; r < PagesTall; r++)
            {
                for (int c = 0; c < PagesWide; c++)
                {
                    double srcX = c * stepXPx;
                    double srcY = r * stepYPx;
                    var tileRect = new System.Windows.Rect(srcX, srcY, tileWPx, tileHPx);

                    // Only include tiles that intersect with the image
                    if (tileRect.IntersectsWith(imageBounds))
                    {
                        tiles.Add(new TileInfo
                        {
                            Row = r,
                            Col = c,
                            SourceRect = tileRect
                        });
                    }
                }
            }
        }
        else // TopToBottomThenRight
        {
            // Column-major order: top to bottom, then left to right
            for (int c = 0; c < PagesWide; c++)
            {
                for (int r = 0; r < PagesTall; r++)
                {
                    double srcX = c * stepXPx;
                    double srcY = r * stepYPx;
                    var tileRect = new System.Windows.Rect(srcX, srcY, tileWPx, tileHPx);

                    // Only include tiles that intersect with the image
                    if (tileRect.IntersectsWith(imageBounds))
                    {
                        tiles.Add(new TileInfo
                        {
                            Row = r,
                            Col = c,
                            SourceRect = tileRect
                        });
                    }
                }
            }
        }

        Tiles = tiles;
    }
}
