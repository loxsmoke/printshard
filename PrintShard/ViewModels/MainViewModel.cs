using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using PrintShard.Models;
using PrintShard.Services;
using PrintShard.Views;

namespace PrintShard.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private const string NoPrintersInstalled = "No printers installed";

    private static readonly string AppVersion =
        System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";

    private readonly AppSettings _settings;

    // ── Image state ──────────────────────────────────────────────────────────
    private BitmapSource? _loadedImage;
    private string?       _loadedFilePath;

    // ── Layout controls ──────────────────────────────────────────────────────
    private int             _pagesWide;
    private int             _pagesTall;
    private PageOrientation _orientation;
    private double          _overlapMm;
    private double          _marginMm;
    private PrintOrder      _printOrder;
    private bool            _noOverlapDefaultMargins;
    private CropMarkStyle   _cropMarkStyle;

    // ── Printer / Paper ──────────────────────────────────────────────────────
    private string?   _selectedPrinterName;
    private PaperSize _selectedPaperSize;

    // ── View toggles ─────────────────────────────────────────────────────────
    private bool _showPageBorders;

    // ── Computed layout ──────────────────────────────────────────────────────
    private TileLayout? _currentLayout;

    // ── Recent files ─────────────────────────────────────────────────────────
    public ObservableCollection<string> RecentFiles        { get; } = [];
    public ObservableCollection<string> AvailablePrinters  { get; } = [];
    public ObservableCollection<PaperSize> AvailablePaperSizes { get; } = [.. PaperSize.All];

    // ═════════════════════════════════════════════════════════════════════════
    //  Constructor
    // ═════════════════════════════════════════════════════════════════════════

    public MainViewModel(AppSettings settings)
    {
        _settings = settings;

        _pagesWide   = settings.DefaultPagesWide;
        _pagesTall   = settings.DefaultPagesTall;
        _orientation = settings.DefaultOrientation;
        _printOrder  = settings.DefaultPrintOrder;
        _cropMarkStyle = settings.CropMarkStyle;

        // Default to "no overlap, default margins" mode
        _noOverlapDefaultMargins = true;
        _overlapMm = 0;
        _marginMm = 0;

        _showPageBorders    = settings.ShowPageBorders;

        _selectedPaperSize = PaperSize.A4; // Default fallback, will be updated by LoadPrinters

        foreach (var f in settings.RecentFiles)
            RecentFiles.Add(f);

        // Commands
        LoadImageCommand       = new RelayCommand<string?>(ExecuteLoadImage);
        DropImageCommand       = new RelayCommand<string?>(ExecuteDropImage);
        OpenFileCommand        = new RelayCommand(ExecuteOpenFile);
        OpenRecentFileCommand  = new RelayCommand<string?>(p => ExecuteLoadImage(p));
        PrintCommand           = new RelayCommand(ExecutePrint,        CanPrint);
        ExitCommand            = new RelayCommand(() => Application.Current.Shutdown());
        AboutCommand           = new RelayCommand(ExecuteAbout);

        ZoomInCommand      = new RelayCommand(() => ZoomInAction?.Invoke());
        ZoomOutCommand     = new RelayCommand(() => ZoomOutAction?.Invoke());
        FitToWindowCommand = new RelayCommand(() => FitToWindowAction?.Invoke());

        LoadPrinters();
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Properties — Layout controls
    // ═════════════════════════════════════════════════════════════════════════

    public int PagesWide
    {
        get => _pagesWide;
        set { if (SetField(ref _pagesWide, Math.Clamp(value, 1, 20))) RecomputeLayout(); }
    }

    public int PagesTall
    {
        get => _pagesTall;
        set { if (SetField(ref _pagesTall, Math.Clamp(value, 1, 20))) RecomputeLayout(); }
    }

    public PageOrientation Orientation
    {
        get => _orientation;
        set { if (SetField(ref _orientation, value)) { RecomputeLayout(); OnPropertyChanged(nameof(PaperDimensionsDisplay)); } }
    }

    public double OverlapMm
    {
        get => _overlapMm;
        set { if (SetField(ref _overlapMm, Math.Clamp(value, 0, 50))) RecomputeLayout(); }
    }

    public double MarginMm
    {
        get => _marginMm;
        set { if (SetField(ref _marginMm, Math.Clamp(value, 0, 50))) RecomputeLayout(); }
    }

    /// <summary>
    /// Gets or sets the overlap value in display units (mm or inches based on system settings).
    /// Internally converts to/from millimeters.
    /// </summary>
    public double OverlapDisplay
    {
        get => MeasurementService.MmToDisplayUnit(_overlapMm);
        set
        {
            double mm = MeasurementService.DisplayUnitToMm(value);
            if (SetField(ref _overlapMm, Math.Clamp(mm, 0, 50), nameof(OverlapMm)))
            {
                OnPropertyChanged(nameof(OverlapDisplay));
                RecomputeLayout();
            }
        }
    }

    /// <summary>
    /// Gets or sets the margin value in display units (mm or inches based on system settings).
    /// Internally converts to/from millimeters.
    /// </summary>
    public double MarginDisplay
    {
        get => MeasurementService.MmToDisplayUnit(_marginMm);
        set
        {
            double mm = MeasurementService.DisplayUnitToMm(value);
            if (SetField(ref _marginMm, Math.Clamp(mm, 0, 50), nameof(MarginMm)))
            {
                OnPropertyChanged(nameof(MarginDisplay));
                RecomputeLayout();
            }
        }
    }

    public PrintOrder PrintOrder
    {
        get => _printOrder;
        set { if (SetField(ref _printOrder, value)) RecomputeLayout(); }
    }

    public CropMarkStyle CropMarkStyle
    {
        get => _cropMarkStyle;
        set
        {
            if (SetField(ref _cropMarkStyle, value))
            {
                _settings.CropMarkStyle = value;
                SettingsService.Save(_settings);
            }
        }
    }

    public static IReadOnlyList<CropMarkStyle> CropMarkStyles { get; } = Enum.GetValues<CropMarkStyle>();

    public bool NoOverlapDefaultMargins
    {
        get => _noOverlapDefaultMargins;
        set
        {
            if (SetField(ref _noOverlapDefaultMargins, value))
            {
                if (value)
                {
                    // Set overlap to 0 and margin to minimum (0 for now, as printer minimum varies)
                    OverlapMm = 0;
                    MarginMm = 0;
                }
                OnPropertyChanged(nameof(OverlapMarginEnabled));
            }
        }
    }

    public bool OverlapMarginEnabled => !_noOverlapDefaultMargins;

    // ═════════════════════════════════════════════════════════════════════════
    //  Properties — Image
    // ═════════════════════════════════════════════════════════════════════════

    public BitmapSource? LoadedImage
    {
        get => _loadedImage;
        private set
        {
            SetField(ref _loadedImage, value);
            RecomputeLayout();
            UpdateStatusBar();
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public string? LoadedFilePath
    {
        get => _loadedFilePath;
        private set 
        { 
            SetField(ref _loadedFilePath, value); 
            UpdateStatusBar();
            OnPropertyChanged(nameof(WindowTitle));
        }
    }

    /// <summary>
    /// Gets the window title, including the loaded file name if available.
    /// </summary>
    public string WindowTitle
    {
        get
        {
            if (string.IsNullOrEmpty(_loadedFilePath))
                return $"PrintShard {AppVersion}";
            return $"PrintShard {AppVersion} - {Path.GetFileName(_loadedFilePath)}";
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Properties — Printer / Paper
    // ═════════════════════════════════════════════════════════════════════════

    public string? SelectedPrinterName
    {
        get => _selectedPrinterName;
        set
        {
            if (SetField(ref _selectedPrinterName, value))
            {
                UpdatePaperSizeFromPrinter(value);
                RecomputeLayout();
                UpdateStatusBar();
            }
        }
    }

    public PaperSize SelectedPaperSize
    {
        get => _selectedPaperSize;
        set 
        { 
            if (SetField(ref _selectedPaperSize, value)) 
            { 
                RecomputeLayout(); 
                UpdateStatusBar();
                OnPropertyChanged(nameof(PaperDimensionsDisplay));
            } 
        }
    }

    /// <summary>
    /// Gets the display string for paper dimensions based on system measurement units.
    /// </summary>
    public string PaperDimensionsDisplay
    {
        get
        {
            if (_selectedPaperSize == null) return string.Empty;

            double w = _orientation == PageOrientation.Portrait 
                ? _selectedPaperSize.WidthMm 
                : _selectedPaperSize.HeightMm;
            double h = _orientation == PageOrientation.Portrait 
                ? _selectedPaperSize.HeightMm 
                : _selectedPaperSize.WidthMm;

            return Services.MeasurementService.FormatPaperSize(w, h);
        }
    }

    /// <summary>
    /// Gets the unit label for overlap and margin fields.
    /// </summary>
    public string MeasurementUnit => Services.MeasurementService.UnitSuffix;

    /// <summary>
    /// Gets the increment value for overlap and margin spinners based on measurement unit.
    /// Returns 1 for millimeters, 0.1 for inches.
    /// </summary>
    public double OverlapMarginIncrement => Services.MeasurementService.IsMetric ? 1.0 : 0.1;

    // ═════════════════════════════════════════════════════════════════════════
    //  Properties — View toggles
    // ═════════════════════════════════════════════════════════════════════════

    public bool ShowPageBorders
    {
        get => _showPageBorders;
        set
        {
            if (SetField(ref _showPageBorders, value))
            {
                _settings.ShowPageBorders = value;
                SettingsService.Save(_settings);
            }
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Properties — Colors (exposed as Color for canvas binding)
    // ═════════════════════════════════════════════════════════════════════════

    public Color OverlapShadingColorValue
        => ParseColor(_settings.OverlapShadingColor, Color.FromArgb(102, 255, 255, 0));

    public Color PageBorderColorValue
        => ParseColor(_settings.PageBorderColor, Colors.Red);

    private static Color ParseColor(string hex, Color fallback)
    {
        try { return (Color)ColorConverter.ConvertFromString(hex); }
        catch { return fallback; }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Properties — Computed layout
    // ═════════════════════════════════════════════════════════════════════════

    public TileLayout? CurrentLayout
    {
        get => _currentLayout;
        private set { SetField(ref _currentLayout, value); UpdateStatusBar(); }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Properties — Page count warnings
    // ═════════════════════════════════════════════════════════════════════════

    private bool _hasExcessColumns;
    private bool _hasExcessRows;

    /// <summary>True if some columns would be empty and not printed.</summary>
    public bool HasExcessColumns
    {
        get => _hasExcessColumns;
        private set => SetField(ref _hasExcessColumns, value);
    }

    /// <summary>True if some rows would be empty and not printed.</summary>
    public bool HasExcessRows
    {
        get => _hasExcessRows;
        private set => SetField(ref _hasExcessRows, value);
    }

    public string ExcessPagesWarning => "Too many pages. Empty pages will not be printed.";

    // ═════════════════════════════════════════════════════════════════════════
    //  Properties — Status bar
    // ═════════════════════════════════════════════════════════════════════════

    private string _statusFilename    = "No file loaded";
    private string _statusImageSize   = string.Empty;
    private string _statusPrinterPaper = string.Empty;
    private string _statusLayout      = string.Empty;
    private string _statusDpi         = string.Empty;

    public string StatusFilename     { get => _statusFilename;     private set => SetField(ref _statusFilename,     value); }
    public string StatusImageSize    { get => _statusImageSize;    private set => SetField(ref _statusImageSize,    value); }
    public string StatusPrinterPaper { get => _statusPrinterPaper; private set => SetField(ref _statusPrinterPaper, value); }
    public string StatusLayout       { get => _statusLayout;       private set => SetField(ref _statusLayout,       value); }
    public string StatusDpi          { get => _statusDpi;          private set => SetField(ref _statusDpi,          value); }

    private void UpdateStatusBar()
    {
        if (_loadedFilePath != null)
            StatusFilename = Path.GetFileName(_loadedFilePath);
        else
            StatusFilename = "No file loaded";

        if (_loadedImage != null)
            StatusImageSize = $"{_loadedImage.PixelWidth} × {_loadedImage.PixelHeight} px";
        else
            StatusImageSize = string.Empty;

        StatusPrinterPaper = _selectedPrinterName != null
            ? $"{_selectedPrinterName} — {_selectedPaperSize.Name}"
            : _selectedPaperSize.Name;

        if (_currentLayout != null)
        {
            int n = _currentLayout.Tiles.Count;
            StatusLayout = $"{_currentLayout.PagesWide} wide × {_currentLayout.PagesTall} tall = {n} page{(n == 1 ? "" : "s")}";
            StatusDpi    = $"~{_currentLayout.EffectiveDpi:F0} DPI";
        }
        else
        {
            StatusLayout = string.Empty;
            StatusDpi    = string.Empty;
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Commands
    // ═════════════════════════════════════════════════════════════════════════

    public ICommand LoadImageCommand      { get; }
    public ICommand DropImageCommand      { get; }
    public ICommand OpenFileCommand       { get; }
    public ICommand OpenRecentFileCommand { get; }
    public ICommand PrintCommand          { get; }
    public ICommand ExitCommand           { get; }
    public ICommand AboutCommand          { get; }

    // Zoom commands delegate to actions wired by the View code-behind
    public ICommand ZoomInCommand      { get; }
    public ICommand ZoomOutCommand     { get; }
    public ICommand FitToWindowCommand { get; }

    public Action? ZoomInAction      { get; set; }
    public Action? ZoomOutAction     { get; set; }
    public Action? FitToWindowAction { get; set; }

    // ═════════════════════════════════════════════════════════════════════════
    //  Command implementations
    // ═════════════════════════════════════════════════════════════════════════

    private bool CanPrint() => _loadedImage != null && _currentLayout != null;

    private void ExecuteOpenFile()
    {
        var dlg = new OpenFileDialog
        {
            Filter = ImageLoaderService.DialogFilter,
            Title  = "Open Image"
        };
        if (dlg.ShowDialog() == true)
            ExecuteLoadImage(dlg.FileName);
    }

    private void ExecuteDropImage(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        // If a file is already loaded, ask for confirmation
        if (_loadedImage != null && _loadedFilePath != null)
        {
            var currentFileName = Path.GetFileName(_loadedFilePath);
            var newFileName = Path.GetFileName(path);

            var result = MessageBox.Show(
                $"A file is already open:\n{currentFileName}\n\nDo you want to open the new file?\n{newFileName}",
                "Replace Current Image",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;
        }

        ExecuteLoadImage(path);
    }

    private void ExecuteLoadImage(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        try
        {
            var bitmap = ImageLoaderService.Load(path);

            // Downscale very large images (> 8000 px on either axis)
            if (bitmap.PixelWidth > 8000 || bitmap.PixelHeight > 8000)
            {
                double scale = Math.Min(8000.0 / bitmap.PixelWidth, 8000.0 / bitmap.PixelHeight);
                int scaledW  = (int)(bitmap.PixelWidth  * scale);
                int scaledH  = (int)(bitmap.PixelHeight * scale);
                MessageBox.Show(
                    $"The image ({bitmap.PixelWidth} × {bitmap.PixelHeight} px) exceeds the 8000 px display limit.\n\n" +
                    $"It will be scaled down to {scaledW} × {scaledH} px. " +
                    "This reduces the effective print resolution.",
                    "Image Scaled Down",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                var scaled = new TransformedBitmap(bitmap, new ScaleTransform(scale, scale));
                scaled.Freeze();
                LoadedImage = scaled;
            }
            else
            {
                LoadedImage = bitmap;
            }

            LoadedFilePath = path;
            SettingsService.AddRecentFile(_settings, path);
            RefreshRecentFiles();
        }
        catch (FileNotFoundException)
        {
            MessageBox.Show($"File not found:\n{path}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not load image:\n{ex.Message}\n\nMake sure the file is a supported image format.",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ExecutePrint()
    {
        if (_loadedImage == null || _currentLayout == null) return;
        var vm  = new PrintPreviewViewModel(_loadedImage, _currentLayout, _settings,
            _selectedPrinterName, _selectedPaperSize.Name);
        var win = new PrintPreviewWindow { DataContext = vm };
        win.Owner = Application.Current.MainWindow;
        win.ShowDialog();
    }

    private static void ExecuteAbout()
    {
        var aboutWindow = new AboutWindow
        {
            Owner = Application.Current.MainWindow
        };
        aboutWindow.ShowDialog();
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Layout computation
    // ═════════════════════════════════════════════════════════════════════════

    private void RecomputeLayout()
    {
        if (_loadedImage == null)
        {
            CurrentLayout = null;
            HasExcessColumns = false;
            HasExcessRows = false;
            return;
        }

        var (paperW, paperH, printableW, printableH) = GetPaperAndPrintableDimensions();

        CurrentLayout = TileLayout.Compute(
            _loadedImage.PixelWidth,
            _loadedImage.PixelHeight,
            _pagesWide, _pagesTall,
            _orientation,
            _overlapMm, _marginMm,
            _printOrder,
            paperW, paperH,
            printableW, printableH);

        // Check for excess columns/rows by seeing if reducing by 1 would still cover the image
        UpdateExcessWarnings();
    }

    private void UpdateExcessWarnings()
    {
        if (_loadedImage == null || _currentLayout == null)
        {
            HasExcessColumns = false;
            HasExcessRows = false;
            return;
        }

        // Find the actual used columns and rows from the tiles (0-based indices, -1 if empty)
        int usedColumns = _currentLayout.Tiles.Select(t => t.Col).DefaultIfEmpty(-1).Max() + 1;
        int usedRows = _currentLayout.Tiles.Select(t => t.Row).DefaultIfEmpty(-1).Max() + 1;

        HasExcessColumns = _pagesWide > usedColumns && usedColumns > 0;
        HasExcessRows = _pagesTall > usedRows && usedRows > 0;
    }

    /// <summary>
    /// Gets the paper dimensions and printable (imageable) area dimensions for the selected printer and paper size.
    /// Returns (paperWidth, paperHeight, printableWidth, printableHeight) in mm.
    /// Falls back to full paper size for printable area if printer info is unavailable.
    /// </summary>
    private (double paperW, double paperH, double printableW, double printableH) GetPaperAndPrintableDimensions()
    {
        double paperWidthMm = _selectedPaperSize.WidthMm;
        double paperHeightMm = _selectedPaperSize.HeightMm;

        // Try to get the printer's actual imageable area
        if (!string.IsNullOrEmpty(_selectedPrinterName) && _selectedPrinterName != NoPrintersInstalled)
        {
            try
            {
                using var server = new System.Printing.LocalPrintServer();
                var queue = server.GetPrintQueue(_selectedPrinterName);
                var ticket = queue?.UserPrintTicket ?? new System.Printing.PrintTicket();

                // Set paper size on ticket if possible
                ticket.PageMediaSize = new System.Printing.PageMediaSize(
                    paperWidthMm / 25.4 * 96.0,
                    paperHeightMm / 25.4 * 96.0);

                var capabilities = queue?.GetPrintCapabilities(ticket);
                var imageableArea = capabilities?.PageImageableArea;

                if (imageableArea != null)
                {
                    // ExtentWidth/ExtentHeight are in 1/96 inch units, convert to mm
                    double printableWidthMm = imageableArea.ExtentWidth / 96.0 * 25.4;
                    double printableHeightMm = imageableArea.ExtentHeight / 96.0 * 25.4;

                    // Sanity check: imageable area should be positive and smaller than paper
                    if (printableWidthMm > 0 && printableHeightMm > 0 &&
                        printableWidthMm <= paperWidthMm &&
                        printableHeightMm <= paperHeightMm)
                    {
                        return (paperWidthMm, paperHeightMm, printableWidthMm, printableHeightMm);
                    }
                }
            }
            catch
            {
                // Fall back to full paper size
            }
        }

        // Fallback: printable area equals full paper size
        return (paperWidthMm, paperHeightMm, paperWidthMm, paperHeightMm);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Printer enumeration
    // ═════════════════════════════════════════════════════════════════════════

    private void LoadPrinters()
    {
        string? defaultPrinterName = null;

        try
        {
            using var server = new System.Printing.LocalPrintServer();
            var queues = server.GetPrintQueues([
                System.Printing.EnumeratedPrintQueueTypes.Local,
                System.Printing.EnumeratedPrintQueueTypes.Connections]);

            // Sort printers alphabetically by name
            var sortedPrinters = queues
                .OrderBy(q => q.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            foreach (var q in sortedPrinters)
                AvailablePrinters.Add(q.Name);

            // Try to get the default printer name
            try
            {
                var defaultQueue = System.Printing.LocalPrintServer.GetDefaultPrintQueue();
                defaultPrinterName = defaultQueue?.Name;
            }
            catch
            {
                // No default printer available
            }
        }
        catch
        {
            // System.Printing not available or no printers
        }

        if (AvailablePrinters.Count == 0)
            AvailablePrinters.Add(NoPrintersInstalled);

        // Select default printer if available, otherwise first in the sorted list
        if (!string.IsNullOrEmpty(defaultPrinterName) && AvailablePrinters.Contains(defaultPrinterName))
            SelectedPrinterName = defaultPrinterName;
        else
            SelectedPrinterName = AvailablePrinters[0];
    }

    private void UpdatePaperSizeFromPrinter(string? printerName)
    {
        if (string.IsNullOrEmpty(printerName) || printerName == NoPrintersInstalled)
            return;

        try
        {
            using var server = new System.Printing.LocalPrintServer();
            var queue = server.GetPrintQueue(printerName);
            var ticket = queue?.DefaultPrintTicket;
            var mediaSize = ticket?.PageMediaSize;

            if (mediaSize?.Width is double widthInMicrons && mediaSize?.Height is double heightInMicrons)
            {
                // Convert from 1/96 inch units to millimeters
                // PageMediaSize dimensions are in 1/96 inch units
                double widthMm = widthInMicrons / 96.0 * 25.4;
                double heightMm = heightInMicrons / 96.0 * 25.4;

                // Find the closest matching paper size (within 2mm tolerance)
                var matchedSize = PaperSize.All
                    .FirstOrDefault(p =>
                        Math.Abs(p.WidthMm - widthMm) < 2 && Math.Abs(p.HeightMm - heightMm) < 2);

                if (matchedSize != null)
                {
                    _selectedPaperSize = matchedSize;
                    OnPropertyChanged(nameof(SelectedPaperSize));
                }
            }
        }
        catch
        {
            // Failed to get paper size from printer, keep current selection
        }
    }

    private void RefreshRecentFiles()
    {
        RecentFiles.Clear();
        foreach (var f in _settings.RecentFiles)
            RecentFiles.Add(f);
    }
}
