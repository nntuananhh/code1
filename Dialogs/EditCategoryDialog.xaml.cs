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
    public partial class EditCategoryDialog : Window
    {
        private readonly ICategoryService _categoryService;
        private readonly ISessionContext _sessionContext;
        private Category _category = null!;

        public EditCategoryDialog(ICategoryService categoryService, ISessionContext sessionContext)
        {
            InitializeComponent();
            _categoryService = categoryService;
            _sessionContext = sessionContext;
            ColorTextBox.TextChanged += ColorTextBox_TextChanged;
        }

        public void Initialize(Category category)
        {
            _category = category;
            LoadCategoryData();
        }

        private void LoadCategoryData()
        {
            if (_category.Type == TransactionType.Income)
            {
                CategoryTypeComboBox.SelectedIndex = 0;
            }
            else
            {
                CategoryTypeComboBox.SelectedIndex = 1;
            }

            CategoryNameTextBox.Text = _category.Name;
            ColorTextBox.Text = _category.Color;

            var iconItems = IconComboBox.Items.Cast<ComboBoxItem>();
            var selectedIcon = iconItems.FirstOrDefault(item => item.Tag?.ToString() == _category.Icon);
            if (selectedIcon != null)
            {
                IconComboBox.SelectedItem = selectedIcon;
            }
            else
            {
                IconComboBox.SelectedIndex = 0;
            }
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
                var success = await _categoryService.UpdateCategoryAsync(
                    _category.Id,
                    CategoryNameTextBox.Text.Trim(),
                    transactionType,
                    ColorTextBox.Text.Trim(),
                    iconName,
                    userId);

                if (!success)
                {
                    MessageBox.Show($"Danh mục '{CategoryNameTextBox.Text.Trim()}' đã tồn tại cho loại {transactionType} hoặc có lỗi xảy ra.", 
                        "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                MessageBox.Show("Danh mục đã được cập nhật thành công!", "Thành công", 
                    MessageBoxButton.OK, MessageBoxImage.Information);

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi cập nhật danh mục: {ex.Message}", "Lỗi", 
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
