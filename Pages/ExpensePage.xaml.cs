using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WpfApp3.Models;
using WpfApp3.Services;
using WpfApp3.Dialogs;

namespace WpfApp3.Pages
{
    public partial class ExpensePage : Page
    {
        private readonly ITransactionService _transactionService;
        private readonly ICategoryService _categoryService;
        private readonly ISessionContext _sessionContext;
        private readonly IDataService _dataService;
        private List<Transaction> _allExpenses = new List<Transaction>();
        private string _currentFilter = "ThisMonth";
        private string _searchText = string.Empty;
        private readonly Func<EditTransactionDialog> _editTransactionDialogFactory;
        private readonly Func<AddTransactionDialog> _addTransactionDialogFactory;

        public ExpensePage(ITransactionService transactionService, ICategoryService categoryService, ISessionContext sessionContext,
            IDataService dataService,
            Func<EditTransactionDialog> editTransactionDialogFactory,
            Func<AddTransactionDialog> addTransactionDialogFactory)
        {
            InitializeComponent();            
            _transactionService = transactionService;
            _categoryService = categoryService;
            _sessionContext = sessionContext;
            _dataService = dataService;
            _editTransactionDialogFactory = editTransactionDialogFactory;
            _addTransactionDialogFactory = addTransactionDialogFactory;
            LoadData();
        }

        public async void LoadData()
        {
            try
            {
                _allExpenses = await _transactionService.GetTransactionsAsync(_sessionContext.CurrentUserId ?? 0, TransactionType.Expense);
                UpdateSummaryCards();
                ApplyFilter(_currentFilter);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi tải dữ liệu: {ex.Message}", "Lỗi", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void UpdateSummaryCards()
        {
            var userId = _sessionContext.CurrentUserId ?? 0;
            var totalExpense = await _dataService.GetTotalExpenseAsync(userId);
            TotalExpenseText.Text = $"{totalExpense:N0} ₫";

            var monthlyExpense = await _dataService.GetMonthlyExpenseAsync(userId);
            MonthlyExpenseText.Text = $"{monthlyExpense:N0} ₫";
            
            // trung bình chi tiêu tháng
            var averageExpense = await _dataService.GetAverageMonthlyExpenseAsync(userId);
            AverageExpenseText.Text = $"{averageExpense:N0} ₫";
        }

        private void ApplyFilter(string filter)
        {
            if (_allExpenses == null) return;

            var now = DateTime.Now;
            var filteredExpenses = filter switch
            {
                "ThisMonth" => _allExpenses.Where(t => t.Date.Month == now.Month && t.Date.Year == now.Year),
                "ThreeMonths" => _allExpenses.Where(t => t.Date >= now.AddMonths(-3)),
                "ThisYear" => _allExpenses.Where(t => t.Date.Year == now.Year),
                _ => _allExpenses.Where(t => t.Date.Month == now.Month && t.Date.Year == now.Year) // Default to ThisMonth
            };

            if (!string.IsNullOrWhiteSpace(_searchText))
            {
                filteredExpenses = filteredExpenses.Where(t => 
                    t.Description.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ||
                    (t.Category?.Name.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ?? false));
            }

            ExpenseListView.ItemsSource = filteredExpenses.OrderByDescending(t => t.Date).ToList();
        }

        private void FilterButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                ThisYearButton.Style = (Style)FindResource("MaterialDesignFlatButton");
                ThisYearButton.Background = Brushes.Transparent;
                ThisYearButton.Foreground = (SolidColorBrush)FindResource("MaterialDesignBody");

                button.Style = (Style)FindResource("MaterialDesignRaisedButton");
                button.Background = new SolidColorBrush(Color.FromRgb(33, 150, 243)); // Màu xanh
                button.Foreground = Brushes.White;

                // Cập nhật filter
                _currentFilter = button.Tag?.ToString() ?? "ThisMonth";
                ApplyFilter(_currentFilter);
            }
        }


        private async void EditButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is int transactionId)
            {
                try
                {
                    var transaction = await _transactionService.GetTransactionByIdAsync(transactionId, _sessionContext.CurrentUserId ?? 0);
                    
                    if (transaction != null)
                    {
                        var dialog = _editTransactionDialogFactory();
                        dialog.Initialize(transaction);
                        dialog.Owner = Window.GetWindow(this);
                        
                        if (dialog.ShowDialog() == true)
                        {
                            LoadData();
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi khi tải giao dịch: {ex.Message}", "Lỗi", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is int transactionId)
            {
                var result = MessageBox.Show("Bạn có chắc chắn muốn xóa giao dịch này?", 
                    "Xác nhận xóa", MessageBoxButton.YesNo, MessageBoxImage.Question);
                
                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        var (success, message) = await _transactionService.DeleteTransactionAsync(transactionId, _sessionContext.CurrentUserId ?? 0);
                        
                        if (success)
                        {
                            LoadData();
                            MessageBox.Show(message, "Thành công", 
                                MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        else
                        {
                            MessageBox.Show(message, "Lỗi", 
                                MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Lỗi khi xóa giao dịch: {ex.Message}", 
                            "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }


        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _searchText = SearchTextBox.Text;
            ApplyFilter(_currentFilter);
        }


        private void AdvancedFilterButton_Click(object sender, RoutedEventArgs e)
        {
            if (AdvancedFilterPanel.Visibility == Visibility.Visible)
            {
                AdvancedFilterPanel.Visibility = Visibility.Collapsed;
                AdvancedFilterButton.Style = (Style)FindResource("MaterialDesignFlatButton");
            }
            else
            {
                AdvancedFilterPanel.Visibility = Visibility.Visible;
                AdvancedFilterButton.Style = (Style)FindResource("MaterialDesignRaisedButton");
                LoadCategoriesForFilter();
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
                CategoryFilterComboBox.SelectedIndex = 0;
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
                var minAmount = 0m;
                var maxAmount = decimal.MaxValue;
                var fromDate = FromDatePicker.SelectedDate;
                var toDate = ToDatePicker.SelectedDate;
                ApplyAdvancedFilter(selectedCategoryId, minAmount, maxAmount, fromDate, toDate);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi áp dụng bộ lọc: {ex.Message}", "Lỗi", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ClearAdvancedFilter_Click(object sender, RoutedEventArgs e)
        {
            CategoryFilterComboBox.SelectedIndex = 0;
            FromDatePicker.SelectedDate = null;
            ToDatePicker.SelectedDate = null;
            
            ApplyAdvancedFilter(0, 0, decimal.MaxValue, null, null);
        }

        private void ApplyAdvancedFilter(int categoryId, decimal minAmount, decimal maxAmount, DateTime? fromDate, DateTime? toDate)
        {
            if (_allExpenses == null) return;

            var filteredExpenses = _allExpenses.AsEnumerable();
            if (categoryId > 0)
            {
                filteredExpenses = filteredExpenses.Where(e => e.CategoryId == categoryId);
            }
            filteredExpenses = filteredExpenses.Where(e => e.Amount >= minAmount && e.Amount <= maxAmount);
            if (fromDate.HasValue)
            {
                filteredExpenses = filteredExpenses.Where(e => e.Date >= fromDate.Value);
            }
            if (toDate.HasValue)
            {
                filteredExpenses = filteredExpenses.Where(e => e.Date <= toDate.Value);
            }
            if (!string.IsNullOrEmpty(_searchText))
            {
                filteredExpenses = filteredExpenses.Where(e => 
                    e.Description.Contains(_searchText, StringComparison.OrdinalIgnoreCase));
            }
            ExpenseListView.ItemsSource = filteredExpenses.OrderByDescending(e => e.Date).ToList();
        }

        private void AddExpenseButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = _addTransactionDialogFactory();
                dialog.Initialize(TransactionType.Expense);
                dialog.Owner = Window.GetWindow(this);
                if (dialog.ShowDialog() == true)
                {
                    var mainWindow = Application.Current.MainWindow as MainWindow;
                    mainWindow?.UpdateSidebarData();
                    LoadData();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi thêm chi tiêu: {ex.Message}", 
                    "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}