using System.Windows;

namespace PrintShard.Views;

public partial class AboutWindow : Window
{
    private static readonly string AppVersion =
        System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";

    public AboutWindow()
    {
        InitializeComponent();
        VersionText.Text = $"Version {AppVersion}";
    }
}
