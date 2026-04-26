using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfApp3.Models;
using WpfApp3.Services;

namespace WpfApp3.Dialogs
{
    public partial class AddBudgetDialog : Window
    {
        private readonly IBudgetService _budgetService;
        private readonly ICategoryService _categoryService;
        private readonly ISessionContext _sessionContext;

        public AddBudgetDialog(IBudgetService budgetService, ICategoryService categoryService, ISessionContext sessionContext)
        {
            InitializeComponent();
            _budgetService = budgetService;
            _categoryService = categoryService;
            _sessionContext = sessionContext;

            PeriodComboBox.SelectedIndex = 0;
            StartDatePicker.SelectedDate = DateTime.Now;
            EndDatePicker.SelectedDate = DateTime.Now.AddMonths(1);
            AmountTextBox.Focus();

            LoadCategories();
        }

        private async void LoadCategories()
        {
            try
            {
                var userId = _sessionContext.CurrentUserId ?? 0;
                var categories = await _categoryService.GetCategoriesAsync(userId, TransactionType.Expense);

                CategoryComboBox.ItemsSource = categories;
                CategoryComboBox.DisplayMemberPath = "Name";
                CategoryComboBox.SelectedValuePath = "Id";

                if (categories.Any())
                {
                    CategoryComboBox.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi tải danh mục: {ex.Message}", "Lỗi", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AmountTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            var regex = new System.Text.RegularExpressions.Regex(@"^[0-9]+(\.[0-9]{0,2})?$");
            var text = AmountTextBox.Text + e.Text;
            e.Handled = !regex.IsMatch(text);
        }

        private bool ValidateInput()
        {
            if (CategoryComboBox.SelectedItem == null)
            {
                MessageBox.Show("Vui lòng chọn danh mục.", "Lỗi", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                CategoryComboBox.Focus();
                return false;
            }

            if (string.IsNullOrWhiteSpace(AmountTextBox.Text) || 
                !decimal.TryParse(AmountTextBox.Text, out decimal amount) || 
                amount <= 0)
            {
                MessageBox.Show("Vui lòng nhập số tiền ngân sách hợp lệ.", "Lỗi", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                AmountTextBox.Focus();
                return false;
            }

            if (StartDatePicker.SelectedDate == null)
            {
                MessageBox.Show("Vui lòng chọn ngày bắt đầu.", "Lỗi", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                StartDatePicker.Focus();
                return false;
            }

            if (EndDatePicker.SelectedDate == null)
            {
                MessageBox.Show("Vui lòng chọn ngày kết thúc.", "Lỗi", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                EndDatePicker.Focus();
                return false;
            }

            if (StartDatePicker.SelectedDate >= EndDatePicker.SelectedDate)
            {
                MessageBox.Show("Ngày kết thúc phải sau ngày bắt đầu.", "Lỗi", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                EndDatePicker.Focus();
                return false;
            }

            return true;
        }

        private Budget? BuildBudgetFromUI()
        {
            var selectedCategory = CategoryComboBox.SelectedItem as Category;
            if (selectedCategory == null)
            {
                MessageBox.Show("Vui lòng chọn danh mục!", "Lỗi", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return null;
            }

            if (!decimal.TryParse(AmountTextBox.Text, out var budgetAmount) || budgetAmount <= 0)
                return null;

            return new Budget
            {
                Name = selectedCategory.Name + " Budget",
                CategoryId = selectedCategory.Id,
                Amount = budgetAmount,
                SpentAmount = 0,
                StartDate = StartDatePicker.SelectedDate!.Value,
                EndDate = EndDatePicker.SelectedDate!.Value,
                Notes = "",
                IsActive = true,
                UserId = _sessionContext.CurrentUserId ?? 0,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!ValidateInput())
                    return;

                var budget = BuildBudgetFromUI();
                if (budget == null)
                    return;
                var success = await _budgetService.CreateBudgetAsync(budget, budget.UserId);
                if (success)
                {
                    MessageBox.Show("Ngân sách đã được thêm thành công!", "Thành công", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    DialogResult = true;
                    Close();
                }
                else
                {
                    MessageBox.Show("Lỗi khi thêm ngân sách! Có thể đã tồn tại ngân sách cho danh mục này trong khoảng thời gian này hoặc số dư không đủ.", 
                        "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi thêm ngân sách: {ex.Message}", "Lỗi", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
