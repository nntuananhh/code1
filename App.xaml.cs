using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WpfApp3.Data;
using WpfApp3.Services;

namespace WpfApp3
{
    public partial class App : Application
    {
        public static IServiceProvider ServiceProvider { get; private set; } = null!;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var configuration = new ConfigurationBuilder()
                .SetBasePath(System.AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            var services = new ServiceCollection();
            services.AddDbContextFactory<ExpenseDbContext>(options =>
                options.UseSqlServer(configuration.GetConnectionString("Connection")));

            services.AddScoped<IDataService, DataService>();
            services.AddScoped<IAuthenticationService, AuthenticationService>();
            services.AddSingleton<INavigationService, NavigationService>();
            services.AddSingleton<ISessionContext, SessionContext>();
            services.AddScoped<IBudgetService, BudgetService>();
            services.AddScoped<ITransactionService, TransactionService>();
            services.AddScoped<ICategoryService, CategoryService>();
            services.AddScoped<IGoalService, GoalService>();

            // pages 
            services.AddTransient<WpfApp3.Pages.DashboardPage>();
            services.AddTransient<WpfApp3.Pages.IncomePage>();
            services.AddTransient<WpfApp3.Pages.ExpensePage>();
            services.AddTransient<WpfApp3.Pages.StatisticsPage>();
            services.AddTransient<WpfApp3.Pages.BudgetPage>();
            services.AddTransient<WpfApp3.Pages.GoalsPage>();
            services.AddTransient<WpfApp3.Pages.SettingsPage>();
            services.AddTransient<WpfApp3.Pages.LoginPage>();
            services.AddTransient<WpfApp3.Pages.RegisterPage>();
            
            // Windows
            services.AddTransient<MainWindow>();
            services.AddTransient<LoginWindow>();

            //dialogs
            services.AddTransient<WpfApp3.Dialogs.AddBudgetDialog>();
            services.AddTransient<WpfApp3.Dialogs.EditBudgetDialog>();
            services.AddTransient<WpfApp3.Dialogs.AddTransactionDialog>();
            services.AddTransient<WpfApp3.Dialogs.EditTransactionDialog>();
            services.AddTransient<WpfApp3.Dialogs.AddGoalDialog>();
            services.AddTransient<WpfApp3.Dialogs.EditGoalDialog>();
            services.AddTransient<WpfApp3.Dialogs.AddMoneyToGoalDialog>();
            services.AddTransient<WpfApp3.Dialogs.AddCategoryDialog>();
            services.AddTransient<WpfApp3.Dialogs.EditCategoryDialog>();

            services.AddTransient<Func<WpfApp3.Dialogs.AddBudgetDialog>>(sp => () => sp.GetRequiredService<WpfApp3.Dialogs.AddBudgetDialog>());
            services.AddTransient<Func<WpfApp3.Dialogs.EditBudgetDialog>>(sp => () => sp.GetRequiredService<WpfApp3.Dialogs.EditBudgetDialog>());
            services.AddTransient<Func<WpfApp3.Dialogs.AddTransactionDialog>>(sp => () => sp.GetRequiredService<WpfApp3.Dialogs.AddTransactionDialog>());
            services.AddTransient<Func<WpfApp3.Dialogs.EditTransactionDialog>>(sp => () => sp.GetRequiredService<WpfApp3.Dialogs.EditTransactionDialog>());
            services.AddTransient<Func<WpfApp3.Dialogs.AddGoalDialog>>(sp => () => sp.GetRequiredService<WpfApp3.Dialogs.AddGoalDialog>());
            services.AddTransient<Func<WpfApp3.Dialogs.EditGoalDialog>>(sp => () => sp.GetRequiredService<WpfApp3.Dialogs.EditGoalDialog>());
            services.AddTransient<Func<WpfApp3.Dialogs.AddMoneyToGoalDialog>>(sp => () => sp.GetRequiredService<WpfApp3.Dialogs.AddMoneyToGoalDialog>());
            services.AddTransient<Func<WpfApp3.Dialogs.AddCategoryDialog>>(sp => () => sp.GetRequiredService<WpfApp3.Dialogs.AddCategoryDialog>());
            services.AddTransient<Func<WpfApp3.Dialogs.EditCategoryDialog>>(sp => () => sp.GetRequiredService<WpfApp3.Dialogs.EditCategoryDialog>());

            ServiceProvider = services.BuildServiceProvider();

            Task.Run(async () =>
            {
                try
                {
                    var factory = ServiceProvider.GetRequiredService<IDbContextFactory<ExpenseDbContext>>();
                    await using var context = await factory.CreateDbContextAsync();
                    await context.Database.MigrateAsync();
                }
                catch
                {
                    try
                    {
                        var factory = ServiceProvider.GetRequiredService<IDbContextFactory<ExpenseDbContext>>();
                        await using var context = await factory.CreateDbContextAsync();
                        context.Database.EnsureCreated();
                    }
                    catch
                    {
                    }
                }
            });
            var loginWindow = ServiceProvider.GetRequiredService<LoginWindow>();
            MainWindow = loginWindow;
            loginWindow.Show();
        }
    }
}