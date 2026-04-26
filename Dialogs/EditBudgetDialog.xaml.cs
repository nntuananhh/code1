using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfApp3.Models;
using WpfApp3.Services;

namespace WpfApp3.Dialogs
{
    public partial class EditBudgetDialog : Window
    {
        private readonly IBudgetService _budgetService;
        private readonly ISessionContext _sessionContext;
        private Budget _budget = null!;

        public EditBudgetDialog(IBudgetService budgetService, ISessionContext sessionContext)
        {
            InitializeComponent();
            _budgetService = budgetService;
            _sessionContext = sessionContext;
        }

        public void Initialize(Budget budget)
        {
            _budget = budget;
            LoadBudgetData();
        }

        private void LoadBudgetData()
        {
            try
            {
                CategoryTextBox.Text = _budget.Category?.Name ?? "Không xác định";
                AmountTextBox.Text = _budget.Amount.ToString("F2");
                SpentAmountTextBox.Text = _budget.SpentAmount.ToString("F2");
                StartDatePicker.SelectedDate = _budget.StartDate;
                EndDatePicker.SelectedDate = _budget.EndDate;
                IsActiveCheckBox.IsChecked = _budget.IsActive;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi tải dữ liệu ngân sách: {ex.Message}", "Lỗi", 
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

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!ValidateInput())
                    return;

                var newAmount = decimal.Parse(AmountTextBox.Text);
                var startDate = StartDatePicker.SelectedDate!.Value;
                var endDate = EndDatePicker.SelectedDate!.Value;
                var isActive = IsActiveCheckBox.IsChecked ?? true;
                var userId = _sessionContext.CurrentUserId ?? 0;

                var success = await _budgetService.UpdateBudgetAsync(
                    _budget.Id, newAmount, startDate, endDate, isActive, userId);

                if (success)
                {
                    MessageBox.Show("Ngân sách đã được cập nhật thành công!", "Thành công", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    DialogResult = true;
                    Close();
                }
                else
                {
                    MessageBox.Show("Lỗi khi cập nhật ngân sách! Có thể đã tồn tại ngân sách khác cho danh mục này trong khoảng thời gian này hoặc số dư không đủ.", 
                        "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi cập nhật ngân sách: {ex.Message}", "Lỗi", 
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

