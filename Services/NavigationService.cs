using Microsoft.Extensions.DependencyInjection;
using System;
using System.Windows.Controls;

namespace WpfApp3.Services
{
    public class NavigationService : INavigationService
    {
        private readonly IServiceProvider _serviceProvider;

        public NavigationService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public Page GetPage(string pageName)
        {
            return pageName switch
            {
                "Income" => _serviceProvider.GetRequiredService<WpfApp3.Pages.IncomePage>(),
                "Expense" => _serviceProvider.GetRequiredService<WpfApp3.Pages.ExpensePage>(),
                "Statistics" => _serviceProvider.GetRequiredService<WpfApp3.Pages.StatisticsPage>(),
                "Budget" => _serviceProvider.GetRequiredService<WpfApp3.Pages.BudgetPage>(),
                "Goals" => _serviceProvider.GetRequiredService<WpfApp3.Pages.GoalsPage>(),
                "Settings" => _serviceProvider.GetRequiredService<WpfApp3.Pages.SettingsPage>(),
                _ => _serviceProvider.GetRequiredService<WpfApp3.Pages.DashboardPage>()
            };
        }
    }
}


