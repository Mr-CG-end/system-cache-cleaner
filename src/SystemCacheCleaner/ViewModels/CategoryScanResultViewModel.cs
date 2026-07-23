using SystemCacheCleaner.Infrastructure;
using SystemCacheCleaner.Models;

namespace SystemCacheCleaner.ViewModels;

public class CategoryScanResultViewModel : ObservableObject
{
    private bool _isSelected;

    public CategoryScanResultViewModel(CategoryScanResult model)
    {
        Model = model ?? throw new ArgumentNullException(nameof(model));
        _isSelected = model.IsSelected;
    }

    public CategoryScanResult Model { get; }

    public string CategoryId => Model.Category.CategoryId;
    public string DisplayName => Model.Category.DisplayName;
    public string Description => Model.Category.Description;
    public string RiskLevel => Model.Category.RiskLevel;

    public int FileCount => Model.FileCount;
    public long TotalBytes => Model.TotalBytes;
    public string FormattedTotalSize => ByteSizeFormatter.Format(TotalBytes);
    public string StatusMessage => Model.StatusMessage;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value))
            {
                OnPropertyChanged(nameof(FormattedSelectionSummary));
                SelectionChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public string FormattedSelectionSummary => $"{DisplayName}: {(IsSelected ? "已选中" : "未选中")} ({FormattedTotalSize})";

    public event EventHandler? SelectionChanged;
}
