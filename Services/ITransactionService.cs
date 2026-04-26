using WpfApp3.Models;

namespace WpfApp3.Services
{
    public interface ITransactionService
    {
        Task<List<Transaction>> GetTransactionsAsync(int userId, TransactionType? type = null);
        Task<Transaction?> GetTransactionByIdAsync(int transactionId, int userId);
        Task<(bool Success, string Message)> CreateTransactionAsync(Transaction transaction, int userId);
        Task<(bool Success, string Message)> UpdateTransactionAsync(int transactionId, decimal amount, string description, int categoryId, DateTime date, int userId);
        Task<(bool Success, string Message)> DeleteTransactionAsync(int transactionId, int userId);
        Task<(bool CanAfford, string Message)> ValidateExpenseTransactionAsync(decimal amount, int categoryId, int userId, DateTime date);
        Task<(bool CanAfford, string Message)> ValidateExpenseTransactionUpdateAsync(int transactionId, decimal newAmount, int newCategoryId, int userId, DateTime newDate);
    }
}











