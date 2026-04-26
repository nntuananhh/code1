using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpfApp3.Models;
using WpfApp3.Services;

namespace WpfApp3.Dialogs
{
    public partial class AddMoneyToGoalDialog : Window
    {
        private readonly IGoalService _goalService;
        private readonly ISessionContext _sessionContext;
        private Goal _goal = null!;

        public AddMoneyToGoalDialog(IGoalService goalService, ISessionContext sessionContext)
        {
            InitializeComponent();
            _goalService = goalService;
            _sessionContext = sessionContext;
        }

        public void Initialize(Goal goal)
        {
            _goal = goal;
            GoalNameText.Text = $"Mục tiêu: {goal.Name}";
            UpdateCurrentProgress();
            AmountToAddTextBox.Focus();
        }

        private void UpdateCurrentProgress()
        {
            var currentPercentage = _goal.TargetAmount > 0 ? (double)(_goal.CurrentAmount / _goal.TargetAmount) * 100 : 0;
            
            CurrentAmountText.Text = $"{_goal.CurrentAmount:N0} ₫";
            TargetAmountText.Text = $"{_goal.TargetAmount:N0} ₫";
            ProgressPercentageText.Text = $"{currentPercentage:F1}%";
            CurrentProgressBar.Value = currentPercentage;
        }

        private void AmountToAddTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdatePreview();
        }

        private void UpdatePreview()
        {
            if (decimal.TryParse(AmountToAddTextBox.Text, out decimal amountToAdd))
            {
                var newCurrentAmount = _goal.CurrentAmount + amountToAdd;
                var newPercentage = _goal.TargetAmount > 0 ? (double)(newCurrentAmount / _goal.TargetAmount) * 100 : 0;
                
                NewAmountText.Text = $"{newCurrentAmount:N0} ₫";
                NewTargetAmountText.Text = $"{_goal.TargetAmount:N0} ₫";
                NewProgressPercentageText.Text = $"{newPercentage:F1}%";
                NewProgressBar.Value = Math.Min(newPercentage, 100);
                
                // Kiểm tra xem có hoàn thành mục tiêu không
                if (newCurrentAmount >= _goal.TargetAmount)
                {
                    CompletionCheckBorder.Visibility = Visibility.Visible;
                    CompletionText.Text = "🎉 Mục tiêu sẽ được hoàn thành!";
                    NewProgressBar.Foreground = new SolidColorBrush(Color.FromRgb(76, 175, 80)); // Green
                }
                else
                {
                    CompletionCheckBorder.Visibility = Visibility.Collapsed;
                    NewProgressBar.Foreground = new SolidColorBrush(Color.FromRgb(33, 150, 243)); // Blue
                }
            }
            else
            {
                // Reset preview
                NewAmountText.Text = $"{_goal.CurrentAmount:N0} ₫";
                NewTargetAmountText.Text = $"{_goal.TargetAmount:N0} ₫";
                NewProgressPercentageText.Text = $"{(_goal.TargetAmount > 0 ? (double)(_goal.CurrentAmount / _goal.TargetAmount) * 100 : 0):F1}%";
                NewProgressBar.Value = _goal.TargetAmount > 0 ? (double)(_goal.CurrentAmount / _goal.TargetAmount) * 100 : 0;
                CompletionCheckBorder.Visibility = Visibility.Collapsed;
            }
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
            if (string.IsNullOrWhiteSpace(AmountToAddTextBox.Text) || 
                !decimal.TryParse(AmountToAddTextBox.Text, out decimal amountToAdd) || 
                amountToAdd <= 0)
            {
                MessageBox.Show("Vui lòng nhập số tiền hợp lệ.", "Lỗi", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                AmountToAddTextBox.Focus();
                return false;
            }

            return true;
        }

        private async void AddButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!ValidateInput())
                    return;

                var amountToAdd = decimal.Parse(AmountToAddTextBox.Text);
                var userId = _sessionContext.CurrentUserId ?? 0;

                var (success, message) = await _goalService.AddMoneyToGoalAsync(
                    _goal.Id, amountToAdd, userId);

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
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi thêm tiền vào mục tiêu: {ex.Message}", "Lỗi", 
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
