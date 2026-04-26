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
    public partial class IncomePage : Page
    {
        private readonly ITransactionService _transactionService;
        private readonly ICategoryService _categoryService;
        private readonly ISessionContext _sessionContext;
        private readonly IDataService _dataService;
        private List<Transaction> _allIncomes = new List<Transaction>();
        private string _currentFilter = "ThisMonth";
        private string _searchText = string.Empty;

        private readonly Func<EditTransactionDialog> _editTransactionDialogFactory;
        private readonly Func<AddTransactionDialog> _addTransactionDialogFactory;

        public IncomePage(ITransactionService transactionService, ICategoryService categoryService, ISessionContext sessionContext,
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
                _allIncomes = await _transactionService.GetTransactionsAsync(_sessionContext.CurrentUserId ?? 0, TransactionType.Income);
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
            
            var totalIncome = await _dataService.GetTotalIncomeAsync(userId);
            TotalIncomeText.Text = $"{totalIncome:N0} ₫";

            var monthlyIncome = await _dataService.GetMonthlyIncomeAsync(userId);
            MonthlyIncomeText.Text = $"{monthlyIncome:N0} ₫";

            var averageIncome = await _dataService.GetAverageMonthlyIncomeAsync(userId);
            AverageIncomeText.Text = $"{averageIncome:N0} ₫";
        }

        private void ApplyFilter(string filter)
        {
            if (_allIncomes == null) return;

            var now = DateTime.Now;
            var filteredIncomes = filter switch
            {
                "ThisMonth" => _allIncomes.Where(t => t.Date.Month == now.Month && t.Date.Year == now.Year),
                "ThreeMonths" => _allIncomes.Where(t => t.Date >= now.AddMonths(-3)),
                "ThisYear" => _allIncomes.Where(t => t.Date.Year == now.Year),
                _ => _allIncomes.Where(t => t.Date.Month == now.Month && t.Date.Year == now.Year) // Default to ThisMonth
            };
            if (!string.IsNullOrWhiteSpace(_searchText))
            {
                filteredIncomes = filteredIncomes.Where(t => 
                    t.Description.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ||
                    (t.Category?.Name.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ?? false));
            }

            IncomeListView.ItemsSource = filteredIncomes.OrderByDescending(t => t.Date).ToList();
        }

        private void FilterButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                ThisYearButton.Style = (Style)FindResource("MaterialDesignFlatButton");
                ThisYearButton.Background = Brushes.Transparent;
                ThisYearButton.Foreground = (SolidColorBrush)FindResource("MaterialDesignBody");

                button.Style = (Style)FindResource("MaterialDesignRaisedButton");
                button.Background = new SolidColorBrush(Color.FromRgb(33, 150, 243)); // Blue color
                button.Foreground = Brushes.White;

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
                var categories = await _categoryService.GetCategoriesAsync(_sessionContext.CurrentUserId ?? 0, TransactionType.Income);
                
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
                // Get filter values
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
            if (_allIncomes == null) return;

            var filteredIncomes = _allIncomes.AsEnumerable();

            if (categoryId > 0)
            {
                filteredIncomes = filteredIncomes.Where(i => i.CategoryId == categoryId);
            }

            // Filter by amount range
            filteredIncomes = filteredIncomes.Where(i => i.Amount >= minAmount && i.Amount <= maxAmount);

            // Filter by date range
            if (fromDate.HasValue)
            {
                filteredIncomes = filteredIncomes.Where(i => i.Date >= fromDate.Value);
            }
            if (toDate.HasValue)
            {
                filteredIncomes = filteredIncomes.Where(i => i.Date <= toDate.Value);
            }

            if (!string.IsNullOrEmpty(_searchText))
            {
                filteredIncomes = filteredIncomes.Where(i => 
                    i.Description.Contains(_searchText, StringComparison.OrdinalIgnoreCase));
            }

            // Update ListView
            IncomeListView.ItemsSource = filteredIncomes.OrderByDescending(i => i.Date).ToList();
        }

        private void AddIncomeButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = _addTransactionDialogFactory();
                dialog.Initialize(TransactionType.Income);
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
                MessageBox.Show($"Lỗi khi thêm thu nhập: {ex.Message}", 
                    "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}