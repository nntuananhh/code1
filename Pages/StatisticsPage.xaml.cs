using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WpfApp3.Models;
using WpfApp3.Services;
using Microsoft.Extensions.DependencyInjection;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.WPF;
using SkiaSharp;

namespace WpfApp3.Pages
{
    public partial class StatisticsPage : Page, INotifyPropertyChanged
    {
        private readonly IDataService _dataService;
        private readonly ISessionContext _sessionContext;
        private string _currentPeriod = "ThisMonth";
        private ISeries[] _categorySeries = Array.Empty<ISeries>();
        private ISeries[] _monthlyTrendSeries = Array.Empty<ISeries>();
        private string[] _trendMonthLabels = Array.Empty<string>();
        private LiveChartsCore.Kernel.Sketches.ICartesianAxis[] _xAxes = Array.Empty<LiveChartsCore.Kernel.Sketches.ICartesianAxis>();
        private LiveChartsCore.Kernel.Sketches.ICartesianAxis[] _yAxes = Array.Empty<LiveChartsCore.Kernel.Sketches.ICartesianAxis>();

        public event PropertyChangedEventHandler? PropertyChanged;

        public ISeries[] CategorySeries
        {
            get => _categorySeries;
            set
            {
                _categorySeries = value;
                OnPropertyChanged();
            }
        }

        public ISeries[] MonthlyTrendSeries
        {
            get => _monthlyTrendSeries;
            set
            {
                _monthlyTrendSeries = value;
                OnPropertyChanged();
            }
        }

        public string[] TrendMonthLabels
        {
            get => _trendMonthLabels;
            set
            {
                _trendMonthLabels = value;
                OnPropertyChanged();
            }
        }

        public LiveChartsCore.Kernel.Sketches.ICartesianAxis[] XAxes
        {
            get => _xAxes;
            set
            {
                _xAxes = value;
                OnPropertyChanged();
            }
        }

        public LiveChartsCore.Kernel.Sketches.ICartesianAxis[] YAxes
        {
            get => _yAxes;
            set
            {
                _yAxes = value;
                OnPropertyChanged();
            }
        }

        public Func<double, string> YFormatter { get; set; }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public StatisticsPage(IDataService dataService, ISessionContext sessionContext)
        {
            InitializeComponent();
            _dataService = dataService;
            _sessionContext = sessionContext;
            YFormatter = value => $"{value:N0} ₫";
            DataContext = this;
            UpdateCurrentPeriodDisplay();
            
            LoadData();
        }

        public async void LoadData()
        {
            try
            {
                await LoadPeriodData(_currentPeriod);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi tải dữ liệu: {ex.Message}", "Lỗi", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadPeriodData(string period)
        {
            var now = DateTime.Now;
            DateTime startDate;
            DateTime endDate;
            switch (period)
            {
                case "ThisYear":
                    startDate = new DateTime(now.Year, 1, 1);
                    endDate = new DateTime(now.Year, 12, 31);
                    break;
                case "Last6Months":
                    // Lấy từ đầu tháng cách đây 5 tháng đến cuối tháng hiện tại
                    startDate = new DateTime(now.AddMonths(-5).Year, now.AddMonths(-5).Month, 1);
                    endDate = new DateTime(now.Year, now.Month, DateTime.DaysInMonth(now.Year, now.Month));
                    break;
                case "ThisMonth":
                default:
                    startDate = new DateTime(now.Year, now.Month, 1);
                    endDate = new DateTime(now.Year, now.Month, DateTime.DaysInMonth(now.Year, now.Month));
                    break;
            }

            // Load transactions for the period (để hiển thị trong charts và lists)
            var userId = _sessionContext?.CurrentUserId ?? 0;
            var transactions = await _dataService.GetTransactionsByPeriodAsync(userId, startDate, endDate);
            var incomes = transactions.Where(t => t.Type == TransactionType.Income).ToList();
            var expenses = transactions.Where(t => t.Type == TransactionType.Expense).ToList();

            decimal totalIncome;
            decimal totalExpense;
            if (period == "ThisMonth")
            {
                totalIncome = await _dataService.GetMonthlyIncomeAsync(userId);
                totalExpense = await _dataService.GetMonthlyExpenseAsync(userId);
            }
            else
            {
                totalIncome = await _dataService.GetTotalIncomeByPeriodAsync(userId, startDate, endDate);
                totalExpense = await _dataService.GetTotalExpenseByPeriodAsync(userId, startDate, endDate);
            }
            
            UpdateSummaryCards(totalIncome, totalExpense);

            // Load category spending từ service
            var categorySpending = await _dataService.GetCategorySpendingByPeriodAsync(userId, startDate, endDate);

            // Update charts
            await UpdateCharts(incomes, expenses, categorySpending, startDate, endDate);

            // Update top categories từ categorySpending và expenses
            UpdateTopCategories(categorySpending, expenses);
        }

        private void UpdateSummaryCards(decimal totalIncome, decimal totalExpense)
        {
            var netIncome = totalIncome - totalExpense;
            var savingsRate = totalIncome > 0 ? (netIncome / totalIncome) * 100 : 0;

            TotalIncomeText.Text = $"{totalIncome:N0} ₫";
            TotalExpenseText.Text = $"{totalExpense:N0} ₫";
            SavingsRateText.Text = $"{savingsRate:F1}%";
        }

        private async Task UpdateCharts(List<Transaction> incomes, List<Transaction> expenses, List<CategorySpending> categorySpending, DateTime startDate, DateTime endDate)
        {
            // Category Spending Chart - sử dụng dữ liệu từ service
            UpdateCategoryChart(categorySpending);

            // Monthly Trend Chart
            await UpdateMonthlyTrendChart(incomes, expenses, startDate, endDate);
        }

        private void UpdateCategoryChart(List<CategorySpending> categorySpending)
        {
            // Sử dụng dữ liệu từ service (đã được filter !IsAllocation)
            var topCategories = categorySpending
                .OrderByDescending(x => x.Amount)
                .Take(8) // Top 8 categories
                .ToList();

            var series = new List<ISeries>();

            foreach (var item in topCategories)
            {
                var colorCode = string.IsNullOrWhiteSpace(item.Color) ? "#9E9E9E" : item.Color;
                var paint = new SolidColorPaint(SKColor.Parse(colorCode));
                series.Add(new PieSeries<double>
                {
                    Name = item.CategoryName ?? "Không có danh mục",
                    Values = new double[] { (double)item.Amount },
                    Fill = paint,
                    InnerRadius = 40
                });
            }
            
            CategorySeries = series.ToArray();
        }

        private async Task UpdateMonthlyTrendChart(List<Transaction> incomes, List<Transaction> expenses, DateTime startDate, DateTime endDate)
        {
            // Group by month for trend - sử dụng List để đảm bảo thứ tự
            var monthlyTrend = new List<(string month, decimal income, decimal expense, decimal net)>();
            
            // Đảm bảo date luôn là ngày 1 của tháng
            var currentMonth = new DateTime(startDate.Year, startDate.Month, 1);
            var lastMonth = new DateTime(endDate.Year, endDate.Month, 1);
            var userId = _sessionContext?.CurrentUserId ?? 0;
            
            while (currentMonth <= lastMonth)
            {
                var monthKey = $"{currentMonth.Month}/{currentMonth.Year}"; // Hiển thị "X/YYYY"
                var monthIncome = await _dataService.GetMonthlyIncomeAsync(userId, currentMonth.Month, currentMonth.Year);
                var monthExpense = await _dataService.GetMonthlyExpenseAsync(userId, currentMonth.Month, currentMonth.Year);
                var netIncome = monthIncome - monthExpense;
                monthlyTrend.Add((monthKey, monthIncome, monthExpense, netIncome));
                
                currentMonth = currentMonth.AddMonths(1);
            }

            TrendMonthLabels = monthlyTrend.Select(m => m.month).ToArray();

            // Cấu hình XAxes để hiển thị labels
            XAxes = new LiveChartsCore.Kernel.Sketches.ICartesianAxis[]
            {
                new Axis
                {
                    Name = "Tháng",
                    Labels = TrendMonthLabels,
                    LabelsRotation = 0
                }
            };

            // Cấu hình YAxes
            YAxes = new LiveChartsCore.Kernel.Sketches.ICartesianAxis[]
            {
                new Axis
                {
                    Name = "Số tiền (₫)",
                    Labeler = YFormatter,
                    MinLimit = 0
                }
            };

            var series = new List<ISeries>
            {
                // Thu nhập
                new LineSeries<double>
                {
                    Name = "Thu nhập",
                    Values = monthlyTrend.Select(m => (double)m.income).ToArray(),
                    Stroke = new SolidColorPaint(SKColor.Parse("#81C784")),
                    Fill = new SolidColorPaint(SKColor.Parse("#81C784").WithAlpha(51)),
                    GeometrySize = 6,
                    DataLabelsPaint = null
                },
                // Chi tiêu
                new LineSeries<double>
                {
                    Name = "Chi tiêu",
                    Values = monthlyTrend.Select(m => (double)m.expense).ToArray(),
                    Stroke = new SolidColorPaint(SKColor.Parse("#EF9A9A")),
                    Fill = new SolidColorPaint(SKColor.Parse("#EF9A9A").WithAlpha(51)),
                    GeometrySize = 6,
                    DataLabelsPaint = null
                }
            };

            MonthlyTrendSeries = series.ToArray();
        }

        private void UpdateTopCategories(List<CategorySpending> categorySpending, List<Transaction> expenses)
        {
            // Sử dụng dữ liệu từ service (đã được filter !IsAllocation)
            var topCategories = categorySpending
                .OrderByDescending(x => x.Amount)
                .Take(10)
                .ToList();

            var totalExpense = categorySpending.Sum(c => c.Amount);

            // Tính số lượng giao dịch cho mỗi category từ danh sách expenses (group theo CategoryName)
            var transactionCountsByCategoryName = expenses
                .Where(e => e.Category != null)
                .GroupBy(e => e.Category!.Name)
                .ToDictionary(g => g.Key, g => g.Count());

            var topCategoriesWithPercentage = topCategories.Select(c =>
            {
                var transactionCount = transactionCountsByCategoryName.ContainsKey(c.CategoryName)
                    ? transactionCountsByCategoryName[c.CategoryName]
                    : 0;

                return new
                {
                    CategoryName = c.CategoryName,
                    Color = c.Color,
                    Amount = c.Amount,
                    TransactionCount = transactionCount,
                    Percentage = totalExpense > 0 ? (c.Amount / totalExpense) * 100 : 0
                };
            }).ToList();

            TopCategoriesListView.ItemsSource = topCategoriesWithPercentage;
        }

        private void PeriodButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                // Reset all button styles
                ThisMonthButton.Style = (Style)FindResource("MaterialDesignFlatButton");
                ThisMonthButton.Background = Brushes.Transparent;
                ThisMonthButton.Foreground = (SolidColorBrush)FindResource("MaterialDesignBody");
                
                ThisYearButton.Style = (Style)FindResource("MaterialDesignFlatButton");
                ThisYearButton.Background = Brushes.Transparent;
                ThisYearButton.Foreground = (SolidColorBrush)FindResource("MaterialDesignBody");

                button.Style = (Style)FindResource("MaterialDesignRaisedButton");
                button.Background = new SolidColorBrush(Color.FromRgb(33, 150, 243)); // Blue color
                button.Foreground = Brushes.White;

                _currentPeriod = button.Tag?.ToString() ?? "ThisMonth";
                UpdateCurrentPeriodDisplay();
                LoadData();
            }
        }

        private void UpdateCurrentPeriodDisplay()
        {
            var now = DateTime.Now;
            var (periodText, descriptionText) = _currentPeriod switch
            {
                "ThisYear" => ($"Năm nay - Năm {now.Year}", 
                              $"Dữ liệu thống kê cho năm {now.Year}"),
                "Last6Months" => ("6 tháng", 
                                 $"Dữ liệu thống kê cho 6 tháng gần đây"),
                "ThisMonth" => ($"Tháng này - Tháng {now.Month}/{now.Year}", 
                               $"Dữ liệu thống kê cho tháng {now.Month}/{now.Year}"),
                _ => ($"Tháng này - Tháng {now.Month}/{now.Year}", 
                      $"Dữ liệu thống kê cho tháng {now.Month}/{now.Year}")
            };

            CurrentPeriodText.Text = periodText;
            PeriodDescriptionText.Text = descriptionText;
        }
    }
}
