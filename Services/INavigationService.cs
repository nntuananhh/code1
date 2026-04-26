using System.Windows.Controls;

namespace WpfApp3.Services
{
    public interface INavigationService
    {
        Page GetPage(string pageName);
    }
}




