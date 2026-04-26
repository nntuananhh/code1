using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WpfApp3.Models;
using WpfApp3.Services;
using WpfApp3.Dialogs;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.WPF;
using SkiaSharp;

namespace WpfApp3.Pages
{
    public partial class BudgetPage : Page, INotifyPropertyChanged
    {
        private readonly IBudgetService _budgetService;
        private readonly ICategoryService _categoryService;
        private readonly ISessionContext _sessionContext;
        private List<Budget> _allBudgets = new List<Budget>();
        private string _currentFilter = "Active";
        private ISeries[] _budgetProgressSeries = Array.Empty<ISeries>();
        private string[] _categoryLabels = Array.Empty<string>();
        private LiveChartsCore.Kernel.Sketches.ICartesianAxis[] _xAxes = Array.Empty<LiveChartsCore.Kernel.Sketches.ICartesianAxis>();
        private LiveChartsCore.Kernel.Sketches.ICartesianAxis[] _yAxes = Array.Empty<LiveChartsCore.Kernel.Sketches.ICartesianAxis>();

        public event PropertyChangedEventHandler? PropertyChanged;

        public ISeries[] BudgetProgressSeries
        {
            get => _budgetProgressSeries;
            set
            {
                _budgetProgressSeries = value;
                OnPropertyChanged();
            }
        }

        public string[] CategoryLabels
        {
            get => _categoryLabels;
            set
            {
                _categoryLabels = value;
                OnPropertyChanged();
            }
        }

        public LiveChartsCore.Kernel.Sketches.ICartesianAxis[] XAxes
        {
            get => _xAxes;
            set
            {
                _xAxes = value;
                OnPropertyChanged();
            }
        }

        public LiveChartsCore.Kernel.Sketches.ICartesianAxis[] YAxes
        {
            get => _yAxes;
            set
            {
                _yAxes = value;
                OnPropertyChanged();
            }
        }

        public Func<double, string> YFormatter { get; set; }
        private readonly Func<EditBudgetDialog> _editBudgetDialogFactory;
        private readonly Func<AddBudgetDialog> _addBudgetDialogFactory;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public BudgetPage(IBudgetService budgetService, ICategoryService categoryService, ISessionContext sessionContext,
            Func<EditBudgetDialog> editBudgetDialogFactory,
            Func<AddBudgetDialog> addBudgetDialogFactory)
        {
            InitializeComponent();
            _budgetService = budgetService;
            _categoryService = categoryService;
            _sessionContext = sessionContext;
            _editBudgetDialogFactory = editBudgetDialogFactory;
            _addBudgetDialogFactory = addBudgetDialogFactory;
            
            YFormatter = value => $"{value:N0} ₫";
            DataContext = this;
            
            LoadData();
        }

        public async void LoadData()
        {
            try
            {
                _allBudgets = await _budgetService.GetBudgetsAsync(_sessionContext.CurrentUserId ?? 0);
                UpdateSummaryCards();
                ApplyFilter(_currentFilter);
                await UpdateBudgetProgressChart();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi tải dữ liệu: {ex.Message}", "Lỗi", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateSummaryCards()
        {
            if (_allBudgets == null || !_allBudgets.Any())
            {
                TotalBudgetText.Text = "₫0";
                TotalSpentText.Text = "₫0";
                RemainingBudgetText.Text = "₫0";
                return;
            }

            var totalBudget = _allBudgets.Sum(b => b.Amount);
            var totalSpent = _allBudgets.Sum(b => b.SpentAmount);
            var remainingBudget = totalBudget - totalSpent;

            TotalBudgetText.Text = $"{totalBudget:N0} ₫";
            TotalSpentText.Text = $"{totalSpent:N0} ₫";
            RemainingBudgetText.Text = $"{remainingBudget:N0} ₫";
            RemainingBudgetText.Foreground = remainingBudget >= 0 ? Brushes.Green : Brushes.Red;
            CheckBudgetAlerts();
        }

        private void ApplyFilter(string filter)
        {
            if (_allBudgets == null) return;
            List<Budget> filteredBudgets;

            switch (filter)
            {
                case "Active":
                    // Chỉ hiển thị các hũ chi tiêu còn tiền (chưa hết)
                    filteredBudgets = _allBudgets.Where(b => b.SpentAmount < b.Amount).ToList();
                    break;
                case "All":
                default:
                    filteredBudgets = _allBudgets.ToList();
                    break;
            }

            BudgetListView.ItemsSource = filteredBudgets.Select(b => new BudgetViewModel
            {
                Id = b.Id,
                Category = b.Category!,
                Amount = b.Amount,
                SpentAmount = b.SpentAmount,
                RemainingAmount = b.Amount - b.SpentAmount,
                ProgressPercentage = b.Amount > 0 ? (double)(b.SpentAmount / b.Amount * 100) : 0,
                StatusText = b.SpentAmount >= b.Amount ? "Đã hết" : "Đang hoạt động",
                StatusColor = b.SpentAmount >= b.Amount ? Brushes.Red : Brushes.Green,
                ProgressColor = b.SpentAmount >= b.Amount ? Brushes.Red :
                               (b.SpentAmount / b.Amount) > 0.8m ? Brushes.Orange : Brushes.Green
            }).ToList();
        }

        private static DateTime _lastAlertTime = DateTime.MinValue;
        private static readonly TimeSpan AlertCooldown = TimeSpan.FromMinutes(5); // 5 phút

        private async void CheckBudgetAlerts()
        {
            if (_allBudgets == null || !_allBudgets.Any())
                return;
            if (DateTime.Now - _lastAlertTime < AlertCooldown)
                return;

            try
            {
                var alertResult = await _budgetService.CheckBudgetAlertsAsync(_sessionContext.CurrentUserId ?? 0);

                if (alertResult.HasAlerts)
                {
                    var message = string.Empty;
                    
                    if (alertResult.OverBudgetItems.Any())
                    {
                        message = "CẢNH BÁO: Các hũ chi tiêu sau đã vượt quá:\n\n" + 
                                 string.Join("\n", alertResult.OverBudgetItems);
                    }
                    else if (alertResult.WarningItems.Any())
                    {
                        message = "⚠️ CẢNH BÁO: Các hũ chi tiêu sau sắp vượt quá:\n\n" + 
                                 string.Join("\n", alertResult.WarningItems);
                    }

                    if (!string.IsNullOrEmpty(message))
                    {
                        MessageBox.Show(message, "Cảnh báo hũ chi tiêu", 
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        
                        _lastAlertTime = DateTime.Now;
                    }
                }
            }
            catch
            {
            }
        }

        private Task UpdateBudgetProgressChart()
        {
            if (_allBudgets == null || !_allBudgets.Any())
            {
                BudgetProgressSeries = Array.Empty<ISeries>();
                CategoryLabels = Array.Empty<string>();
                return Task.CompletedTask;
            }

            var activeBudgets = _allBudgets.Where(b => b.IsActive).ToList();
            if (!activeBudgets.Any())
            {
                BudgetProgressSeries = Array.Empty<ISeries>();
                CategoryLabels = Array.Empty<string>();
                return Task.CompletedTask;
            }

            CategoryLabels = activeBudgets.Select(b => b.Category?.Name ?? "Không có danh mục").ToArray();

            // Cấu hình XAxes để hiển thị labels
            XAxes = new LiveChartsCore.Kernel.Sketches.ICartesianAxis[]
            {
                new Axis
                {
                    Name = "Danh mục",
                    Labels = CategoryLabels,
                    LabelsRotation = 0
                }
            };

            // Cấu hình YAxes
            YAxes = new LiveChartsCore.Kernel.Sketches.ICartesianAxis[]
            {
                new Axis
                {
                    Name = "Số tiền (₫)",
                    Labeler = YFormatter,
                    MinLimit = 0
                }
            };

            var series = new List<ISeries>
            {
                new ColumnSeries<double>
                {
                    Name = "Ngân sách",
                    Values = activeBudgets.Select(b => (double)b.Amount).ToArray(),
                    Fill = new SolidColorPaint(SKColor.Parse("#2196F3"))
                },
                new ColumnSeries<double>
                {
                    Name = "Đã chi",
                    Values = activeBudgets.Select(b => (double)b.SpentAmount).ToArray(),
                    Fill = new SolidColorPaint(SKColor.Parse("#F44336"))
                }
            };

            BudgetProgressSeries = series.ToArray();
            
            return Task.CompletedTask;
        }

        private async void EditButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is int budgetId)
            {
                try
                {
                    var budget = await _budgetService.GetBudgetByIdAsync(budgetId, _sessionContext.CurrentUserId ?? 0);
                    
                    if (budget != null)
                    {
                        var dialog = _editBudgetDialogFactory();
                        dialog.Initialize(budget);
                        dialog.Owner = Window.GetWindow(this);
                        
                        if (dialog.ShowDialog() == true)
                        {
                            LoadData();
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi khi tải ngân sách: {ex.Message}", "Lỗi", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is int budgetId)
            {
                try
                {
                    var budget = await _budgetService.GetBudgetByIdAsync(budgetId, _sessionContext.CurrentUserId ?? 0);
                    
                    if (budget != null)
                    {
                        // Tính SpentAmount thực tế từ các transactions
                        var actualSpentAmount = await _budgetService.CalculateActualSpentAmountAsync(budgetId);
                        var remainingAmount = budget.Amount - actualSpentAmount;
                        
                        var message = $"Bạn có chắc chắn muốn xóa hũ chi tiêu '{budget.Category?.Name}'?\n\n";
                        
                        if (remainingAmount > 0)
                        {
                            message += $"Số tiền còn lại: {remainingAmount:N0} ₫\n";
                            message += $"Số tiền này sẽ được hoàn trả về tổng số dư của bạn.";
                        }
                        else
                        {
                            message += $"Hũ chi tiêu này đã sử dụng hết số tiền.";
                        }
                        
                        var result = MessageBox.Show(message, "Xác nhận xóa hũ chi tiêu", 
                            MessageBoxButton.YesNo, MessageBoxImage.Question);
                        
                        if (result == MessageBoxResult.Yes)
                        {
                            var (success, messageResult) = await _budgetService.DeleteBudgetAsync(budgetId, _sessionContext.CurrentUserId ?? 0);
                            
                            if (success)
                            {
                                await Task.Delay(100);
                                LoadData();
                                
                                var mainWindow = Application.Current.MainWindow as MainWindow;
                                if (mainWindow != null)
                                {
                                    mainWindow.UpdateSidebarData();
                                    var currentPage = mainWindow.MainFrame.Content as Page;
                                    if (currentPage is DashboardPage dashboardPage)
                                    {
                                        await Task.Delay(50);
                                        dashboardPage.RefreshData();
                                    }
                                }
                                
                                MessageBox.Show(messageResult, "Thành công", 
                                    MessageBoxButton.OK, MessageBoxImage.Information);
                            }
                            else
                            {
                                MessageBox.Show(messageResult, "Lỗi", 
                                    MessageBoxButton.OK, MessageBoxImage.Error);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi khi xóa hũ chi tiêu: {ex.Message}", 
                        "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BudgetListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private async void AddBudgetButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = _addBudgetDialogFactory();
                if (dialog.ShowDialog() == true)
                {
                    // Refresh data after adding
                    LoadData();
                    
                    // Refresh dashboard if open
                    var mainWindow = Application.Current.MainWindow as MainWindow;
                    if (mainWindow != null)
                    {
                        mainWindow.UpdateSidebarData();
                        
                        var currentPage = mainWindow.MainFrame.Content as Page;
                        if (currentPage is DashboardPage dashboardPage)
                        {
                            await Task.Delay(50);
                            dashboardPage.RefreshData();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi thêm hũ chi tiêu: {ex.Message}", 
                    "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void FilterButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                _currentFilter = button.Tag?.ToString() ?? "Active";
                ActiveButton.Style = (Style)FindResource("MaterialDesignFlatButton");
                ActiveButton.Background = Brushes.Transparent;
                ActiveButton.Foreground = (SolidColorBrush)FindResource("MaterialDesignBody");
                
                AllButton.Style = (Style)FindResource("MaterialDesignFlatButton");
                AllButton.Background = Brushes.Transparent;
                AllButton.Foreground = (SolidColorBrush)FindResource("MaterialDesignBody");
                if (_currentFilter == "Active")
                {
                    ActiveButton.Style = (Style)FindResource("MaterialDesignRaisedButton");
                    ActiveButton.Background = new SolidColorBrush(Color.FromRgb(33, 150, 243)); // Blue color
                    ActiveButton.Foreground = Brushes.White;
                }
                else if (_currentFilter == "All")
                {
                    AllButton.Style = (Style)FindResource("MaterialDesignRaisedButton");
                    AllButton.Background = new SolidColorBrush(Color.FromRgb(33, 150, 243)); // Blue color
                    AllButton.Foreground = Brushes.White;
                }
                if (_currentFilter == "Advanced")
                {
                    AdvancedFilterPanel.Visibility = Visibility.Visible;
                    LoadCategoriesForFilter();
                }
                else
                {
                    AdvancedFilterPanel.Visibility = Visibility.Collapsed;
                }
                LoadData();
            }
        }

        private async void LoadCategoriesForFilter()
        {
            try
            {
                var categories = await _categoryService.GetCategoriesAsync(_sessionContext.CurrentUserId ?? 0, TransactionType.Expense);
                
                // Add "Tất cả" option
                var allCategories = new List<Category> { new Category { Id = 0, Name = "Tất cả" } };
                allCategories.AddRange(categories);
                CategoryFilterComboBox.ItemsSource = allCategories;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi tải danh mục: {ex.Message}", "Lỗi", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ApplyAdvancedFilter_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedCategoryId = CategoryFilterComboBox.SelectedValue as int? ?? 0;
                var minAmount = decimal.TryParse(MinAmountTextBox.Text, out var min) ? min : 0;
                var maxAmount = decimal.TryParse(MaxAmountTextBox.Text, out var max) ? max : decimal.MaxValue;
                var statusFilter = (StatusFilterComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "All";
                ApplyAdvancedFilter(selectedCategoryId, minAmount, maxAmount, statusFilter);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi áp dụng bộ lọc: {ex.Message}", "Lỗi", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ClearAdvancedFilter_Click(object sender, RoutedEventArgs e)
        {
            // Reset filter controls
            CategoryFilterComboBox.SelectedIndex = 0;
            MinAmountTextBox.Text = "0";
            MaxAmountTextBox.Text = "";
            StatusFilterComboBox.SelectedIndex = 0;

            ApplyAdvancedFilter(0, 0, decimal.MaxValue, "All");
        }

        private void ApplyAdvancedFilter(int categoryId, decimal minAmount, decimal maxAmount, string statusFilter)
        {
            if (_allBudgets == null) return;

            var filteredBudgets = _allBudgets.AsEnumerable();
            if (categoryId > 0)
            {
                filteredBudgets = filteredBudgets.Where(b => b.CategoryId == categoryId);
            }
            filteredBudgets = filteredBudgets.Where(b => b.Amount >= minAmount && b.Amount <= maxAmount);
            switch (statusFilter)
            {
                case "Active":
                    filteredBudgets = filteredBudgets.Where(b => b.SpentAmount < b.Amount);
                    break;
                case "Exhausted":
                    filteredBudgets = filteredBudgets.Where(b => b.SpentAmount >= b.Amount);
                    break;
                case "NearExhausted":
                    filteredBudgets = filteredBudgets.Where(b => b.Amount > 0 && (b.SpentAmount / b.Amount) > 0.8m && b.SpentAmount < b.Amount);
                    break;
                case "All":
                default:
                    break;
            }

            BudgetListView.ItemsSource = filteredBudgets.Select(b => new BudgetViewModel
            {
                Id = b.Id,
                Category = b.Category!,
                Amount = b.Amount,
                SpentAmount = b.SpentAmount,
                RemainingAmount = b.Amount - b.SpentAmount,
                ProgressPercentage = b.Amount > 0 ? (double)(b.SpentAmount / b.Amount * 100) : 0,
                StatusText = b.SpentAmount >= b.Amount ? "Đã hết" : "Đang hoạt động",
                StatusColor = b.SpentAmount >= b.Amount ? Brushes.Red : Brushes.Green,
                ProgressColor = b.SpentAmount >= b.Amount ? Brushes.Red : 
                               (b.SpentAmount / b.Amount) > 0.8m ? Brushes.Orange : Brushes.Green
            }).ToList();
        }
    }

    public class BudgetViewModel
    {
        public int Id { get; set; }
        public Category Category { get; set; } = null!;
        public decimal Amount { get; set; }
        public decimal SpentAmount { get; set; }
        public decimal RemainingAmount { get; set; }
        public double ProgressPercentage { get; set; }
        public string StatusText { get; set; } = string.Empty;
        public Brush StatusColor { get; set; } = Brushes.Transparent;
        public Brush ProgressColor { get; set; } = Brushes.Transparent;
    }
}
