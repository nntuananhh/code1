using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System.IO;

namespace WpfApp3.Data
{
    public class ExpenseDbContextFactory : IDesignTimeDbContextFactory<ExpenseDbContext>
    {
        public ExpenseDbContext CreateDbContext(string[] args)
        {
            IConfigurationRoot configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json")
                .Build();

            var connectionString = configuration.GetConnectionString("Connection");
            var optionsBuilder = new DbContextOptionsBuilder<ExpenseDbContext>();
            optionsBuilder.UseSqlServer(connectionString);
            return new ExpenseDbContext(optionsBuilder.Options);
        }
    }
}