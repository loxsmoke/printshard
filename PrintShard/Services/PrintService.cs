using System.Globalization;
using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PrintShard.Models;

namespace PrintShard.Services;

/// <summary>
/// Provides printing and print-preview rendering for a tiled image layout.
/// </summary>
public static class PrintService
{
    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Spools all tiles directly to the printer without showing a dialog.</summary>
    public static void Print(
        BitmapSource image,
        TileLayout layout,
        AppSettings settings,
        string? selectedPrinterName = null,
        string? paperSizeName = null,
        IProgress<(int current, int total)>? progress = null,
        CancellationToken ct = default)
    {
        using var printServer = new LocalPrintServer();
        PrintQueue? selectedPrinter = null;

        // First, try to use the printer selected in the main window
        if (!string.IsNullOrEmpty(selectedPrinterName))
        {
            try
            {
                selectedPrinter = printServer.GetPrintQueue(selectedPrinterName);
            }
            catch
            {
                // Printer not found, fall back to default
            }
        }

        // If no printer selected or not found, try the system default
        if (selectedPrinter == null)
        {
            try
            {
                selectedPrinter = LocalPrintServer.GetDefaultPrintQueue();
            }
            catch
            {
                // No default printer available
            }
        }

        // Last resort: get the first available printer
        if (selectedPrinter == null)
        {
            var printers = printServer.GetPrintQueues()
                .OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            if (printers.Count > 0)
                selectedPrinter = printers[0];
        }

        bool isLandscape = layout.Orientation == Models.PageOrientation.Landscape;
        var pageOrientation = isLandscape
            ? System.Printing.PageOrientation.Landscape
            : System.Printing.PageOrientation.Portrait;

        // PaperSize always stores portrait-first dimensions; convert to WPF units (1/96 inch).
        double portraitWidthWpf  = layout.PaperWidthMm  / 25.4 * 96.0;
        double portraitHeightWpf = layout.PaperHeightMm / 25.4 * 96.0;

        // Oriented physical page size — fallback when GetPrintCapabilities returns null.
        double orientedPhysW = isLandscape ? portraitHeightWpf : portraitWidthWpf;
        double orientedPhysH = isLandscape ? portraitWidthWpf  : portraitHeightWpf;

        var dialog = new PrintDialog();
        if (selectedPrinter != null)
            dialog.PrintQueue = selectedPrinter;

        // Build the print ticket using a named PageMediaSizeName where possible.
        // Named sizes allow GetPrintCapabilities to return accurate imageable-area data;
        // anonymous dimension-only sizes are often silently remapped or ignored by drivers.
        var sizeName = GetPageMediaSizeName(paperSizeName);
        var ticket = new PrintTicket
        {
            PageOrientation = pageOrientation,
            PageMediaSize   = sizeName != PageMediaSizeName.Unknown
                ? new PageMediaSize(sizeName)
                : new PageMediaSize(portraitWidthWpf, portraitHeightWpf)
        };
        dialog.PrintTicket = ticket;

        // Query capabilities with the configured ticket.
        var printQueue        = dialog.PrintQueue;
        var printCaps         = printQueue.GetPrintCapabilities(dialog.PrintTicket);
        var pageImageableArea = printCaps.PageImageableArea;

        double marginLeft = pageImageableArea?.OriginWidth  ?? 0;
        double marginTop  = pageImageableArea?.OriginHeight ?? 0;

        double physicalWidth  = printCaps.OrientedPageMediaWidth  ?? orientedPhysW;
        double physicalHeight = printCaps.OrientedPageMediaHeight ?? orientedPhysH;

        // When imageable-area extents are unavailable, assume symmetric margins.
        double extentW = pageImageableArea?.ExtentWidth  ?? (physicalWidth  - 2 * marginLeft);
        double extentH = pageImageableArea?.ExtentHeight ?? (physicalHeight - 2 * marginTop);

        double marginRight  = physicalWidth  - marginLeft - extentW;
        double marginBottom = physicalHeight - marginTop  - extentH;

        var printerMargins = new Thickness(marginLeft, marginTop, marginRight, marginBottom);
        var physicalSize   = new Size(physicalWidth, physicalHeight);

        // renderSize: use the capabilities extents when available — with a named
        // PageMediaSizeName in the ticket, the driver returns accurate imageable-area
        // data for this exact paper and orientation.  Fall back to the layout's
        // pre-computed dimensions only when the driver returns no imageable area.
        var renderSize = new Size(
            pageImageableArea?.ExtentWidth  ?? layout.PrintableWidthMm / 25.4 * 96.0,
            pageImageableArea?.ExtentHeight ?? layout.PrintableHeightMm / 25.4 * 96.0);

        var fixedDoc = CreateFixedDocument(image, layout, settings, renderSize, physicalSize, printerMargins);
        dialog.PrintDocument(fixedDoc.DocumentPaginator, "PrintShard");
    }

    /// <summary>
    /// Renders a single tile page to a <see cref="RenderTargetBitmap"/> suitable for preview.
    /// </summary>
    /// <param name="dpi">Screen DPI for the preview rendering.</param>
    public static RenderTargetBitmap RenderPagePreview(
        BitmapSource image,
        TileLayout layout,
        AppSettings settings,
        int pageIndex,
        double previewWidthPx,
        double dpi = 96)
    {
        if (pageIndex < 0 || pageIndex >= layout.Tiles.Count)
            throw new ArgumentOutOfRangeException(nameof(pageIndex));

        var pageSize = new Size(previewWidthPx, previewWidthPx * layout.PrintableHeightMm / layout.PrintableWidthMm);
        var visual   = BuildPageVisual(image, layout.Tiles[pageIndex], layout, settings, pageSize);

        var rtb = new RenderTargetBitmap(
            (int)Math.Ceiling(pageSize.Width), (int)Math.Ceiling(pageSize.Height),
            dpi, dpi, PixelFormats.Pbgra32);
        rtb.Render(visual);
        rtb.Freeze();
        return rtb;
    }

    // ── Internals ─────────────────────────────────────────────────────────────

    internal static DrawingVisual BuildPageVisual(
        BitmapSource image,
        TileInfo tile,
        TileLayout layout,
        AppSettings settings,
        Size pageSize)
    {
        var visual = new DrawingVisual();
        using var dc = visual.RenderOpen();

        // The pageSize represents the printable area. Content must fit within pageSize.
        // We don't add offsets to content positions since the coordinate system already
        // starts at (0,0) within the printable area.

        // White background (covers the full printable area)
        dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, pageSize.Width, pageSize.Height));

        // Calculate margin in page units (proportion of page size)
        double marginRatioX = layout.MarginMm / layout.PrintableWidthMm;
        double marginRatioY = layout.MarginMm / layout.PrintableHeightMm;
        double marginPxX = pageSize.Width * marginRatioX;
        double marginPxY = pageSize.Height * marginRatioY;

        // The content area is the page minus margins on all sides
        double contentW = pageSize.Width - 2 * marginPxX;
        double contentH = pageSize.Height - 2 * marginPxY;

        // Crop source image to this tile's source rect (clamped to image bounds)
        var srcRect = ClampTileRect(tile.SourceRect, image.PixelWidth, image.PixelHeight);
        if (srcRect.Width > 0 && srcRect.Height > 0 && contentW > 0 && contentH > 0)
        {
            BitmapSource tileBitmap = new CroppedBitmap(image, srcRect);

            // Destination rect: scale to fill the content area (inside margins),
            // preserving tile aspect ratio if the tile doesn't cover the full image in that dimension.
            double destW = contentW;
            double destH = contentH;

            // If the source rect is smaller than the nominal tile (e.g. edge tiles),
            // scale the destination proportionally so pixels aren't stretched.
            double nomW = tile.SourceRect.Width;
            double nomH = tile.SourceRect.Height;
            double actualW = srcRect.Width;
            double actualH = srcRect.Height;

            destW *= actualW / nomW;
            destH *= actualH / nomH;

            // Draw image with margin offset (within the printable area)
            dc.DrawImage(tileBitmap, new Rect(marginPxX, marginPxY, destW, destH));
        }

        // Registration marks (L-shaped corner crop marks) - at image content boundaries
        if (settings.CropMarkStyle != CropMarkStyle.None)
            DrawCropMarks(dc, pageSize, marginPxX, marginPxY, settings.CropMarkStyle);

        // Tile label
        if (settings.ShowTileLabel)
            DrawTileLabel(dc, tile, visual, marginPxX, marginPxY);

        return visual;
    }

    private static Int32Rect ClampTileRect(System.Windows.Rect src, int imgW, int imgH)
    {
        int x = (int)Math.Max(0, src.X);
        int y = (int)Math.Max(0, src.Y);
        int w = (int)Math.Min(imgW - x, src.Width + (src.X < 0 ? src.X : 0));
        int h = (int)Math.Min(imgH - y, src.Height + (src.Y < 0 ? src.Y : 0));
        if (w <= 0 || h <= 0) return new Int32Rect(0, 0, 0, 0);
        return new Int32Rect(x, y, w, h);
    }

    private static void DrawCropMarks(DrawingContext dc, Size pageSize, double marginX, double marginY, CropMarkStyle style)
    {
        const double Thick = 0.75;
        const double Half  = Thick / 2.0;
        var pen = new Pen(Brushes.Black, Thick);

        // Inset each anchor by half the pen width so the full stroke stays within the
        // bitmap and within the printer's imageable area.  Without this, lines whose
        // centre sits exactly on the imageable-area boundary have their outer half
        // clipped by the printer's hardware margin and disappear on paper.
        double left   = marginX + Half;
        double top    = marginY + Half;
        double right  = pageSize.Width  - marginX - Half;
        double bottom = pageSize.Height - marginY - Half;

        if (style == CropMarkStyle.Corners)
        {
            const double Len = 18;

            // Top-left
            dc.DrawLine(pen, new Point(left, top), new Point(left + Len, top));
            dc.DrawLine(pen, new Point(left, top), new Point(left, top + Len));
            // Top-right
            dc.DrawLine(pen, new Point(right, top), new Point(right - Len, top));
            dc.DrawLine(pen, new Point(right, top), new Point(right, top + Len));
            // Bottom-left
            dc.DrawLine(pen, new Point(left, bottom), new Point(left + Len, bottom));
            dc.DrawLine(pen, new Point(left, bottom), new Point(left, bottom - Len));
            // Bottom-right
            dc.DrawLine(pen, new Point(right, bottom), new Point(right - Len, bottom));
            dc.DrawLine(pen, new Point(right, bottom), new Point(right, bottom - Len));
        }
        else if (style == CropMarkStyle.FullLines)
        {
            // Draw full lines around the image content area
            // Top line
            dc.DrawLine(pen, new Point(left, top), new Point(right, top));
            // Bottom line
            dc.DrawLine(pen, new Point(left, bottom), new Point(right, bottom));
            // Left line
            dc.DrawLine(pen, new Point(left, top), new Point(left, bottom));
            // Right line
            dc.DrawLine(pen, new Point(right, top), new Point(right, bottom));
        }
    }

    private static PageMediaSizeName GetPageMediaSizeName(string? paperName) =>
        paperName?.ToLowerInvariant() switch
        {
            "a3"      => PageMediaSizeName.ISOA3,
            "a4"      => PageMediaSizeName.ISOA4,
            "a5"      => PageMediaSizeName.ISOA5,
            "letter"  => PageMediaSizeName.NorthAmericaLetter,
            "legal"   => PageMediaSizeName.NorthAmericaLegal,
            "tabloid" => PageMediaSizeName.NorthAmericaTabloid,
            _         => PageMediaSizeName.Unknown,
        };

    private static void DrawTileLabel(DrawingContext dc, TileInfo tile, Visual reference, double offsetX, double offsetY)
    {
        double pixelsPerDip = 1.0;
        try { pixelsPerDip = VisualTreeHelper.GetDpi(reference).PixelsPerDip; } catch { }

        var ft = new FormattedText(
            tile.Label,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI"),
            12,
            new SolidColorBrush(Color.FromArgb(200, 80, 80, 80)),
            pixelsPerDip);

        dc.DrawText(ft, new Point(offsetX + 6, offsetY + 6));
    }

    /// <summary>
    /// Creates a FixedDocument containing all tile pages for printing.
    /// </summary>
    /// <param name="printableSize">The printable area size (content dimensions).</param>
    /// <param name="physicalSize">The physical page size (media dimensions).</param>
    /// <param name="printerMargins">The printer's unprintable margins (offset from physical to printable area).</param>
    private static FixedDocument CreateFixedDocument(
        BitmapSource image,
        TileLayout layout,
        AppSettings settings,
        Size printableSize,
        Size physicalSize,
        Thickness printerMargins)
    {
        var fixedDoc = new FixedDocument();
        fixedDoc.DocumentPaginator.PageSize = physicalSize;

        foreach (var tile in layout.Tiles)
        {
            // Render the visual in printable area coordinates (0,0 origin)
            var visual = BuildPageVisual(image, tile, layout, settings, printableSize);

            // Render the visual to a bitmap at the printable area size
            var rtb = new RenderTargetBitmap(
                (int)Math.Ceiling(printableSize.Width), (int)Math.Ceiling(printableSize.Height),
                96, 96, PixelFormats.Pbgra32);
            rtb.Render(visual);
            rtb.Freeze();

            // Create an Image element to display the rendered bitmap
            var imageElement = new Image
            {
                Source = rtb,
                Width = printableSize.Width,
                Height = printableSize.Height
            };

            // Create a FixedPage at the physical page size
            var fixedPage = new FixedPage
            {
                Width = physicalSize.Width,
                Height = physicalSize.Height
            };

            // Position the content at the printable area origin (accounting for printer margins)
            FixedPage.SetLeft(imageElement, printerMargins.Left);
            FixedPage.SetTop(imageElement, printerMargins.Top);
            fixedPage.Children.Add(imageElement);

            // Create a PageContent and add it to the document
            var pageContent = new PageContent();
            ((IAddChild)pageContent).AddChild(fixedPage);
            fixedDoc.Pages.Add(pageContent);
        }

        return fixedDoc;
    }
}
