using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using WpfApp3.Pages;

namespace WpfApp3
{
    public partial class LoginWindow : Window
    {
        private readonly LoginPage _loginPage;

        public LoginWindow(LoginPage loginPage)
        {
            InitializeComponent();
            _loginPage = loginPage;
            MainFrame.Navigate(_loginPage);
        }
    }
}






