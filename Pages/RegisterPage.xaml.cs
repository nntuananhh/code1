using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using WpfApp3.Services;

namespace WpfApp3.Pages
{
    public partial class RegisterPage : Page
    {
        private readonly IAuthenticationService _authService;

        public RegisterPage(IAuthenticationService authService)
        {
            InitializeComponent();
            _authService = authService;
            FullNameTextBox.Focus();
        }

        private async void RegisterButton_Click(object sender, RoutedEventArgs e)
        {
            var fullName = FullNameTextBox.Text.Trim();
            var username = UsernameTextBox.Text.Trim();
            var email = EmailTextBox.Text.Trim();
            var password = PasswordBox.Password;
            var confirmPassword = ConfirmPasswordBox.Password;

            // Validation
            if (string.IsNullOrEmpty(username))
            {
                ShowError("Vui lòng nhập tên đăng nhập.");
                return;
            }

            if (username.Contains(' ') || username.Contains('\t'))
            {
                ShowError("Tên đăng nhập không được chứa khoảng trắng.");
                return;
            }

            if (string.IsNullOrEmpty(email))
            {
                ShowError("Vui lòng nhập email.");
                return;
            }

            if (!IsValidEmail(email))
            {
                ShowError("Email không hợp lệ.");
                return;
            }

            if (string.IsNullOrEmpty(password))
            {
                ShowError("Vui lòng nhập mật khẩu.");
                return;
            }

            if (password.Length < 6)
            {
                ShowError("Mật khẩu phải có ít nhất 6 ký tự.");
                return;
            }

            if (password != confirmPassword)
            {
                ShowError("Mật khẩu xác nhận không khớp.");
                return;
            }

            try
            {
                RegisterButton.IsEnabled = false;
                RegisterButton.Content = "ĐANG ĐĂNG KÝ...";

                var success = await _authService.RegisterAsync(username, email, password, 
                    string.IsNullOrEmpty(fullName) ? null : fullName);
                
                if (success)
                {
                    MessageBox.Show("Đăng ký thành công! Vui lòng đăng nhập.", 
                        "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                    NavigationService?.GoBack();
                }
                else
                {
                    ShowError("Tên đăng nhập hoặc email đã tồn tại.");
                }
            }
            catch (Exception ex)
            {
                ShowError($"Lỗi đăng ký: {ex.Message}");
            }
            finally
            {
                RegisterButton.IsEnabled = true;
                RegisterButton.Content = "ĐĂNG KÝ";
            }
        }

        private void LoginLinkButton_Click(object sender, RoutedEventArgs e)
        {
            NavigationService?.GoBack();
        }

        private void ShowError(string message)
        {
            ErrorMessageTextBlock.Text = message;
            ErrorMessageTextBlock.Visibility = Visibility.Visible;
        }

        private bool IsValidEmail(string email)
        {
            var emailRegex = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$");
            return emailRegex.IsMatch(email);
        }

        private void TextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                RegisterButton_Click(sender, e);
            }
        }

        private void PasswordBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                RegisterButton_Click(sender, e);
            }
        }
    }
}





