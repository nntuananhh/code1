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
    public partial class AddTransactionDialog : Window
    {
        private readonly ITransactionService _transactionService;
        private readonly ICategoryService _categoryService;
        private readonly ISessionContext _sessionContext;
        private readonly Func<AddCategoryDialog> _addCategoryDialogFactory;
        private TransactionType _transactionType;
        private List<Category> _categories = new List<Category>();

        public AddTransactionDialog(ITransactionService transactionService, ICategoryService categoryService, 
            ISessionContext sessionContext, Func<AddCategoryDialog> addCategoryDialogFactory)
        {
            InitializeComponent();
            _transactionService = transactionService;
            _categoryService = categoryService;
            _sessionContext = sessionContext;
            _addCategoryDialogFactory = addCategoryDialogFactory;
        }

        public void Initialize(TransactionType transactionType)
        {
            _transactionType = transactionType;
            InitializeDialog();
            LoadCategories();
        }

        private void InitializeDialog()
        {
            // Set initial values
            DatePicker.SelectedDate = DateTime.Now;
            DatePicker.DisplayDateEnd = DateTime.Now.Date; // Chỉ cho phép chọn ngày hiện tại và quá khứ
            
            if (_transactionType == TransactionType.Income)
            {
                DialogTitle.Text = "Thêm thu nhập";
                DialogSubtitle.Text = "Nhập thông tin thu nhập mới";
                TransactionTypeComboBox.SelectedIndex = 0;
            }
            else
            {
                DialogTitle.Text = "Thêm chi tiêu";
                DialogSubtitle.Text = "Nhập thông tin chi tiêu mới";
                TransactionTypeComboBox.SelectedIndex = 1;
            }
            TransactionTypeComboBox.SelectionChanged += TransactionTypeComboBox_SelectionChanged;
        }

        private async void LoadCategories()
        {
            try
            {
                var userId = _sessionContext.CurrentUserId ?? 0;
                _categories = await _categoryService.GetCategoriesAsync(userId, _transactionType);

                CategoryComboBox.ItemsSource = _categories;
                CategoryComboBox.DisplayMemberPath = "Name";
                CategoryComboBox.SelectedValuePath = "Id";

                if (_categories.Any())
                {
                    CategoryComboBox.SelectedIndex = 0;
                }
                else
                {
                    MessageBox.Show($"Không tìm thấy danh mục cho loại giao dịch {_transactionType}. Vui lòng thêm danh mục trước.", 
                        "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                }
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
                    _transactionType = TransactionType.Income;
                }
                else if (tag == "Expense")
                {
                    _transactionType = TransactionType.Expense;
                }
                
                CategoryComboBox.SelectedValue = null;
                LoadCategories();
            }
        }

        private void AddCategoryButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = _addCategoryDialogFactory();
            dialog.Initialize(_transactionType);
            dialog.Owner = this;
            
            if (dialog.ShowDialog() == true)
            {
                LoadCategories();
            }
        }

        private void AmountTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Only allow numbers and decimal point
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
                MessageBox.Show("Vui lòng nhập số tiền hợp lệ!", "Lỗi", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                AmountTextBox.Focus();
                return false;
            }
            if (string.IsNullOrWhiteSpace(DescriptionTextBox.Text))
            {
                MessageBox.Show("Vui lòng nhập mô tả giao dịch!", "Lỗi", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                DescriptionTextBox.Focus();
                return false;
            }
            if (CategoryComboBox.SelectedValue == null)
            {
                MessageBox.Show("Vui lòng chọn danh mục!", "Lỗi", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                CategoryComboBox.Focus();
                return false;
            }
            if (DatePicker.SelectedDate == null)
            {
                MessageBox.Show("Vui lòng chọn ngày giao dịch!", "Lỗi", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                DatePicker.Focus();
                return false;
            }

            return true;
        }

        private async Task<bool> ValidateBalance()
        {
            if (_transactionType == TransactionType.Expense)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(AmountTextBox.Text) || 
                        !decimal.TryParse(AmountTextBox.Text, out decimal amount) ||
                        amount <= 0)
                        return true;

                    if (CategoryComboBox.SelectedValue == null)
                        return true;

                    if (DatePicker.SelectedDate == null)
                        return true;

                    var categoryId = (int)CategoryComboBox.SelectedValue;
                    var userId = _sessionContext.CurrentUserId ?? 0;
                    var selectedDate = DatePicker.SelectedDate.Value;
                    
                    var (canAfford, message) = await _transactionService.ValidateExpenseTransactionAsync(
                        amount, categoryId, userId, selectedDate);
                    
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

                var userId = _sessionContext.CurrentUserId ?? 0;
                var selectedCategory = CategoryComboBox.SelectedItem as Category;
                var amount = decimal.Parse(AmountTextBox.Text);
                var date = DatePicker.SelectedDate!.Value;
                var description = DescriptionTextBox.Text.Trim();

                if (selectedCategory == null)
                {
                    MessageBox.Show("Vui lòng chọn danh mục!", "Lỗi", 
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                var transaction = new Transaction
                {
                    Amount = amount,
                    Description = string.IsNullOrWhiteSpace(description) ? "Giao dịch mới" : description,
                    Type = _transactionType,
                    CategoryId = selectedCategory.Id,
                    UserId = userId,
                    Date = date
                };

                var (success, message) = await _transactionService.CreateTransactionAsync(transaction, userId);

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
                MessageBox.Show($"Lỗi khi thêm giao dịch: {ex.Message}", "Lỗi", 
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
