using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows;
using System.Windows.Threading;
using System.Collections.ObjectModel;
using System.Linq;
using WpfApp3.Services;
using WpfApp3.Models;
using WpfApp3.Dialogs;
using Microsoft.Extensions.DependencyInjection;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.Defaults;
using SkiaSharp;
using materialDesign = MaterialDesignThemes.Wpf;

namespace WpfApp3.Pages
{
    public partial class DashboardPage : Page
    {
        private readonly IDataService _dataService;
        private readonly ISessionContext _sessionContext;
        public ObservableCollection<ISeries> ExpenseSeries { get; set; }

        public DashboardPage(IDataService dataService, ISessionContext sessionContext)
        {
            InitializeComponent();
            _dataService = dataService;
            _sessionContext = sessionContext;
            ExpenseSeries = new ObservableCollection<ISeries>();
            DataContext = this;
            
            LoadData();
        }

        public async void RefreshData()
        {
            try
            {
                await Task.Delay(200);
                
                await LoadDataAsync();
            }
            catch (Exception)
            {
            }
        }


        private async void LoadData()
        {
            await LoadDataAsync();
        }

        private async Task LoadDataAsync()
        {
            try
            {
                // Kiểm tra dữ liệu
                var hasData = await _dataService.HasDataAsync(_sessionContext.CurrentUserId ?? 0);
                
                if (hasData)
                {
                    await LoadRealData();
                }
                else
                {
                    ShowNoDataMessage();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi tải dữ liệu: {ex.Message}", "Lỗi", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadRealData()
        {
            var summary = await _dataService.GetFinancialSummaryAsync(_sessionContext.CurrentUserId ?? 0);
            // Load giao dịch gần đây
            var recentTransactions = await _dataService.GetRecentTransactionsAsync(_sessionContext.CurrentUserId ?? 0, 4);
            
            // Load dữ liệu chi tiêu theo danh mục - CHỈ tính tháng hiện tại
            var now = DateTime.Now;
            var startDate = new DateTime(now.Year, now.Month, 1);
            var endDate = new DateTime(now.Year, now.Month, DateTime.DaysInMonth(now.Year, now.Month));
            var categorySpending = await _dataService.GetCategorySpendingByPeriodAsync(_sessionContext.CurrentUserId ?? 0, startDate, endDate);

            // Cập nhật UI với dữ liệu thực
            UpdateUIWithRealData(summary, recentTransactions, categorySpending);
        }

        private void UpdateUIWithRealData(Services.FinancialSummary summary, 
            List<Transaction> transactions, 
            List<Services.CategorySpending> categorySpending)
        {
            // Cập nhật summary cards
            TotalBalanceText.Text = $"{summary.TotalBalance:N0} ₫";
            MonthlyIncomeText.Text = $"{summary.MonthlyIncome:N0} ₫";
            MonthlyExpenseText.Text = $"{summary.MonthlyExpense:N0} ₫";
            SavingsRateText.Text = $"{summary.SavingsRate:F1}%";

            // Cập nhật danh sách giao dịch
            UpdateTransactionList(transactions);

            // Cập nhật thông tin biểu đồ
            UpdateChartInfo(categorySpending);
        }


        private void UpdateTransactionList(List<Transaction> transactions)
        {
            TransactionList.Children.Clear();

            foreach (var transaction in transactions)
            {
                var transactionGrid = CreateTransactionItem(transaction);
                TransactionList.Children.Add(transactionGrid);
            }
        }

        private Grid CreateTransactionItem(Transaction transaction)
        {
            var grid = new Grid();
            grid.Margin = new Thickness(0, 0, 0, 16);

            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0, GridUnitType.Auto) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0, GridUnitType.Auto) });

            // Icon border
            var iconBorder = new Border
            {
                Background = GetCategoryColor(transaction.Category?.Color),
                CornerRadius = new CornerRadius(20),
                Width = 40,
                Height = 40,
                VerticalAlignment = VerticalAlignment.Center
            };

            var icon = new materialDesign::PackIcon
            {
                Kind = GetCategoryIcon(transaction.Category?.Name),
                Width = 20,
                Height = 20,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            iconBorder.Child = icon;
            Grid.SetColumn(iconBorder, 0);

            // Transaction details
            var detailsPanel = new StackPanel
            {
                Margin = new Thickness(16, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            var descriptionText = new TextBlock
            {
                Text = transaction.Description,
                FontSize = 14,
                FontWeight = FontWeights.Medium,
                Foreground = new SolidColorBrush(Color.FromRgb(33, 33, 33))
            };

            var dateText = new TextBlock
            {
                Text = FormatDate(transaction.CreatedAt),
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(117, 117, 117))
            };

            detailsPanel.Children.Add(descriptionText);
            detailsPanel.Children.Add(dateText);
            Grid.SetColumn(detailsPanel, 1);

            // Amount
            var amountText = new TextBlock
            {
                Text = FormatAmount(transaction.Amount, transaction.Type),
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = transaction.Type == TransactionType.Income 
                    ? new SolidColorBrush(Color.FromRgb(76, 175, 80)) 
                    : new SolidColorBrush(Color.FromRgb(244, 67, 54))
            };
            Grid.SetColumn(amountText, 2);

            grid.Children.Add(iconBorder);
            grid.Children.Add(detailsPanel);
            grid.Children.Add(amountText);

            return grid;
        }

        private Brush GetCategoryColor(string? color)
        {
            if (string.IsNullOrEmpty(color))
                return new SolidColorBrush(Color.FromRgb(33, 150, 243));

            try
            {
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
            }
            catch
            {
                return new SolidColorBrush(Color.FromRgb(33, 150, 243));
            }
        }

        private materialDesign::PackIconKind GetCategoryIcon(string? categoryName)
        {
            return categoryName?.ToLower() switch
            {
                "ăn uống" or "food" => materialDesign::PackIconKind.Restaurant,
                "lương" or "salary" => materialDesign::PackIconKind.Work,
                "giao thông" or "transport" => materialDesign::PackIconKind.DirectionsCar,
                "mua sắm" or "shopping" => materialDesign::PackIconKind.Shopping,
                "giải trí" or "entertainment" => materialDesign::PackIconKind.Movie,
                "y tế" or "health" => materialDesign::PackIconKind.MedicalBag,
                "học tập" or "education" => materialDesign::PackIconKind.School,
                "thưởng" or "bonus" => materialDesign::PackIconKind.Gift,
                _ => materialDesign::PackIconKind.CurrencyUsd
            };
        }


        private void UpdateChartInfo(List<Services.CategorySpending> categorySpending)
        {
            ExpenseSeries.Clear();
            
            if (categorySpending.Count > 0)
            {
                ExpenseChart.Visibility = Visibility.Visible;
                var totalAmount = categorySpending.Sum(x => x.Amount);
                var defaultColors = new[]
                {
                    "#2196F3", "#4CAF50", "#FF9800", "#F44336", "#9C27B0", 
                    "#00BCD4", "#8BC34A", "#FF5722", "#607D8B", "#795548"
                };
                
                for (int i = 0; i < categorySpending.Count; i++)
                {
                    var item = categorySpending[i];
                    Color color;
                    
                    try
                    {
                        if (!string.IsNullOrEmpty(item.Color) && item.Color.StartsWith("#"))
                        {
                            color = (Color)ColorConverter.ConvertFromString(item.Color);
                        }
                        else
                        {
                            color = (Color)ColorConverter.ConvertFromString(defaultColors[i % defaultColors.Length]);
                        }
                    }
                    catch
                    {
                        color = (Color)ColorConverter.ConvertFromString(defaultColors[i % defaultColors.Length]);
                    }
                    
                    var skColor = new SKColor(color.R, color.G, color.B);
                    var percentage = totalAmount > 0 ? (item.Amount / totalAmount) * 100 : 0;
                    
                    ExpenseSeries.Add(new PieSeries<double>
                    {
                        Name = item.CategoryName,
                        Values = new double[] { (double)item.Amount },
                        Fill = new SolidColorPaint(skColor),
                        Stroke = new SolidColorPaint(skColor) { StrokeThickness = 2 },
                        InnerRadius = 40
                    });
                }
            }
            else
            {
                ExpenseChart.Visibility = Visibility.Collapsed;

            }
        }

        private void ShowNoDataMessage()
        {
            TotalBalanceText.Text = "₫0";
            MonthlyIncomeText.Text = "₫0";
            MonthlyExpenseText.Text = "₫0";
            SavingsRateText.Text = "0%";
            ExpenseChart.Visibility = Visibility.Collapsed;
        }


        private string FormatDate(DateTime date)
        {
            var now = DateTime.Now;
            var today = now.Date;
            var yesterday = today.AddDays(-1);

            if (date.Date == today)
            {
                return $"Hôm nay, {date:HH:mm}";
            }
            else if (date.Date == yesterday)
            {
                return $"Hôm qua, {date:HH:mm}";
            }
            else if (date.Year == now.Year)
            {
                return $"{date:dd/MM}, {date:HH:mm}";
            }
            else
            {
                return $"{date:dd/MM/yyyy}, {date:HH:mm}";
            }
        }

        private string FormatAmount(decimal amount, TransactionType type)
        {
            var prefix = type == TransactionType.Income ? "+" : "-";
            return $"{prefix}{amount:N0} ₫";
        }
    }
}