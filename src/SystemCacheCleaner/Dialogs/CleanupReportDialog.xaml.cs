using System.Windows;
using System.Windows.Media;
using SystemCacheCleaner.Infrastructure;
using SystemCacheCleaner.Models;

namespace SystemCacheCleaner.Dialogs;

public partial class CleanupReportDialog : Window
{
    public CleanupReportDialog(CleanupSessionResult result)
    {
        InitializeComponent();

        if (result == null)
        {
            return;
        }

        TxtSuccessCount.Text = result.SuccessCount.ToString();
        TxtSkipCount.Text = result.SkipCount.ToString();
        TxtFailCount.Text = result.FailCount.ToString();
        TxtFreedBytes.Text = ByteSizeFormatter.Format(result.TotalDeletedBytes);
        TxtTimeRange.Text = $"开始：{result.StartTime:yyyy-MM-dd HH:mm:ss}    结束：{result.EndTime:yyyy-MM-dd HH:mm:ss}";

        // 状态判定：只有在没有失败、没有跳过且未被取消时显示“清理完成”；否则显示“部分完成”
        bool isFullSuccess = result.FailCount == 0 && result.SkipCount == 0 && !result.IsCancelled;

        if (isFullSuccess)
        {
            BannerBorder.Background = new SolidColorBrush(Color.FromRgb(236, 253, 245)); // #ECFDF5
            BannerBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(16, 185, 129)); // #10B981
            TxtBannerIcon.Text = "✅ ";
            TxtBannerTitle.Text = "清理完成！所选文件已全部安全清除。";
            TxtBannerTitle.Foreground = new SolidColorBrush(Color.FromRgb(4, 120, 87));
        }
        else
        {
            BannerBorder.Background = new SolidColorBrush(Color.FromRgb(254, 243, 199)); // #FEF3C7
            BannerBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(245, 158, 11)); // #F59E0B
            TxtBannerIcon.Text = "⚠️ ";
            TxtBannerTitle.Text = result.IsCancelled 
                ? "清理已被中途取消（部分文件已清除，部分未处理）。"
                : "清理完成（部分文件由于独占锁定或无权限已自动跳过）。";
            TxtBannerTitle.Foreground = new SolidColorBrush(Color.FromRgb(146, 64, 14));
        }

        // 呈现异常/日志记录
        LstLogs.ItemsSource = result.FileResults;
    }

    private void OnRescanClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
