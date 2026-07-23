using System.Windows;
using SystemCacheCleaner.Infrastructure;

namespace SystemCacheCleaner.Dialogs;

public partial class CleanupConfirmationDialog : Window
{
    public CleanupConfirmationDialog(
        IReadOnlyList<string> selectedCategoryNames,
        int totalFiles,
        long expectedFreeBytes)
    {
        InitializeComponent();

        TxtCategoryCount.Text = $"{selectedCategoryNames.Count} 类";
        TxtCategoryNames.Text = string.Join("、", selectedCategoryNames);
        TxtFileCount.Text = $"{totalFiles} 个文件";
        TxtExpectedSpace.Text = ByteSizeFormatter.Format(expectedFreeBytes);
    }

    private void OnConfirmClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
