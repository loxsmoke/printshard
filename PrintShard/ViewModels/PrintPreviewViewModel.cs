using System.Windows.Input;
using System.Windows.Media.Imaging;
using PrintShard.Models;
using PrintShard.Services;

namespace PrintShard.ViewModels;

public sealed class PrintPreviewViewModel : ViewModelBase
{
    private readonly BitmapSource _image;
    private readonly TileLayout   _layout;
    private readonly AppSettings  _settings;
    private readonly string?      _selectedPrinterName;
    private readonly string?      _selectedPaperSizeName;
    private int _currentPageIndex;

    public event Action? RequestClose;

    public PrintPreviewViewModel(BitmapSource image, TileLayout layout, AppSettings settings,
        string? selectedPrinterName = null, string? selectedPaperSizeName = null)
    {
        _image    = image;
        _layout   = layout;
        _settings = settings;
        _selectedPrinterName   = selectedPrinterName;
        _selectedPaperSizeName = selectedPaperSizeName;

        PreviousPageCommand = new RelayCommand(PreviousPage, () => _currentPageIndex > 0);
        NextPageCommand     = new RelayCommand(NextPage,     () => _currentPageIndex < TotalPages - 1);
        PrintCommand        = new RelayCommand(ExecutePrint);
        CloseCommand        = new RelayCommand(() => RequestClose?.Invoke());

        RenderCurrentPage();
    }

    // ── Properties ───────────────────────────────────────────────────────────

    public int TotalPages => _layout.Tiles.Count;

    public int CurrentPageIndex
    {
        get => _currentPageIndex;
        private set
        {
            if (SetField(ref _currentPageIndex, value))
            {
                OnPropertyChanged(nameof(PageLabel));
                RenderCurrentPage();
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public string PageLabel => $"Page {_currentPageIndex + 1} of {TotalPages}";

    private BitmapSource? _currentPageBitmap;
    public BitmapSource? CurrentPageBitmap
    {
        get => _currentPageBitmap;
        private set => SetField(ref _currentPageBitmap, value);
    }

    // ── Commands ─────────────────────────────────────────────────────────────

    public ICommand PreviousPageCommand { get; }
    public ICommand NextPageCommand     { get; }
    public ICommand PrintCommand        { get; }
    public ICommand CloseCommand        { get; }

    private void PreviousPage() => CurrentPageIndex--;
    private void NextPage()     => CurrentPageIndex++;

    private void ExecutePrint()
    {
        try
        {
            PrintService.Print(_image, _layout, _settings, _selectedPrinterName, _selectedPaperSizeName);
            RequestClose?.Invoke();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Print failed:\n{ex.Message}", "Print Error",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    private void RenderCurrentPage()
    {
        try
        {
            CurrentPageBitmap = PrintService.RenderPagePreview(
                _image, _layout, _settings, _currentPageIndex, previewWidthPx: 700);
        }
        catch
        {
            CurrentPageBitmap = null;
        }
    }
}
