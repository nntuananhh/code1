using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfApp3.Models;
using WpfApp3.Services;

namespace WpfApp3.Dialogs
{
    public partial class AddGoalDialog : Window
    {
        private readonly IGoalService _goalService;
        private readonly ISessionContext _sessionContext;

        public AddGoalDialog(IGoalService goalService, ISessionContext sessionContext)
        {
            InitializeComponent();
            _goalService = goalService;
            _sessionContext = sessionContext;
            TargetDatePicker.SelectedDate = DateTime.Now.AddMonths(6);
            GoalNameTextBox.Focus();
        }

        private void AmountTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Only allow numbers and decimal point
            var regex = new System.Text.RegularExpressions.Regex(@"^[0-9]+(\.[0-9]{0,2})?$");
            var text = ((TextBox)sender).Text + e.Text;
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

            if (currentAmount >= targetAmount)
            {
                MessageBox.Show("Số tiền hiện tại không được lớn hơn hoặc bằng số tiền mục tiêu.", "Lỗi", 
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

            if (TargetDatePicker.SelectedDate <= DateTime.Now)
            {
                MessageBox.Show("Ngày mục tiêu phải sau ngày hiện tại.", "Lỗi", 
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

                var userId = _sessionContext.CurrentUserId ?? 0;
                var targetDate = TargetDatePicker.SelectedDate!.Value;
                var goal = new Goal
                {
                    Name = GoalNameTextBox.Text.Trim(),
                    Description = "",
                    TargetAmount = decimal.Parse(TargetAmountTextBox.Text),
                    CurrentAmount = decimal.Parse(CurrentAmountTextBox.Text),
                    TargetDate = targetDate,
                    Color = "#9C27B0",
                    UserId = userId,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                var success = await _goalService.CreateGoalAsync(goal, userId);

                if (success)
                {
                    MessageBox.Show("Mục tiêu đã được thêm thành công!", "Thành công", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    DialogResult = true;
                    Close();
                }
                else
                {
                    MessageBox.Show("Lỗi khi thêm mục tiêu!", "Lỗi", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi thêm mục tiêu: {ex.Message}", "Lỗi", 
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
