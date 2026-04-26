using WpfApp3.Models;

namespace WpfApp3.Services
{
    public interface IDataService
    {
        Task<bool> HasDataAsync(int userId);
        Task<List<CategorySpending>> GetCategorySpendingByPeriodAsync(int userId, DateTime startDate, DateTime endDate);
        Task<List<Transaction>> GetRecentTransactionsAsync(int userId, int count = 10);
        Task<List<Transaction>> GetTransactionsByPeriodAsync(int userId, DateTime startDate, DateTime endDate);
        Task<FinancialSummary> GetFinancialSummaryAsync(int userId);
        Task<decimal> GetMonthlyIncomeAsync(int userId, int? month = null, int? year = null);
        Task<decimal> GetMonthlyExpenseAsync(int userId, int? month = null, int? year = null);
        Task<decimal> GetTotalIncomeByPeriodAsync(int userId, DateTime startDate, DateTime endDate);
        Task<decimal> GetTotalExpenseByPeriodAsync(int userId, DateTime startDate, DateTime endDate);
        Task<decimal> GetTotalIncomeAsync(int userId);
        Task<decimal> GetTotalExpenseAsync(int userId);
        Task<decimal> GetAverageMonthlyIncomeAsync(int userId);
        Task<decimal> GetAverageMonthlyExpenseAsync(int userId);
        Task<bool> ResetUserDataAsync(int userId);
    }
}


