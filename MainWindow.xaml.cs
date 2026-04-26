using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using WpfApp3.Dialogs;
using WpfApp3.Models;
using WpfApp3.Pages;
using WpfApp3.Services;
using Microsoft.Extensions.DependencyInjection;

namespace WpfApp3
{
    public partial class MainWindow : Window
    {
        private readonly INavigationService _navigationService;
        private readonly ISessionContext _sessionContext;
        private readonly IAuthenticationService _authService;
        private readonly IServiceProvider _serviceProvider;
        private readonly Func<AddTransactionDialog> _addTransactionDialogFactory;

        public MainWindow(INavigationService navigationService, ISessionContext sessionContext, 
            IAuthenticationService authService, IServiceProvider serviceProvider,
            Func<AddTransactionDialog> addTransactionDialogFactory)
        {
            InitializeComponent();
            _navigationService = navigationService;
            _sessionContext = sessionContext;
            _authService = authService;
            _serviceProvider = serviceProvider;
            _addTransactionDialogFactory = addTransactionDialogFactory;
            LoadUserInfo();
            NavigateToPage("Dashboard");
        }

        public void UpdateSidebarData()
        {
            if (MainFrame.Content is not DashboardPage) return;
        }

        private void NavigationButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button) return;

            DashboardButton.Style = (Style)FindResource("SidebarButtonStyle");
            IncomeButton.Style = (Style)FindResource("SidebarButtonStyle");
            ExpenseButton.Style = (Style)FindResource("SidebarButtonStyle");
            StatisticsButton.Style = (Style)FindResource("SidebarButtonStyle");
            BudgetButton.Style = (Style)FindResource("SidebarButtonStyle");
            GoalsButton.Style = (Style)FindResource("SidebarButtonStyle");
            SettingsButton.Style = (Style)FindResource("SidebarButtonStyle");

            button.Style = (Style)FindResource("ActiveSidebarButtonStyle");
            NavigateToPage(button.Tag?.ToString() ?? "Dashboard");
        }

        public void NavigateToPage(string pageName)
        {
            PageTitle.Text = pageName switch
            {
                "Income" => "Thu nhập",
                "Expense" => "Chi tiêu",
                "Statistics" => "Thống kê",
                "Budget" => "Hũ chi tiêu",
                "Goals" => "Mục tiêu",
                "Settings" => "Cài đặt",
                "Categories" => "Danh mục",
                _ => "Dashboard"
            };

            var page = _navigationService.GetPage(pageName);
            MainFrame.Navigate(page);
        }

        public void LoadUserInfo()
        {
            var user = _sessionContext.CurrentUser;
            if (user is null) return;

            UserNameText.Text = user.FullName ?? user.Username;
            UserEmailText.Text = user.Email;
            LoadSidebarAvatar(user);
        }

        private void LoadSidebarAvatar(User user)
        {
            if (AvatarImage is null || DefaultAvatarIcon is null) return;

            if (!string.IsNullOrEmpty(user.Avatar) && File.Exists(user.Avatar))
            {
                try
                {
                    var bytes = File.ReadAllBytes(user.Avatar);
                    using var ms = new MemoryStream(bytes);
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.StreamSource = ms;
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    bmp.Freeze();

                    AvatarImage.Source = bmp;
                    DefaultAvatarIcon.Visibility = Visibility.Collapsed;
                    return;
                }
                catch
                {
                    // Không thể load avatar, hiển thị icon mặc định
                }
            }

            AvatarImage.Source = null;
            DefaultAvatarIcon.Visibility = Visibility.Visible;
        }

        private void LogoutButton_Click(object sender, RoutedEventArgs e) => PerformLogout();
        private void PerformLogout()
        {
            var result = MessageBox.Show("Bạn có chắc chắn muốn đăng xuất?", "Xác nhận đăng xuất",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            _authService.Logout();
            var loginWindow = _serviceProvider.GetRequiredService<LoginWindow>();
            loginWindow.Show();
            Close();
        }

        private void AddIncome_Click(object sender, RoutedEventArgs e) => HandleAddTransaction(TransactionType.Income);

        private void AddExpense_Click(object sender, RoutedEventArgs e) => HandleAddTransaction(TransactionType.Expense);

        private void HandleAddTransaction(TransactionType type)
        {
            var dialog = _addTransactionDialogFactory();
            dialog.Initialize(type);
            dialog.Owner = this;
            var result = dialog.ShowDialog();

            if (result != true) return;

            UpdateSidebarData();
            if (MainFrame.Content is DashboardPage d) d.RefreshData();
            else if (MainFrame.Content is IncomePage i) i.LoadData();
            else if (MainFrame.Content is ExpensePage e) e.LoadData();
            else if (MainFrame.Content is StatisticsPage s) s.LoadData();
            else if (MainFrame.Content is BudgetPage b) b.LoadData();
            else if (MainFrame.Content is GoalsPage g) g.LoadData();
        }
    }
}
