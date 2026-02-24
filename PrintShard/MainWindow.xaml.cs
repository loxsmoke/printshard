using System.Windows;
using PrintShard.ViewModels;

namespace PrintShard;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Wire ViewModel zoom actions to canvas methods
        var vm = (MainViewModel)DataContext;
        vm.ZoomInAction      = () => PreviewCanvas.ZoomIn();
        vm.ZoomOutAction     = () => PreviewCanvas.ZoomOut();
        vm.FitToWindowAction = () => PreviewCanvas.FitToWindow();
    }

    // ── File drop ─────────────────────────────────────────────────────────────

    private void PreviewCanvas_FileDrop(object sender, string filePath)
        => ((MainViewModel)DataContext).DropImageCommand.Execute(filePath);
}
