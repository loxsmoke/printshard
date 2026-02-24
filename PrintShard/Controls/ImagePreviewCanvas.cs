using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PrintShard.Models;

namespace PrintShard.Controls;

/// <summary>
/// Custom rendering element that displays a source image with tiled page-layout overlays.
/// Supports zoom (mouse wheel, 10 %–1000 %) and pan (click-drag).
/// </summary>
public sealed class ImagePreviewCanvas : FrameworkElement
{
    // ── Dependency Properties ─────────────────────────────────────────────────

    public static readonly DependencyProperty SourceImageProperty =
        DependencyProperty.Register(nameof(SourceImage), typeof(BitmapSource),
            typeof(ImagePreviewCanvas),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender,
                OnVisualPropertyChanged));

    public static readonly DependencyProperty LayoutProperty =
        DependencyProperty.Register(nameof(Layout), typeof(TileLayout),
            typeof(ImagePreviewCanvas),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender,
                OnVisualPropertyChanged));

    public static readonly DependencyProperty ShowPageBordersProperty =
        DependencyProperty.Register(nameof(ShowPageBorders), typeof(bool),
            typeof(ImagePreviewCanvas),
            new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty OverlapColorProperty =
        DependencyProperty.Register(nameof(OverlapColor), typeof(Color),
            typeof(ImagePreviewCanvas),
            new FrameworkPropertyMetadata(Color.FromArgb(102, 255, 255, 0),
                FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BorderColorProperty =
        DependencyProperty.Register(nameof(BorderColor), typeof(Color),
            typeof(ImagePreviewCanvas),
            new FrameworkPropertyMetadata(Colors.Red,
                FrameworkPropertyMetadataOptions.AffectsRender));

    private static void OnVisualPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ImagePreviewCanvas c)
            c.ResetView();
    }

    // ── CLR wrappers ──────────────────────────────────────────────────────────

    public BitmapSource? SourceImage
    {
        get => (BitmapSource?)GetValue(SourceImageProperty);
        set => SetValue(SourceImageProperty, value);
    }

    public TileLayout? Layout
    {
        get => (TileLayout?)GetValue(LayoutProperty);
        set => SetValue(LayoutProperty, value);
    }

    public bool ShowPageBorders
    {
        get => (bool)GetValue(ShowPageBordersProperty);
        set => SetValue(ShowPageBordersProperty, value);
    }

    public Color OverlapColor
    {
        get => (Color)GetValue(OverlapColorProperty);
        set => SetValue(OverlapColorProperty, value);
    }

    public Color BorderColor
    {
        get => (Color)GetValue(BorderColorProperty);
        set => SetValue(BorderColorProperty, value);
    }

    // ── File-drop event ───────────────────────────────────────────────────────

    public event EventHandler<string>? FileDrop;

    // ── Zoom / Pan state ──────────────────────────────────────────────────────

    private double _zoom     = 1.0;   // 1 = fit-to-window
    private Vector _pan      = new(); // offset in canvas pixels
    private Point  _lastMouse;
    private bool   _isPanning;

    // ── Constructor ───────────────────────────────────────────────────────────

    public ImagePreviewCanvas()
    {
        AllowDrop    = true;
        ClipToBounds = true;
    }

    // ── Rendering ─────────────────────────────────────────────────────────────

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        // Background
        dc.DrawRectangle(SystemColors.ControlDarkBrush, null, new Rect(RenderSize));

        var image = SourceImage;
        if (image == null)
        {
            DrawPlaceholder(dc);
            return;
        }

        double canvasW = ActualWidth;
        double canvasH = ActualHeight;
        if (canvasW < 1 || canvasH < 1) return;

        double imgW = image.PixelWidth;
        double imgH = image.PixelHeight;

        double baseScale = GetBaseScale(imgW, imgH, canvasW, canvasH);
        double totalScale = baseScale * _zoom;
        if (totalScale <= 0) return;

        double scaledW = imgW * totalScale;
        double scaledH = imgH * totalScale;
        double cx      = (canvasW - scaledW) / 2 + _pan.X;
        double cy      = (canvasH - scaledH) / 2 + _pan.Y;

        // Draw image
        dc.DrawImage(image, new Rect(cx, cy, scaledW, scaledH));

        // Draw overlays
        var layout = Layout;
        if (layout != null)
        {
            // Push transform: converts image-pixel coordinates → canvas coordinates
            dc.PushTransform(new TranslateTransform(cx, cy));
            dc.PushTransform(new ScaleTransform(totalScale, totalScale));

            DrawOverlays(dc, layout, imgW, imgH, totalScale);

            dc.Pop();
            dc.Pop();
        }
    }

    private void DrawPlaceholder(DrawingContext dc)
    {
        var dip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var tf  = new FormattedText(
            "Open an image to get started\n(File > Open  or drag-and-drop)",
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI"),
            14,
            SystemColors.GrayTextBrush,
            dip);
        tf.TextAlignment = TextAlignment.Center;
        dc.DrawText(tf, new Point(ActualWidth / 2, ActualHeight / 2 - tf.Height / 2));
    }

    private void DrawOverlays(DrawingContext dc, TileLayout layout,
        double imgW, double imgH, double totalScale)
    {
        double overlapPx = layout.MmPerPx > 0 ? layout.OverlapMm / layout.MmPerPx : 0;

        if (overlapPx > 0.5)
            DrawOverlapShading(dc, layout, imgW, imgH, overlapPx);

        if (ShowPageBorders)
            DrawPageBorders(dc, layout, imgW, imgH, totalScale);

        DrawTileLabels(dc, layout, imgW, imgH, totalScale);
    }

    private void DrawOverlapShading(DrawingContext dc, TileLayout layout,
        double imgW, double imgH, double overlapPx)
    {
        var brush = new SolidColorBrush(OverlapColor);
        brush.Freeze();

        int cols = layout.PagesWide;
        int rows = layout.PagesTall;

        // Vertical overlap strips (between adjacent columns)
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols - 1; c++)
            {
                var tile = layout.Tiles[r * cols + c];
                double x      = tile.SourceRect.Right - overlapPx;
                var    strip  = ClampRect(new Rect(x, tile.SourceRect.Y, overlapPx, tile.SourceRect.Height), imgW, imgH);
                if (!strip.IsEmpty) dc.DrawRectangle(brush, null, strip);
            }
        }

        // Horizontal overlap strips (between adjacent rows)
        for (int r = 0; r < rows - 1; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                var tile = layout.Tiles[r * cols + c];
                double y      = tile.SourceRect.Bottom - overlapPx;
                var    strip  = ClampRect(new Rect(tile.SourceRect.X, y, tile.SourceRect.Width, overlapPx), imgW, imgH);
                if (!strip.IsEmpty) dc.DrawRectangle(brush, null, strip);
            }
        }
    }

    private void DrawPageBorders(DrawingContext dc, TileLayout layout,
        double imgW, double imgH, double totalScale)
    {
        double penWidth = Math.Max(0.5, 1.0 / totalScale); // constant ~1 px on screen
        var    brush    = new SolidColorBrush(BorderColor);
        brush.Freeze();
        var pen = new Pen(brush, penWidth) { DashStyle = DashStyles.Dash };
        pen.Freeze();

        foreach (var tile in layout.Tiles)
        {
            var r = ClampRect(tile.SourceRect, imgW, imgH);
            if (!r.IsEmpty) dc.DrawRectangle(null, pen, r);
        }
    }

    private void DrawTileLabels(DrawingContext dc, TileLayout layout,
        double imgW, double imgH, double totalScale)
    {
        double dip      = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        double fontSize = Math.Max(6, 12.0 / totalScale); // ~12 px on screen
        double offset   = Math.Max(2, 5.0  / totalScale);
        var    typeface = new Typeface("Segoe UI");
        var    brush    = new SolidColorBrush(Color.FromArgb(200, 200, 30, 30));
        brush.Freeze();

        foreach (var tile in layout.Tiles)
        {
            var r = ClampRect(tile.SourceRect, imgW, imgH);
            if (r.IsEmpty || r.Width < fontSize * 2) continue;

            var ft = new FormattedText(
                tile.Label,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                typeface,
                fontSize,
                brush,
                dip);

            dc.DrawText(ft, new Point(r.X + offset, r.Y + offset));
        }
    }

    // ── Zoom / Pan ────────────────────────────────────────────────────────────

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        var image = SourceImage;
        if (image == null)
        {
            e.Handled = true;
            return;
        }

        double canvasW = ActualWidth;
        double canvasH = ActualHeight;
        if (canvasW < 1 || canvasH < 1)
        {
            e.Handled = true;
            return;
        }

        double imgW = image.PixelWidth;
        double imgH = image.PixelHeight;
        double baseScale = GetBaseScale(imgW, imgH, canvasW, canvasH);
        double oldTotalScale = baseScale * _zoom;

        // Calculate current image bounds
        double scaledW = imgW * oldTotalScale;
        double scaledH = imgH * oldTotalScale;
        double imgLeft = (canvasW - scaledW) / 2 + _pan.X;
        double imgTop  = (canvasH - scaledH) / 2 + _pan.Y;

        // Get mouse position
        Point mousePos = e.GetPosition(this);

        // Determine zoom anchor point
        Point zoomAnchor;
        bool mouseOverImage = mousePos.X >= imgLeft && mousePos.X <= imgLeft + scaledW &&
                              mousePos.Y >= imgTop  && mousePos.Y <= imgTop + scaledH;

        if (mouseOverImage)
        {
            // Zoom towards mouse cursor
            zoomAnchor = mousePos;
        }
        else
        {
            // Zoom towards center of window
            zoomAnchor = new Point(canvasW / 2, canvasH / 2);
        }

        // Calculate the point in image-space that's under the anchor
        double imgSpaceX = (zoomAnchor.X - imgLeft) / oldTotalScale;
        double imgSpaceY = (zoomAnchor.Y - imgTop) / oldTotalScale;

        // Apply zoom
        double factor = e.Delta > 0 ? 1.15 : 1.0 / 1.15;
        double newZoom = Math.Clamp(_zoom * factor, 0.1, 10.0);

        if (Math.Abs(newZoom - _zoom) < 0.0001)
        {
            e.Handled = true;
            return;
        }

        _zoom = newZoom;
        double newTotalScale = baseScale * _zoom;

        // Calculate new image bounds (before pan adjustment)
        double newScaledW = imgW * newTotalScale;
        double newScaledH = imgH * newTotalScale;
        double newImgLeft = (canvasW - newScaledW) / 2 + _pan.X;
        double newImgTop  = (canvasH - newScaledH) / 2 + _pan.Y;

        // Calculate where the anchor point would be with current pan
        double newAnchorX = newImgLeft + imgSpaceX * newTotalScale;
        double newAnchorY = newImgTop + imgSpaceY * newTotalScale;

        // Adjust pan so the anchor point stays under the cursor/center
        _pan.X += zoomAnchor.X - newAnchorX;
        _pan.Y += zoomAnchor.Y - newAnchorY;

        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        _isPanning  = true;
        _lastMouse  = e.GetPosition(this);
        CaptureMouse();
        Cursor = Cursors.SizeAll;
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (!_isPanning) return;
        var  pos    = e.GetPosition(this);
        _pan       += pos - _lastMouse;
        _lastMouse  = pos;
        InvalidateVisual();
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        _isPanning = false;
        ReleaseMouseCapture();
        Cursor = Cursors.Arrow;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void FitToWindow()
    {
        _zoom = 1.0;
        _pan  = new Vector();
        InvalidateVisual();
    }

    public void ZoomIn()  => ApplyZoom(_zoom * 1.2);
    public void ZoomOut() => ApplyZoom(_zoom / 1.2);

    private void ApplyZoom(double newZoom)
    {
        _zoom = Math.Clamp(newZoom, 0.1, 10.0);
        InvalidateVisual();
    }

    private void ResetView()
    {
        _zoom = 1.0;
        _pan  = new Vector();
        // AffectsRender flag on the DP already triggers invalidation
    }

    // ── Drag-and-drop ─────────────────────────────────────────────────────────

    protected override void OnDragEnter(DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    protected override void OnDragOver(DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    protected override void OnDrop(DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
            FileDrop?.Invoke(this, files[0]);
        e.Handled = true;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static double GetBaseScale(double imgW, double imgH, double canvasW, double canvasH)
    {
        if (imgW <= 0 || imgH <= 0) return 1;
        return Math.Min(canvasW / imgW, canvasH / imgH);
    }

    private static Rect ClampRect(Rect r, double maxW, double maxH)
    {
        double x = Math.Max(0, r.X);
        double y = Math.Max(0, r.Y);
        double w = Math.Min(maxW, r.Right)  - x;
        double h = Math.Min(maxH, r.Bottom) - y;
        return w > 0 && h > 0 ? new Rect(x, y, w, h) : Rect.Empty;
    }
}
