using System.ComponentModel;
using System.Windows;
using SystemCacheCleaner.ViewModels;

namespace SystemCacheCleaner;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private bool _closeApproved;
    private bool _closeRequestPending;

    public MainWindow()
    {
        InitializeComponent();
    }

    protected override async void OnClosing(CancelEventArgs e)
    {
        if (_closeApproved
            || DataContext is not MainViewModel viewModel
            || (!viewModel.IsScanning && !viewModel.IsCleaning))
        {
            base.OnClosing(e);
            return;
        }

        e.Cancel = true;
        base.OnClosing(e);

        if (_closeRequestPending)
        {
            return;
        }

        _closeRequestPending = true;
        try
        {
            if (await viewModel.ConfirmWindowClosingAsync())
            {
                _closeApproved = true;
                Close();
            }
        }
        finally
        {
            _closeRequestPending = false;
        }
    }
}
