using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfApp3.Models;
using WpfApp3.Services;

namespace WpfApp3.Dialogs
{
    public partial class EditGoalDialog : Window
    {
        private readonly IGoalService _goalService;
        private readonly ISessionContext _sessionContext;
        private Goal _goal = null!;

        public EditGoalDialog(IGoalService goalService, ISessionContext sessionContext)
        {
            InitializeComponent();
            _goalService = goalService;
            _sessionContext = sessionContext;
        }

        public void Initialize(Goal goal)
        {
            _goal = goal;
            LoadGoalData();
        }

        private void LoadGoalData()
        {
            try
            {
                GoalNameTextBox.Text = _goal.Name;
                TargetAmountTextBox.Text = _goal.TargetAmount.ToString("F2");
                CurrentAmountTextBox.Text = _goal.CurrentAmount.ToString("F2");
                TargetDatePicker.SelectedDate = _goal.TargetDate;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi tải dữ liệu mục tiêu: {ex.Message}", "Lỗi", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AmountTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            var regex = new System.Text.RegularExpressions.Regex(@"^[0-9]+(\.[0-9]{0,2})?$");
            var textBox = sender as TextBox;
            var current = textBox?.Text ?? string.Empty;
            var text = current + e.Text;
            e.Handled = !regex.IsMatch(text);
        }

        private bool ValidateInput()
        {
            if (string.IsNullOrWhiteSpace(GoalNameTextBox.Text))
            {
                MessageBox.Show("Vui lòng nhập tên mục tiêu.", "Lỗi", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                GoalNameTextBox.Focus();
                return false;
            }

            if (string.IsNullOrWhiteSpace(TargetAmountTextBox.Text) || 
                !decimal.TryParse(TargetAmountTextBox.Text, out decimal targetAmount) || 
                targetAmount <= 0)
            {
                MessageBox.Show("Vui lòng nhập số tiền mục tiêu hợp lệ.", "Lỗi", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                TargetAmountTextBox.Focus();
                return false;
            }

            if (string.IsNullOrWhiteSpace(CurrentAmountTextBox.Text) || 
                !decimal.TryParse(CurrentAmountTextBox.Text, out decimal currentAmount) || 
                currentAmount < 0)
            {
                MessageBox.Show("Vui lòng nhập số tiền hiện tại hợp lệ.", "Lỗi", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                CurrentAmountTextBox.Focus();
                return false;
            }

            if (currentAmount > targetAmount)
            {
                MessageBox.Show("Số tiền hiện tại không thể lớn hơn số tiền mục tiêu.", "Lỗi", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                CurrentAmountTextBox.Focus();
                return false;
            }

            if (TargetDatePicker.SelectedDate == null)
            {
                MessageBox.Show("Vui lòng chọn ngày mục tiêu.", "Lỗi", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                TargetDatePicker.Focus();
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

                var name = GoalNameTextBox.Text.Trim();
                var description = string.Empty;
                var targetAmount = decimal.Parse(TargetAmountTextBox.Text);
                var targetDate = TargetDatePicker.SelectedDate!.Value;
                var userId = _sessionContext.CurrentUserId ?? 0;

                var success = await _goalService.UpdateGoalAsync(
                    _goal.Id, name, description, targetAmount, targetDate, userId);

                if (success)
                {
                    MessageBox.Show("Mục tiêu đã được cập nhật thành công!", "Thành công", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    DialogResult = true;
                    Close();
                }
                else
                {
                    MessageBox.Show("Lỗi khi cập nhật mục tiêu!", "Lỗi", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi cập nhật mục tiêu: {ex.Message}", "Lỗi", 
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

















