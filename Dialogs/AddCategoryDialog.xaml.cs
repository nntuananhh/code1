using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WpfApp3.Models;
using WpfApp3.Services;

namespace WpfApp3.Dialogs
{
    public partial class AddCategoryDialog : Window
    {
        private readonly ICategoryService _categoryService;
        private readonly ISessionContext _sessionContext;
        private TransactionType _transactionType;

        public AddCategoryDialog(ICategoryService categoryService, ISessionContext sessionContext)
        {
            InitializeComponent();
            _categoryService = categoryService;
            _sessionContext = sessionContext;
            _transactionType = TransactionType.Expense;
            CategoryNameTextBox.Focus();
            if (_transactionType == TransactionType.Income)
            {
                CategoryTypeComboBox.SelectedIndex = 0; // Income
            }
            else
            {
                CategoryTypeComboBox.SelectedIndex = 1; // Expense
            }
            
            IconComboBox.SelectedIndex = 0;
            ColorTextBox.TextChanged += ColorTextBox_TextChanged;
        }

        public void Initialize(TransactionType? transactionType = null)
        {
            _transactionType = transactionType ?? TransactionType.Expense;
            CategoryTypeComboBox.SelectedIndex = _transactionType == TransactionType.Income ? 0 : 1;
        }

        private void ColorTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                var colorText = ColorTextBox.Text.Trim();
                if (IsValidColor(colorText))
                {
                    var color = (Color)ColorConverter.ConvertFromString(colorText);
                    ColorPreview.Background = new SolidColorBrush(color);
                }
            }
            catch
            {
            }
        }

        private bool IsValidColor(string colorText)
        {
            if (string.IsNullOrWhiteSpace(colorText))
                return false;
                
            // Check if it's a valid hex color
            var hexPattern = @"^#([A-Fa-f0-9]{6}|[A-Fa-f0-9]{3})$";
            return Regex.IsMatch(colorText, hexPattern);
        }

        private bool ValidateInput()
        {
            if (string.IsNullOrWhiteSpace(CategoryNameTextBox.Text))
            {
                MessageBox.Show("Vui lòng nhập tên danh mục.", "Lỗi", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                CategoryNameTextBox.Focus();
                return false;
            }

            if (CategoryTypeComboBox.SelectedItem == null)
            {
                MessageBox.Show("Vui lòng chọn loại danh mục.", "Lỗi", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                CategoryTypeComboBox.Focus();
                return false;
            }

            if (!IsValidColor(ColorTextBox.Text))
            {
                MessageBox.Show("Vui lòng nhập mã màu hợp lệ (VD: #FF5722).", "Lỗi", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                ColorTextBox.Focus();
                return false;
            }

            return true;
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!ValidateInput())
                    return;
                var selectedType = CategoryTypeComboBox.SelectedItem as ComboBoxItem;
                var typeString = selectedType?.Tag?.ToString();
                var transactionType = typeString == "Income" ? TransactionType.Income : TransactionType.Expense;
                var selectedIcon = IconComboBox.SelectedItem as ComboBoxItem;
                var iconName = selectedIcon?.Tag?.ToString() ?? "CurrencyUsd";
                var userId = _sessionContext.CurrentUserId ?? 0;
                var category = new Category
                {
                    Name = CategoryNameTextBox.Text.Trim(),
                    Type = transactionType,
                    Color = ColorTextBox.Text.Trim(),
                    Icon = iconName
                };
                bool success = await _categoryService.CreateCategoryAsync(category, userId);
                if (success)
                {
                    MessageBox.Show("Danh mục đã được thêm thành công!", "Thành công",
                        MessageBoxButton.OK, MessageBoxImage.Information);

                    DialogResult = true;
                    Close();
                }
                else
                {
                    MessageBox.Show($"Danh mục '{category.Name}' đã tồn tại cho loại {transactionType}.",
                        "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi thêm danh mục: {ex.Message}", "Lỗi",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void QuickColor_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string colorCode)
            {
                ColorTextBox.Text = colorCode;
                try
                {
                    var color = (Color)ColorConverter.ConvertFromString(colorCode);
                    ColorPreview.Background = new SolidColorBrush(color);
                }
                catch
                {
                }
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
