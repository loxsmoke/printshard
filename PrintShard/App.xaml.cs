using System.IO;
using System.Windows;
using PrintShard.Services;
using PrintShard.ViewModels;

namespace PrintShard;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var settings = SettingsService.Load();
        var vm = new MainViewModel(settings);
        var window = new MainWindow { DataContext = vm };
        MainWindow = window;
        window.Show();

        if (e.Args.Length > 0 && File.Exists(e.Args[0]))
            vm.LoadImageCommand.Execute(e.Args[0]);
    }
}
