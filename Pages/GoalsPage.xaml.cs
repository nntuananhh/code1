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
    public partial class GoalsPage : Page
    {
        private readonly IGoalService _goalService;
        private readonly ISessionContext _sessionContext;
        private readonly Func<AddGoalDialog> _addGoalDialogFactory;
        private readonly Func<EditGoalDialog> _editGoalDialogFactory;
        private readonly Func<AddMoneyToGoalDialog> _addMoneyToGoalDialogFactory;
        private List<Goal> _allGoals = new List<Goal>();
        private string _currentFilter = "Active";

        public GoalsPage(IGoalService goalService, ISessionContext sessionContext,
            Func<AddGoalDialog> addGoalDialogFactory, Func<EditGoalDialog> editGoalDialogFactory,
            Func<AddMoneyToGoalDialog> addMoneyToGoalDialogFactory)
        {
            InitializeComponent();
            _goalService = goalService;
            _sessionContext = sessionContext;
            _addGoalDialogFactory = addGoalDialogFactory;
            _editGoalDialogFactory = editGoalDialogFactory;
            _addMoneyToGoalDialogFactory = addMoneyToGoalDialogFactory;
            LoadData();
        }

        public async void LoadData()
        {
            try
            {
                _allGoals = await _goalService.GetGoalsAsync(_sessionContext.CurrentUserId ?? 0);
                UpdateSummaryCards();
                ApplyFilter(_currentFilter);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi tải dữ liệu: {ex.Message}", "Lỗi", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateSummaryCards()
        {
            if (_allGoals == null || !_allGoals.Any())
            {
                TotalGoalsText.Text = "0";
                CompletedGoalsText.Text = "0";
                SuccessRateText.Text = "0%";
                return;
            }

            var totalGoals = _allGoals.Count;
            var completedGoals = _allGoals.Count(g => g.IsCompleted);
            var successRate = totalGoals > 0 ? (completedGoals * 100.0 / totalGoals) : 0;

            TotalGoalsText.Text = totalGoals.ToString();
            CompletedGoalsText.Text = completedGoals.ToString();
            SuccessRateText.Text = $"{successRate:F1}%";
        }

        private void ApplyFilter(string filter)
        {
            if (_allGoals == null) return;

            var filteredGoals = filter switch
            {
                "Active" => _allGoals.Where(g => !g.IsCompleted),
                "Completed" => _allGoals.Where(g => g.IsCompleted),
                _ => _allGoals
            };

            var goalViewModels = filteredGoals.Select(g => new GoalViewModel
            {
                Id = g.Id,
                Title = g.Name,
                Description = g.Description ?? string.Empty,
                TargetAmount = g.TargetAmount,
                CurrentAmount = g.CurrentAmount,
                TargetDate = g.TargetDate,
                IsCompleted = g.IsCompleted,
                ProgressPercentage = g.TargetAmount > 0 ? (double)(g.CurrentAmount / g.TargetAmount) * 100 : 0,
                StatusText = GetGoalStatus(g),
                StatusColor = GetGoalStatusColor(g)
            }).OrderByDescending(g => g.TargetDate).ToList();

            GoalsListView.ItemsSource = goalViewModels;
        }

        private string GetGoalStatus(Goal goal)
        {
            if (_goalService is WpfApp3.Services.GoalService goalService)
            {
                return goalService.GetGoalStatus(goal);
            }
            return _goalService.GetGoalStatusAsync(goal).Result;
        }

        private Brush GetGoalStatusColor(Goal goal)
        {
            if (goal.IsCompleted)
                return new SolidColorBrush(Color.FromRgb(76, 175, 80)); // Green
            
            var now = DateTime.Now;
            if (goal.TargetDate < now)
                return new SolidColorBrush(Color.FromRgb(244, 67, 54)); // Red
            
            var daysLeft = (goal.TargetDate - now).Days;
            if (daysLeft <= 7)
                return new SolidColorBrush(Color.FromRgb(255, 152, 0)); // Orange
            
            return new SolidColorBrush(Color.FromRgb(33, 150, 243)); // Blue
        }

        private void FilterButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                // Reset all button styles
                ActiveButton.Style = (Style)FindResource("MaterialDesignFlatButton");
                ActiveButton.Background = Brushes.Transparent;
                ActiveButton.Foreground = (SolidColorBrush)FindResource("MaterialDesignBody");
                
                CompletedButton.Style = (Style)FindResource("MaterialDesignFlatButton");
                CompletedButton.Background = Brushes.Transparent;
                CompletedButton.Foreground = (SolidColorBrush)FindResource("MaterialDesignBody");
  
                button.Style = (Style)FindResource("MaterialDesignRaisedButton");
                button.Background = new SolidColorBrush(Color.FromRgb(33, 150, 243)); // Blue color
                button.Foreground = Brushes.White;

                _currentFilter = button.Tag?.ToString() ?? "Active";
                ApplyFilter(_currentFilter);
            }
        }

        private void AddGoalButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = _addGoalDialogFactory();
            dialog.Owner = Window.GetWindow(this);
            
            var result = dialog.ShowDialog();
            
            if (result == true)
            {
                LoadData();
            }
        }

        private async void EditButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is int goalId)
            {
                try
                {
                    var goal = await _goalService.GetGoalByIdAsync(goalId, _sessionContext.CurrentUserId ?? 0);
                    
                    if (goal != null)
                    {
                        var dialog = _editGoalDialogFactory();
                        dialog.Initialize(goal);
                        dialog.Owner = Window.GetWindow(this);
                        
                        if (dialog.ShowDialog() == true)
                        {
                            LoadData();
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi khi tải mục tiêu: {ex.Message}", "Lỗi", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is int goalId)
            {
                var result = MessageBox.Show("Bạn có chắc chắn muốn xóa mục tiêu này?", 
                    "Xác nhận xóa", MessageBoxButton.YesNo, MessageBoxImage.Question);
                
                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        var success = await _goalService.DeleteGoalAsync(goalId, _sessionContext.CurrentUserId ?? 0);
                        
                        if (success)
                        {
                            LoadData();
                            MessageBox.Show("Mục tiêu đã được xóa thành công!", 
                                "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        else
                        {
                            MessageBox.Show("Lỗi khi xóa mục tiêu!", 
                                "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Lỗi khi xóa mục tiêu: {ex.Message}", 
                            "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private async void AddMoneyButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is int goalId)
            {
                try
                {
                    var goal = await _goalService.GetGoalByIdAsync(goalId, _sessionContext.CurrentUserId ?? 0);
                    
                    if (goal != null)
                    {
                        var amountDialog = _addMoneyToGoalDialogFactory();
                        amountDialog.Initialize(goal);
                        amountDialog.Owner = Window.GetWindow(this);
                        
                        if (amountDialog.ShowDialog() == true)
                        {
                            LoadData();
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi khi thêm tiền vào mục tiêu: {ex.Message}", "Lỗi", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }

    public class GoalViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal TargetAmount { get; set; }
        public decimal CurrentAmount { get; set; }
        public DateTime TargetDate { get; set; }
        public bool IsCompleted { get; set; }
        public double ProgressPercentage { get; set; }
        public string StatusText { get; set; } = string.Empty;
        public Brush StatusColor { get; set; } = Brushes.Transparent;
    }
}