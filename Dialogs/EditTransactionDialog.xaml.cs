using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfApp3.Models;
using WpfApp3.Services;

namespace WpfApp3.Dialogs
{
    public partial class EditTransactionDialog : Window
    {
        private readonly ITransactionService _transactionService;
        private readonly ICategoryService _categoryService;
        private readonly ISessionContext _sessionContext;
        private Transaction _transaction = null!;
        private List<Category> _categories = new List<Category>();

        public EditTransactionDialog(ITransactionService transactionService, ICategoryService categoryService, ISessionContext sessionContext)
        {
            InitializeComponent();
            _transactionService = transactionService;
            _categoryService = categoryService;
            _sessionContext = sessionContext;
            TransactionTypeComboBox.SelectionChanged += TransactionTypeComboBox_SelectionChanged;
        }

        public void Initialize(Transaction transaction)
        {
            _transaction = transaction;
            LoadTransactionData();
            LoadCategories();
        }

        private void LoadTransactionData()
        {
            if (_transaction.Type == TransactionType.Income)
            {
                TransactionTypeComboBox.SelectedIndex = 0;
            }
            else
            {
                TransactionTypeComboBox.SelectedIndex = 1;
            }
            AmountTextBox.Text = _transaction.Amount.ToString();
            DescriptionTextBox.Text = _transaction.Description;
            DatePicker.SelectedDate = _transaction.Date;
            DatePicker.DisplayDateEnd = DateTime.Now.Date; // Chỉ cho phép chọn ngày hiện tại và quá khứ
        }

        private async void LoadCategories()
        {
            try
            {
                var userId = _sessionContext.CurrentUserId ?? 0;
                _categories = await _categoryService.GetCategoriesAsync(userId, _transaction.Type);

                CategoryComboBox.ItemsSource = _categories;
                CategoryComboBox.DisplayMemberPath = "Name";
                CategoryComboBox.SelectedValuePath = "Id";
                CategoryComboBox.SelectedValue = _transaction.CategoryId;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi tải danh mục: {ex.Message}", "Lỗi",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void TransactionTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TransactionTypeComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                var tag = selectedItem.Tag?.ToString();
                if (tag == "Income")
                {
                    _transaction.Type = TransactionType.Income;
                }
                else if (tag == "Expense")
                {
                    _transaction.Type = TransactionType.Expense;
                }

                LoadCategories();
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
            if (string.IsNullOrWhiteSpace(AmountTextBox.Text) || 
                !decimal.TryParse(AmountTextBox.Text, out decimal amount) || 
                amount <= 0)
            {
                MessageBox.Show("Vui lòng nhập số tiền hợp lệ.", "Lỗi",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                AmountTextBox.Focus();
                return false;
            }

            if (string.IsNullOrWhiteSpace(DescriptionTextBox.Text))
            {
                MessageBox.Show("Vui lòng nhập mô tả giao dịch.", "Lỗi",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                DescriptionTextBox.Focus();
                return false;
            }

            if (CategoryComboBox.SelectedItem == null)
            {
                MessageBox.Show("Vui lòng chọn danh mục.", "Lỗi",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                CategoryComboBox.Focus();
                return false;
            }

            if (DatePicker.SelectedDate == null)
            {
                MessageBox.Show("Vui lòng chọn ngày giao dịch.", "Lỗi",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                DatePicker.Focus();
                return false;
            }

            return true;
        }

        private async Task<bool> ValidateBalance()
        {
            // Kiểm tra số dư nếu là chi tiêu
            if (_transaction.Type == TransactionType.Expense)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(AmountTextBox.Text) ||
                        !decimal.TryParse(AmountTextBox.Text, out decimal newAmount) ||
                        newAmount <= 0)
                        return true;

                    if (CategoryComboBox.SelectedItem == null)
                        return true;

                    if (DatePicker.SelectedDate == null)
                        return true;

                    var selectedCategory = (Category)CategoryComboBox.SelectedItem;
                    var categoryId = selectedCategory.Id;
                    var userId = _sessionContext.CurrentUserId ?? 0;
                    var selectedDate = DatePicker.SelectedDate.Value;

                    var (canAfford, message) = await _transactionService.ValidateExpenseTransactionUpdateAsync(
                        _transaction.Id, newAmount, categoryId, userId, selectedDate);

                    if (!canAfford)
                    {
                        MessageBox.Show(message, "Không đủ số dư",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        AmountTextBox.Focus();
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi khi kiểm tra số dư: {ex.Message}", "Lỗi",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }
            }

            return true;
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!ValidateInput())
                    return;

                if (!await ValidateBalance())
                    return;

                var amount = decimal.Parse(AmountTextBox.Text);
                var description = DescriptionTextBox.Text.Trim();
                var categoryId = (int)CategoryComboBox.SelectedValue;
                var date = DatePicker.SelectedDate!.Value;
                var userId = _sessionContext.CurrentUserId ?? 0;

                var (success, message) = await _transactionService.UpdateTransactionAsync(
                    _transaction.Id, amount, description, categoryId, date, userId);

                if (success)
                {
                    MessageBox.Show(message, "Thành công",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    DialogResult = true;
                    Close();
                }
                else
                {
                    MessageBox.Show(message, "Lỗi",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi cập nhật giao dịch: {ex.Message}", "Lỗi",
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
