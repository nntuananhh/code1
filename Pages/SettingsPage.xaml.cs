using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using WpfApp3.Dialogs;
using WpfApp3.Models;
using WpfApp3.Services;
using Microsoft.Extensions.DependencyInjection;

namespace WpfApp3.Pages
{
    public partial class SettingsPage : Page
    {
        private readonly ISessionContext _sessionContext;
        private readonly IAuthenticationService _authService;
        private readonly IDataService _dataService;
        private readonly IServiceProvider _serviceProvider;
        private User? _currentUser;
        private string? _selectedImagePath;

        public SettingsPage(ISessionContext sessionContext, 
            IAuthenticationService authService, IDataService dataService, IServiceProvider serviceProvider)
        {
            InitializeComponent();
            _sessionContext = sessionContext;
            _authService = authService;
            _dataService = dataService;
            _serviceProvider = serviceProvider;
            LoadUserInfo();
        }

        private void LoadUserInfo()
        {
            try
            {
                _currentUser = _sessionContext.CurrentUser;
                if (_currentUser != null)
                {
                    FullNameTextBox.Text = _currentUser.FullName ?? string.Empty;
                    EmailTextBox.Text = _currentUser.Email;
                    UsernameTextBox.Text = _currentUser.Username;
                    CreatedDateTextBox.Text = _currentUser.CreatedAt.ToString("dd/MM/yyyy HH:mm");
                    DisplayAvatar(_currentUser.Avatar);
                }
                else
                {
                    FullNameTextBox.Text = EmailTextBox.Text =
                    UsernameTextBox.Text = CreatedDateTextBox.Text = "Không xác định";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi tải thông tin người dùng: {ex.Message}",
                    "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DisplayAvatar(string? imagePath)
        {
            if (!string.IsNullOrEmpty(imagePath) && File.Exists(imagePath))
            {
                try
                {
                    byte[] imageBytes = File.ReadAllBytes(imagePath);
                    using var imageStream = new MemoryStream(imageBytes);
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.StreamSource = imageStream;
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    AvatarImage.Source = bitmap;
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

        private void RefreshMainWindowAvatar()
        {
            var mainWindow = Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
            mainWindow?.LoadUserInfo();
        }

        private async void ResetDataButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "CẢNH BÁO: Bạn có chắc chắn muốn xóa TẤT CẢ dữ liệu?\n\n" +
                "Hành động này sẽ xóa:\n" +
                "• Tất cả giao dịch (thu nhập & chi tiêu)\n" +
                "• Tất cả hũ chi tiêu\n" +
                "• Tất cả mục tiêu\n" +
                "• Tất cả danh mục tùy chỉnh\n\n" +
                "Hành động này KHÔNG THỂ HOÀN TÁC!",
                "Xác nhận Reset Data",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            var confirm = MessageBox.Show(
                "Bạn có THỰC SỰ chắc chắn muốn xóa tất cả dữ liệu?\n\n" +
                "Nhấn 'Yes' để tiếp tục, 'No' để hủy.",
                "Xác nhận lần cuối",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                var userId = _sessionContext.CurrentUserId ?? 0;

                // Sử dụng DataService để xóa dữ liệu
                var success = await _dataService.ResetUserDataAsync(userId);
                
                if (!success)
                {
                    MessageBox.Show("Lỗi khi reset data. Vui lòng thử lại.",
                        "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                MessageBox.Show("Đã xóa tất cả dữ liệu thành công!\n\nỨng dụng sẽ được làm mới.",
                    "Reset Data Thành công", MessageBoxButton.OK, MessageBoxImage.Information);

                if (Application.Current.MainWindow is MainWindow mainWindow)
                {
                    mainWindow.NavigateToPage("Dashboard");
                    mainWindow.UpdateSidebarData();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi reset data: {ex.Message}",
                    "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UploadAvatarButton_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "Chọn ảnh đại diện",
                Filter = "Ảnh (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp|Tất cả files (*.*)|*.*",
                FilterIndex = 1
            };

            if (openFileDialog.ShowDialog() == true)
            {
                _selectedImagePath = openFileDialog.FileName;
                DisplayAvatar(_selectedImagePath);
            }
        }

        private async void SaveProfileButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentUser == null)
            {
                MessageBox.Show("Không tìm thấy thông tin người dùng.",
                    "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                SaveProfileButton.IsEnabled = false;
                SaveProfileButton.Content = "Đang lưu...";

                var fullName = FullNameTextBox.Text.Trim();
                var email = EmailTextBox.Text.Trim();

                if (!string.IsNullOrEmpty(email) && !IsValidEmail(email))
                {
                    MessageBox.Show("Địa chỉ email không hợp lệ.",
                        "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string? avatarPath = _currentUser.Avatar;

                if (!string.IsNullOrEmpty(_selectedImagePath))
                    avatarPath = await CopyImageToAppDirectory(_selectedImagePath, _currentUser.Id);
                var success = await _authService.UpdateProfileAsync(fullName, email, avatarPath);

                if (success)
                {
                    RefreshMainWindowAvatar();
                    MessageBox.Show("Cập nhật thông tin thành công!",
                        "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadUserInfo();
                }
                else
                {
                    MessageBox.Show("Có lỗi xảy ra khi cập nhật thông tin. Vui lòng thử lại.",
                        "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Có lỗi xảy ra: {ex.Message}",
                    "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                SaveProfileButton.IsEnabled = true;
                SaveProfileButton.Content = "Lưu thay đổi";
            }
        }

        private Task<string> CopyImageToAppDirectory(string sourcePath, int userId)
        {
            if (!File.Exists(sourcePath))
                throw new FileNotFoundException($"Không tìm thấy file nguồn: {sourcePath}");

            var appDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Avatars");
            Directory.CreateDirectory(appDir);

            var extension = Path.GetExtension(sourcePath);
            var fileName = $"user_{userId}_{DateTime.Now:yyyyMMddHHmmss}{extension}";
            var destination = Path.Combine(appDir, fileName);

            File.Copy(sourcePath, destination, true);
            return Task.FromResult(destination);
        }

        private bool IsValidEmail(string email)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }

        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Bạn có chắc chắn muốn đăng xuất?",
                "Xác nhận đăng xuất", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            _authService.Logout();
            var loginWindow = _serviceProvider.GetRequiredService<LoginWindow>();
            loginWindow.Show();

            (Application.Current.MainWindow as MainWindow)?.Close();
        }
    }
}
