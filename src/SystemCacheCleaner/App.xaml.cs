using System.Windows;
using SystemCacheCleaner.ViewModels;

namespace SystemCacheCleaner;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        bool isDemoMode = false;
        foreach (string arg in e.Args)
        {
            if (arg.Equals("--demo", StringComparison.OrdinalIgnoreCase))
            {
                isDemoMode = true;
                break;
            }
        }

        MainViewModel viewModel = new MainViewModel(isDemoMode: isDemoMode);
        MainWindow mainWindow = new MainWindow
        {
            DataContext = viewModel
        };

        mainWindow.Show();
    }
}
