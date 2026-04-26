using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using WpfApp3.Services;

namespace WpfApp3.Pages
{
    public partial class LoginPage : Page
    {
        private readonly IAuthenticationService _authService;
        private readonly INavigationService _navigationService;
        private readonly IServiceProvider _serviceProvider;

        public LoginPage(IAuthenticationService authService, INavigationService navigationService, IServiceProvider serviceProvider)
        {
            InitializeComponent();
            _authService = authService;
            _navigationService = navigationService;
            _serviceProvider = serviceProvider;
            UsernameTextBox.Focus();
        }

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            var username = UsernameTextBox.Text.Trim();
            var password = PasswordBox.Password;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                ShowError("Vui lòng nhập đầy đủ thông tin đăng nhập.");
                return;
            }

            try
            {
                LoginButton.IsEnabled = false;
                LoginButton.Content = "ĐANG ĐĂNG NHẬP...";

                var success = await _authService.LoginAsync(username, password);
                
                if (success)
                {
                    var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
                    mainWindow.Show();
                    var loginWindow = Window.GetWindow(this);
                    loginWindow?.Close();
                }
                else
                {
                    ShowError("Tên đăng nhập hoặc mật khẩu không đúng.");
                }
            }
            catch (Exception ex)
            {
                ShowError($"Lỗi đăng nhập: {ex.Message}");
            }
            finally
            {
                LoginButton.IsEnabled = true;
                LoginButton.Content = "ĐĂNG NHẬP";
            }
        }

        private void RegisterLinkButton_Click(object sender, RoutedEventArgs e)
        {
            var registerPage = _serviceProvider.GetRequiredService<RegisterPage>();
            NavigationService?.Navigate(registerPage);
        }

        private void ShowError(string message)
        {
            ErrorMessageTextBlock.Text = message;
            ErrorMessageTextBlock.Visibility = Visibility.Visible;
        }

        private void HideError()
        {
            ErrorMessageTextBlock.Visibility = Visibility.Collapsed;
        }

        private void UsernameTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            HideError();
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            HideError();
        }

        private void TextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                LoginButton_Click(sender, e);
            }
        }

        private void PasswordBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                LoginButton_Click(sender, e);
            }
        } 
    }
}