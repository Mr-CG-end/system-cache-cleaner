using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using SystemCacheCleaner.Dialogs;
using SystemCacheCleaner.Infrastructure;
using SystemCacheCleaner.Models;
using SystemCacheCleaner.Services;

namespace SystemCacheCleaner.ViewModels;

public class MainViewModel : ObservableObject
{
    private readonly IDiskSpaceService _diskSpaceService;
    private readonly ICacheCatalog _cacheCatalog;
    private readonly ICacheScanService _scanService;
    private readonly ICleanupService _cleanupService;

    private OperationStatus _status = OperationStatus.Idle;
    private bool _isDemoMode;
    private SystemDiskInfo? _systemDiskInfo;
    private string _selectedSummaryText = "已选择 0 类，预计可释放 0 B";
    private string _statusDescription = "准备就绪，点击“开始扫描”检测可清理的系统缓存数据。";
    private CancellationTokenSource? _scanCts;
    private CancellationTokenSource? _cleanCts;
    private TaskCompletionSource<bool>? _scanCompletion;
    private TaskCompletionSource<bool>? _cleanCompletion;
    private bool _isClosingRequested;
    private bool _isScanCancelled;
    private int _discoveredFiles;
    private long _discoveredBytes;
    private CleanupSessionResult? _lastCleanupResult;
    private Func<IReadOnlyList<string>, int, long, bool?>? _confirmDialogHandler;
    private Func<CleanupSessionResult, bool?>? _reportDialogHandler;
    private Func<string, string, bool>? _closingConfirmHandler;

    public MainViewModel(
        IDiskSpaceService? diskSpaceService = null,
        ICacheCatalog? cacheCatalog = null,
        ICacheScanService? scanService = null,
        ICleanupService? cleanupService = null,
        bool isDemoMode = false)
    {
        _diskSpaceService = diskSpaceService ?? new DiskSpaceService();
        _isDemoMode = isDemoMode;
        _cacheCatalog = cacheCatalog ?? new CacheCatalog(_isDemoMode);
        _scanService = scanService ?? new CacheScanService();
        _cleanupService = cleanupService ?? new CleanupService();

        Categories = _cacheCatalog.GetCategories();
        CategoryResults = new ObservableCollection<CategoryScanResultViewModel>();

        StartScanCommand = new AsyncRelayCommand(ExecuteStartScanAsync, CanExecuteStartScan);
        CancelScanCommand = new RelayCommand(ExecuteCancelScan, CanExecuteCancelScan);
        CleanCommand = new AsyncRelayCommand(ExecuteCleanAsync, CanExecuteClean);
        RescanCommand = new AsyncRelayCommand(ExecuteStartScanAsync, CanExecuteRescan);
        ShowReportCommand = new RelayCommand(ExecuteShowReport, CanExecuteShowReport);
        RefreshDiskCommand = new RelayCommand(RefreshDiskInfo);

        RefreshDiskInfo();
    }

    public string WindowTitle => "系统缓存清理工具软件 V1.0";

    public IReadOnlyList<CacheCategoryDefinition> Categories { get; }

    public ObservableCollection<CategoryScanResultViewModel> CategoryResults { get; }

    public bool IsDemoMode
    {
        get => _isDemoMode;
        set => SetProperty(ref _isDemoMode, value);
    }

    public string DemoModeBannerText => "演示模式：当前数据不是真实系统缓存";

    public OperationStatus Status
    {
        get => _status;
        set
        {
            if (SetProperty(ref _status, value))
            {
                OnPropertyChanged(nameof(IsIdle));
                OnPropertyChanged(nameof(IsScanning));
                OnPropertyChanged(nameof(IsNotScanning));
                OnPropertyChanged(nameof(IsScanCompleted));
                OnPropertyChanged(nameof(IsCleaning));
                OnPropertyChanged(nameof(HasScanResults));
                OnPropertyChanged(nameof(HasReport));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public bool IsIdle => Status == OperationStatus.Idle;
    public bool IsScanning => Status == OperationStatus.Scanning;
    public bool IsNotScanning => !IsScanning;
    public bool IsScanCompleted => Status == OperationStatus.ScanCompleted;
    public bool IsCleaning => Status == OperationStatus.Cleaning;
    public bool HasScanResults => Status == OperationStatus.ScanCompleted || Status == OperationStatus.CleanupCompleted || Status == OperationStatus.PartialCompleted;
    public bool HasReport => LastCleanupResult != null;

    public bool IsScanCancelled
    {
        get => _isScanCancelled;
        private set => SetProperty(ref _isScanCancelled, value);
    }

    public SystemDiskInfo? SystemDiskInfo
    {
        get => _systemDiskInfo;
        set
        {
            if (SetProperty(ref _systemDiskInfo, value))
            {
                OnPropertyChanged(nameof(SystemDiskDisplayText));
                OnPropertyChanged(nameof(SystemDiskFormattedDetail));
            }
        }
    }

    public string SystemDiskDisplayText
    {
        get
        {
            if (SystemDiskInfo == null || !SystemDiskInfo.IsAvailable)
            {
                return SystemDiskInfo?.StatusMessage ?? "未能获取系统盘信息";
            }

            string totalFormatted = ByteSizeFormatter.Format(SystemDiskInfo.TotalBytes);
            string freeFormatted = ByteSizeFormatter.Format(SystemDiskInfo.FreeBytes);
            string usedFormatted = ByteSizeFormatter.Format(SystemDiskInfo.UsedBytes);

            return $"系统盘 ({SystemDiskInfo.DriveName}) - 已用 {usedFormatted} / 总计 {totalFormatted} (可用 {freeFormatted})";
        }
    }

    public string SystemDiskFormattedDetail
    {
        get
        {
            if (SystemDiskInfo == null || !SystemDiskInfo.IsAvailable)
            {
                return SystemDiskInfo?.StatusMessage ?? "读取系统盘信息失败";
            }

            string totalFormatted = ByteSizeFormatter.Format(SystemDiskInfo.TotalBytes);
            string freeFormatted = ByteSizeFormatter.Format(SystemDiskInfo.FreeBytes);
            return $"总空间: {totalFormatted}  |  可用空间: {freeFormatted} ({100 - SystemDiskInfo.UsedPercentage:F1}%)";
        }
    }

    public string SelectedSummaryText
    {
        get => _selectedSummaryText;
        set => SetProperty(ref _selectedSummaryText, value);
    }

    public string StatusDescription
    {
        get => _statusDescription;
        set => SetProperty(ref _statusDescription, value);
    }

    public int DiscoveredFiles
    {
        get => _discoveredFiles;
        set => SetProperty(ref _discoveredFiles, value);
    }

    public long DiscoveredBytes
    {
        get => _discoveredBytes;
        set => SetProperty(ref _discoveredBytes, value);
    }

    public CleanupSessionResult? LastCleanupResult
    {
        get => _lastCleanupResult;
        private set
        {
            if (SetProperty(ref _lastCleanupResult, value))
            {
                OnPropertyChanged(nameof(HasReport));
            }
        }
    }

    public void SetConfirmDialogHandler(Func<IReadOnlyList<string>, int, long, bool?> handler)
    {
        _confirmDialogHandler = handler;
    }

    public void SetReportDialogHandler(Func<CleanupSessionResult, bool?> handler)
    {
        _reportDialogHandler = handler;
    }

    public void SetClosingConfirmHandler(Func<string, string, bool> handler)
    {
        _closingConfirmHandler = handler;
    }

    public ICommand StartScanCommand { get; }
    public ICommand CancelScanCommand { get; }
    public ICommand CleanCommand { get; }
    public ICommand RescanCommand { get; }
    public ICommand ShowReportCommand { get; }
    public ICommand RefreshDiskCommand { get; }

    public void RefreshDiskInfo()
    {
        SystemDiskInfo = _diskSpaceService.GetSystemDiskInfo();
    }

    private bool CanExecuteStartScan()
    {
        return Status == OperationStatus.Idle || Status == OperationStatus.ScanCompleted || Status == OperationStatus.CleanupCompleted || Status == OperationStatus.PartialCompleted;
    }

    private async Task ExecuteStartScanAsync()
    {
        CancellationTokenSource scanCts = new CancellationTokenSource();
        TaskCompletionSource<bool> completion = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        _scanCts = scanCts;
        _scanCompletion = completion;

        Status = OperationStatus.Scanning;
        IsScanCancelled = false;
        StatusDescription = "正在扫描系统缓存文件...";
        DiscoveredFiles = 0;
        DiscoveredBytes = 0;

        foreach (CategoryScanResultViewModel vm in CategoryResults)
        {
            vm.SelectionChanged -= OnCategorySelectionChanged;
        }
        CategoryResults.Clear();
        UpdateSelectedSummary();

        Progress<ScanProgressReport> progress = new Progress<ScanProgressReport>(report =>
        {
            DiscoveredFiles = report.DiscoveredFiles;
            DiscoveredBytes = report.DiscoveredBytes;
            StatusDescription = $"正在扫描 [{report.CurrentCategoryName}]... 已发现 {report.DiscoveredFiles} 个文件 ({ByteSizeFormatter.Format(report.DiscoveredBytes)})";
        });

        try
        {
            ScanSessionResult sessionResult = await _scanService.ScanAsync(Categories, progress, scanCts.Token);

            IsScanCancelled = sessionResult.IsCancelled;

            foreach (CategoryScanResult catResult in sessionResult.CategoryResults)
            {
                CategoryScanResultViewModel vm = new CategoryScanResultViewModel(catResult);
                vm.SelectionChanged += OnCategorySelectionChanged;
                CategoryResults.Add(vm);
            }

            Status = OperationStatus.ScanCompleted;

            if (IsScanCancelled)
            {
                StatusDescription = "扫描已取消，结果可能不完整。请重新扫描以进行清理。";
            }
            else
            {
                StatusDescription = $"扫描完成，共找到 {CategoryResults.Sum(c => c.FileCount)} 个文件。";
            }

            UpdateSelectedSummary();
        }
        catch (OperationCanceledException)
        {
            IsScanCancelled = true;
            Status = OperationStatus.ScanCompleted;
            StatusDescription = "扫描已取消，结果可能不完整。请重新扫描以进行清理。";
            UpdateSelectedSummary();
        }
        catch (Exception ex)
        {
            Status = OperationStatus.Idle;
            StatusDescription = $"扫描过程发生异常: {ex.Message}";
        }
        finally
        {
            scanCts.Dispose();
            if (ReferenceEquals(_scanCts, scanCts))
            {
                _scanCts = null;
            }

            completion.TrySetResult(true);
            if (ReferenceEquals(_scanCompletion, completion))
            {
                _scanCompletion = null;
            }
        }
    }

    private bool CanExecuteCancelScan()
    {
        return Status == OperationStatus.Scanning;
    }

    private void ExecuteCancelScan()
    {
        if (Status == OperationStatus.Scanning && _scanCts != null && !_scanCts.IsCancellationRequested)
        {
            _scanCts.Cancel();
            StatusDescription = "正在取消扫描，请稍候...";
        }
    }

    private void OnCategorySelectionChanged(object? sender, EventArgs e)
    {
        UpdateSelectedSummary();
    }

    public void UpdateSelectedSummary()
    {
        var selectedList = CategoryResults.Where(c => c.IsSelected).ToList();
        int selectedCount = selectedList.Count;
        long expectedFreeBytes = selectedList.Sum(c => c.TotalBytes);

        SelectedSummaryText = $"已选择 {selectedCount} 类，预计可释放 {ByteSizeFormatter.Format(expectedFreeBytes)}";
        CommandManager.InvalidateRequerySuggested();
    }

    private bool CanExecuteClean()
    {
        if (Status != OperationStatus.ScanCompleted || IsScanCancelled)
        {
            return false;
        }

        var selectedList = CategoryResults.Where(c => c.IsSelected).ToList();
        long expectedFreeBytes = selectedList.Sum(c => c.TotalBytes);

        return selectedList.Count > 0 && expectedFreeBytes > 0;
    }

    private async Task ExecuteCleanAsync()
    {
        if (!CanExecuteClean())
        {
            return;
        }

        var selectedCategoryVMs = CategoryResults.Where(c => c.IsSelected).ToList();
        List<string> selectedCatNames = selectedCategoryVMs.Select(vm => vm.DisplayName).ToList();
        int selectedCatCount = selectedCategoryVMs.Count;
        int totalFiles = selectedCategoryVMs.Sum(c => c.FileCount);
        long expectedFreeBytes = selectedCategoryVMs.Sum(c => c.TotalBytes);

        // 二次确认弹窗处理
        bool confirmed = false;
        if (_confirmDialogHandler != null)
        {
            confirmed = _confirmDialogHandler(selectedCatNames, totalFiles, expectedFreeBytes) == true;
        }
        else
        {
            CleanupConfirmationDialog dialog = new CleanupConfirmationDialog(selectedCatNames, totalFiles, expectedFreeBytes);
            if (Application.Current != null && Application.Current.MainWindow != null)
            {
                dialog.Owner = Application.Current.MainWindow;
            }
            confirmed = dialog.ShowDialog() == true;
        }

        if (!confirmed)
        {
            return;
        }

        CancellationTokenSource cleanCts = new CancellationTokenSource();
        TaskCompletionSource<bool> completion = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        _cleanCts = cleanCts;
        _cleanCompletion = completion;

        Status = OperationStatus.Cleaning;
        StatusDescription = "正在执行清理...";

        Progress<CleanupProgressReport> progress = new Progress<CleanupProgressReport>(report =>
        {
            StatusDescription = $"正在清理... 已处理 {report.ProcessedFiles}/{report.TotalFiles} 个文件 (已释放 {ByteSizeFormatter.Format(report.TotalDeletedBytes)})";
        });

        try
        {
            List<CategoryScanResult> selectedModels = selectedCategoryVMs.Select(vm => vm.Model with { IsSelected = vm.IsSelected }).ToList();
            CleanupSessionResult sessionResult = await _cleanupService.CleanupAsync(selectedModels, progress, cleanCts.Token);
            LastCleanupResult = sessionResult;

            if (sessionResult.FailCount == 0 && sessionResult.SkipCount == 0 && !sessionResult.IsCancelled)
            {
                Status = OperationStatus.CleanupCompleted;
                StatusDescription = $"清理完成！成功删除 {sessionResult.SuccessCount} 个文件，释放 {ByteSizeFormatter.Format(sessionResult.TotalDeletedBytes)} 空间。";
            }
            else
            {
                Status = OperationStatus.PartialCompleted;
                StatusDescription = $"清理完成（部分跳过/失败）：成功 {sessionResult.SuccessCount} 项，跳过 {sessionResult.SkipCount} 项，失败 {sessionResult.FailCount} 项，共释放 {ByteSizeFormatter.Format(sessionResult.TotalDeletedBytes)}。";
            }

            RefreshDiskInfo();

            // 清理完成后弹窗显示报告 (AC-M06-01)
            bool? rescanRequested = _isClosingRequested ? false : ShowReportDialog(sessionResult);

            // 若在报告弹窗中点击了“重新扫描”，立刻触发一轮全新扫描 (AC-M06-05, AC-M06-06)
            if (rescanRequested == true)
            {
                await ExecuteStartScanAsync();
            }
        }
        catch (Exception ex)
        {
            Status = OperationStatus.PartialCompleted;
            StatusDescription = $"清理过程发生异常: {ex.Message}";
        }
        finally
        {
            cleanCts.Dispose();
            if (ReferenceEquals(_cleanCts, cleanCts))
            {
                _cleanCts = null;
            }

            completion.TrySetResult(true);
            if (ReferenceEquals(_cleanCompletion, completion))
            {
                _cleanCompletion = null;
            }
        }
    }

    private bool? ShowReportDialog(CleanupSessionResult sessionResult)
    {
        if (_reportDialogHandler != null)
        {
            return _reportDialogHandler(sessionResult);
        }

        try
        {
            if (Application.Current != null)
            {
                CleanupReportDialog reportDialog = new CleanupReportDialog(sessionResult);
                if (Application.Current.MainWindow != null)
                {
                    reportDialog.Owner = Application.Current.MainWindow;
                }
                return reportDialog.ShowDialog();
            }
        }
        catch
        {
            // 测试环境或无 UI 线程环境忽略 GUI 错误
        }

        return false;
    }

    private bool CanExecuteShowReport()
    {
        return LastCleanupResult != null
            && Status != OperationStatus.Scanning
            && Status != OperationStatus.Cleaning;
    }

    private void ExecuteShowReport()
    {
        if (LastCleanupResult == null)
        {
            return;
        }

        bool? rescanRequested = ShowReportDialog(LastCleanupResult);
        if (rescanRequested == true && RescanCommand.CanExecute(null))
        {
            RescanCommand.Execute(null);
        }
    }

    private bool CanExecuteRescan()
    {
        return Status == OperationStatus.ScanCompleted || Status == OperationStatus.CleanupCompleted || Status == OperationStatus.PartialCompleted;
    }

    /// <summary>
    /// 处理主窗口关闭前的二次关怀确认逻辑 (AC-M07-01, AC-M07-02)
    /// </summary>
    public async Task<bool> ConfirmWindowClosingAsync()
    {
        if (Status == OperationStatus.Scanning)
        {
            string message = "当前正在进行系统缓存扫描，确定要取消扫描并退出程序吗？";
            string title = "确认退出扫描";

            bool confirm = ConfirmAction(message, title);
            if (confirm)
            {
                _isClosingRequested = true;
                Task completion = _scanCompletion?.Task ?? Task.CompletedTask;
                _scanCts?.Cancel();
                await completion;
                return true;
            }
            return false;
        }

        if (Status == OperationStatus.Cleaning)
        {
            string message = "当前正在进行缓存清理。停止后已删除的文件无法恢复，确定要中断清理并退出程序吗？";
            string title = "警告：确认中断清理";

            bool confirm = ConfirmAction(message, title);
            if (confirm)
            {
                _isClosingRequested = true;
                Task completion = _cleanCompletion?.Task ?? Task.CompletedTask;
                _cleanCts?.Cancel();
                await completion;
                return true;
            }
            return false;
        }

        return true;
    }

    private bool ConfirmAction(string message, string title)
    {
        if (_closingConfirmHandler != null)
        {
            return _closingConfirmHandler(message, title);
        }

        MessageBoxResult result = MessageBox.Show(
            message,
            title,
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning
        );

        return result == MessageBoxResult.Yes;
    }
}
