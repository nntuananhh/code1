using WpfApp3.Models;

namespace WpfApp3.Services
{
    public interface IBudgetService
    {
        Task<List<Budget>> GetBudgetsAsync(int userId);
        Task<Budget?> GetBudgetByIdAsync(int budgetId, int userId);
        Task<Budget?> GetBudgetByCategoryAndDateAsync(int categoryId, int userId, DateTime date);
        Task<bool> CreateBudgetAsync(Budget budget, int userId);
        Task<bool> UpdateBudgetAsync(int budgetId, decimal newAmount, DateTime startDate, DateTime endDate, bool isActive, int userId);
        Task<(bool Success, string Message)> DeleteBudgetAsync(int budgetId, int userId);
        Task<bool> UpdateBudgetSpentAmountAsync(int categoryId, int userId, decimal amount, DateTime? transactionDate = null);
        Task<BudgetAlertResult> CheckBudgetAlertsAsync(int userId);
        Task<decimal> CalculateActualSpentAmountAsync(int budgetId);
    }

    public class BudgetAlertResult
    {
        public List<string> OverBudgetItems { get; set; } = new();
        public List<string> WarningItems { get; set; } = new();
        public bool HasAlerts => OverBudgetItems.Any() || WarningItems.Any();
    }
}











