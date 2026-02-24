using System.ComponentModel;
using System.Windows;
using System.Windows.Media.Imaging;
using PrintShard.ViewModels;

namespace PrintShard.Views;

public partial class PrintPreviewWindow : Window
{
    public PrintPreviewWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is PrintPreviewViewModel old)
        {
            old.RequestClose -= CloseWindow;
            old.PropertyChanged -= OnViewModelPropertyChanged;
        }
        if (e.NewValue is PrintPreviewViewModel vm)
        {
            vm.RequestClose += CloseWindow;
            vm.PropertyChanged += OnViewModelPropertyChanged;
            // Initial sizing when view model is set
            AdjustWindowSizeToContent(vm.CurrentPageBitmap);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PrintPreviewViewModel.CurrentPageBitmap) &&
            sender is PrintPreviewViewModel vm)
        {
            AdjustWindowSizeToContent(vm.CurrentPageBitmap);
        }
    }

    private void AdjustWindowSizeToContent(BitmapSource? bitmap)
    {
        if (bitmap == null) return;

        // Get the screen work area (excludes taskbar)
        var screen = SystemParameters.WorkArea;

        // Calculate desired window size based on image dimensions
        // Add padding for window chrome, margins (20px each side), and navigation bar (~50px)
        const double horizontalPadding = 60;  // Window chrome + margins
        const double verticalPadding = 120;   // Window chrome + margins + nav bar

        double desiredWidth = bitmap.PixelWidth + horizontalPadding;
        double desiredHeight = bitmap.PixelHeight + verticalPadding;

        // Clamp to screen boundaries with some margin (20px from edges)
        const double screenMargin = 20;
        double maxWidth = screen.Width - (screenMargin * 2);
        double maxHeight = screen.Height - (screenMargin * 2);

        // Also respect minimum size constraints
        double newWidth = Math.Clamp(desiredWidth, MinWidth, maxWidth);
        double newHeight = Math.Clamp(desiredHeight, MinHeight, maxHeight);

        // Only resize if the new size is larger than current size
        if (newWidth > Width || newHeight > Height)
        {
            Width = Math.Max(Width, newWidth);
            Height = Math.Max(Height, newHeight);

            // Re-center the window on its owner or screen
            if (Owner != null)
            {
                Left = Owner.Left + (Owner.Width - Width) / 2;
                Top = Owner.Top + (Owner.Height - Height) / 2;
            }
            else
            {
                Left = screen.Left + (screen.Width - Width) / 2;
                Top = screen.Top + (screen.Height - Height) / 2;
            }

            // Ensure window stays within screen bounds
            Left = Math.Max(screen.Left + screenMargin, Math.Min(Left, screen.Right - Width - screenMargin));
            Top = Math.Max(screen.Top + screenMargin, Math.Min(Top, screen.Bottom - Height - screenMargin));
        }
    }

    private void CloseWindow() => Close();
}
