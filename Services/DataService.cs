using Microsoft.EntityFrameworkCore;
using WpfApp3.Data;
using WpfApp3.Models;

namespace WpfApp3.Services
{
    public class DataService : IDataService
    {
        private readonly IDbContextFactory<ExpenseDbContext> _dbFactory;

        public DataService(IDbContextFactory<ExpenseDbContext> dbFactory)
        {
            _dbFactory = dbFactory;
        }

        // Kiểm tra dữ liệu
        public async Task<bool> HasDataAsync(int userId)
        {
            await using var context = await _dbFactory.CreateDbContextAsync();
            var transactionCount = await context.Transactions.AsNoTracking().CountAsync(t => t.UserId == userId);
            var budgetCount = await context.Budgets.AsNoTracking().CountAsync(b => b.UserId == userId);
            var goalCount = await context.Goals.AsNoTracking().CountAsync(g => g.UserId == userId);
            return transactionCount > 0 || budgetCount > 0 || goalCount > 0;
        }

        // Biểu đồ chi tiêu theo kỳ (Chỉ tính chi tiêu THẬT)
        public async Task<List<CategorySpending>> GetCategorySpendingByPeriodAsync(int userId, DateTime startDate, DateTime endDate)
        {
            await using var context = await _dbFactory.CreateDbContextAsync();
            var result = await context.Transactions
                .AsNoTracking()
                .Include(t => t.Category)
                .Where(t => t.Type == TransactionType.Expense &&
                            t.UserId == userId &&
                            t.Date >= startDate &&
                            t.Date <= endDate &&
                            !t.IsAllocation) // Chỉ tính chi tiêu thật
                .GroupBy(t => new { t.CategoryId, t.Category!.Name, t.Category!.Color })
                .Select(g => new CategorySpending
                {
                    CategoryName = g.Key.Name ?? "Unknown",
                    Amount = g.Sum(t => t.Amount),
                    Color = g.Key.Color ?? "#2196F3"
                })
                .OrderByDescending(x => x.Amount)
                .ToListAsync();
            return result;
        }

        // Giao dịch gần đây
        public async Task<List<Transaction>> GetRecentTransactionsAsync(int userId, int count = 10)
        {
            await using var context = await _dbFactory.CreateDbContextAsync();
            return await context.Transactions
                .AsNoTracking()
                .Include(t => t.Category)
                .Where(t => t.UserId == userId)
                .OrderByDescending(t => t.CreatedAt)
                .Take(count)
                .ToListAsync();
        }

        // Giao dịch theo kỳ (Chỉ giao dịch THẬT)
        public async Task<List<Transaction>> GetTransactionsByPeriodAsync(int userId, DateTime startDate, DateTime endDate)
        {
            await using var context = await _dbFactory.CreateDbContextAsync();
            return await context.Transactions
                .AsNoTracking()
                .Include(t => t.Category)
                .Where(t => t.UserId == userId
                    && t.Date >= startDate
                    && t.Date <= endDate
                    && !t.IsAllocation
                    && !t.IsRefunded)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();
        }

        // Thống kê tổng quan
        public async Task<FinancialSummary> GetFinancialSummaryAsync(int userId)
        {
            var currentMonth = DateTime.Now.Month;
            var currentYear = DateTime.Now.Year;
            var lastMonth = currentMonth == 1 ? 12 : currentMonth - 1;
            var lastMonthYear = currentMonth == 1 ? currentYear - 1 : currentYear;

            var currentMonthIncome = await GetMonthlyIncomeAsync(userId, currentMonth, currentYear);
            var currentMonthExpense = await GetMonthlyExpenseAsync(userId, currentMonth, currentYear);
            var lastMonthIncome = await GetMonthlyIncomeAsync(userId, lastMonth, lastMonthYear);
            var lastMonthExpense = await GetMonthlyExpenseAsync(userId, lastMonth, lastMonthYear);

            await using var context = await _dbFactory.CreateDbContextAsync();
            var totalUnallocatedIncome = await context.Transactions
                .AsNoTracking()
                .Where(t => t.Type == TransactionType.Income &&
                            t.UserId == userId &&
                            t.BudgetId == null && // Chỉ tính thu nhập vào ví tổng (không từ hũ)
                            !t.IsAllocation && // CHỈ tính thu nhập thật, KHÔNG tính hoàn tiền hũ
                            !t.IsRefunded) // Loại trừ các transaction đã bị hoàn
                .SumAsync(t => t.Amount);

            var totalAllocatedToBudgets = await context.Budgets
                .AsNoTracking()
                .Where(b => b.UserId == userId)
                .SumAsync(b => (decimal?)b.Amount) ?? 0;

            // Chi tiêu không hũ (không thuộc hũ nào)
            var unallocatedExpense = await context.Transactions
                .AsNoTracking()
                .Where(t => t.Type == TransactionType.Expense &&
                            t.UserId == userId &&
                            t.BudgetId == null &&
                            !t.IsAllocation) // Chỉ tính chi tiêu không hũ
                .SumAsync(t => (decimal?)t.Amount) ?? 0;

            var totalUnallocatedExpense = unallocatedExpense + totalAllocatedToBudgets;

            var totalBalance = totalUnallocatedIncome - totalUnallocatedExpense;

            // Báo cáo Thu/Chi (chỉ tính giao dịch thật)
            var totalIncome = await GetTotalIncomeAsync(userId);
            var totalExpense = await GetTotalExpenseAsync(userId);
            var savingsRate = currentMonthIncome > 0 ? ((currentMonthIncome - currentMonthExpense) / currentMonthIncome) * 100 : 0;

            return new FinancialSummary
            {
                TotalBalance = totalBalance, // Số dư tổng (Unallocated)
                TotalIncome = totalIncome, // Báo cáo (Thu nhập thật)
                TotalExpense = totalExpense, // Báo cáo (Chi tiêu thật)
                MonthlyIncome = currentMonthIncome,
                MonthlyExpense = currentMonthExpense,
                SavingsRate = savingsRate,
                IncomeChangePercent = lastMonthIncome > 0 ? ((currentMonthIncome - lastMonthIncome) / lastMonthIncome) * 100 : 0,
                ExpenseChangePercent = lastMonthExpense > 0 ? ((currentMonthExpense - lastMonthExpense) / lastMonthExpense) * 100 : 0
            };
        }

        // Thu nhập tháng (Chỉ thu nhập THẬT)
        public async Task<decimal> GetMonthlyIncomeAsync(int userId, int? month = null, int? year = null)
        {
            var targetMonth = month ?? DateTime.Now.Month;
            var targetYear = year ?? DateTime.Now.Year;
            await using var context = await _dbFactory.CreateDbContextAsync();
            return await context.Transactions
                .AsNoTracking()
                .Where(t => t.Type == TransactionType.Income
                    && t.UserId == userId
                    && t.Date.Month == targetMonth
                    && t.Date.Year == targetYear
                    && !t.IsAllocation
                    && !t.IsRefunded)
                .SumAsync(t => t.Amount);
        }

        // Chi tiêu tháng (Chỉ chi tiêu THẬT)
        public async Task<decimal> GetMonthlyExpenseAsync(int userId, int? month = null, int? year = null)
        {
            var targetMonth = month ?? DateTime.Now.Month;
            var targetYear = year ?? DateTime.Now.Year;
            await using var context = await _dbFactory.CreateDbContextAsync();
            return await context.Transactions
                .AsNoTracking()
                .Where(t => t.Type == TransactionType.Expense
                    && t.UserId == userId
                    && t.Date.Month == targetMonth
                    && t.Date.Year == targetYear
                    && !t.IsAllocation)
                .SumAsync(t => t.Amount);
        }

        // Tổng thu nhập theo kỳ (Chỉ thu nhập THẬT)
        public async Task<decimal> GetTotalIncomeByPeriodAsync(int userId, DateTime startDate, DateTime endDate)
        {
            await using var context = await _dbFactory.CreateDbContextAsync();
            return await context.Transactions
                .AsNoTracking()
                .Where(t => t.Type == TransactionType.Income
                    && t.UserId == userId
                    && t.Date >= startDate
                    && t.Date <= endDate
                    && !t.IsAllocation
                    && !t.IsRefunded)
                .SumAsync(t => t.Amount);
        }

        // Tổng chi tiêu theo kỳ (Chỉ chi tiêu THẬT)
        public async Task<decimal> GetTotalExpenseByPeriodAsync(int userId, DateTime startDate, DateTime endDate)
        {
            await using var context = await _dbFactory.CreateDbContextAsync();
            return await context.Transactions
                .AsNoTracking()
                .Where(t => t.Type == TransactionType.Expense
                    && t.UserId == userId
                    && t.Date >= startDate
                    && t.Date <= endDate
                    && !t.IsAllocation)
                .SumAsync(t => t.Amount);
        }

        // Tổng thu nhập (Chỉ thu nhập THẬT)
        public async Task<decimal> GetTotalIncomeAsync(int userId)
        {
            await using var context = await _dbFactory.CreateDbContextAsync();
            return await context.Transactions
                .AsNoTracking()
                .Where(t => t.Type == TransactionType.Income
                    && t.UserId == userId
                    && !t.IsAllocation
                    && !t.IsRefunded)
                .SumAsync(t => t.Amount);
        }

        // Tổng chi tiêu (Chỉ chi tiêu THẬT)
        public async Task<decimal> GetTotalExpenseAsync(int userId)
        {
            await using var context = await _dbFactory.CreateDbContextAsync();
            return await context.Transactions
                .AsNoTracking()
                .Where(t => t.Type == TransactionType.Expense
                    && t.UserId == userId
                    && !t.IsAllocation)
                .SumAsync(t => t.Amount);
        }

        // Trung bình thu nhập tháng (Chỉ thu nhập THẬT)
        public async Task<decimal> GetAverageMonthlyIncomeAsync(int userId)
        {
            await using var context = await _dbFactory.CreateDbContextAsync();
            var monthlyIncomes = await context.Transactions
                .AsNoTracking()
                .Where(t => t.Type == TransactionType.Income
                    && t.UserId == userId
                    && !t.IsAllocation
                    && !t.IsRefunded)
                .GroupBy(t => new { t.Date.Year, t.Date.Month })
                .Select(g => g.Sum(t => t.Amount))
                .ToListAsync();
            return monthlyIncomes.Any() ? monthlyIncomes.Average() : 0;
        }

        // Trung bình chi tiêu tháng (Chỉ chi tiêu THẬT)
        public async Task<decimal> GetAverageMonthlyExpenseAsync(int userId)
        {
            await using var context = await _dbFactory.CreateDbContextAsync();
            var monthlyExpenses = await context.Transactions
                .AsNoTracking()
                .Where(t => t.Type == TransactionType.Expense
                    && t.UserId == userId
                    && !t.IsAllocation)
                .GroupBy(t => new { t.Date.Year, t.Date.Month })
                .Select(g => g.Sum(t => t.Amount))
                .ToListAsync();
            return monthlyExpenses.Any() ? monthlyExpenses.Average() : 0;
        }

        // Xóa dữ liệu
        public async Task<bool> ResetUserDataAsync(int userId)
        {
            try
            {
                await using var context = await _dbFactory.CreateDbContextAsync();
                context.Transactions.RemoveRange(await context.Transactions.Where(t => t.UserId == userId).ToListAsync());
                context.Budgets.RemoveRange(await context.Budgets.Where(b => b.UserId == userId).ToListAsync());
                context.Goals.RemoveRange(await context.Goals.Where(g => g.UserId == userId).ToListAsync());
                context.Categories.RemoveRange(await context.Categories.Where(c => c.UserId == userId).ToListAsync());
                await context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    public class CategorySpending
    {
        public string CategoryName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Color { get; set; } = "#2196F3";
    }

    public class FinancialSummary
    {
        public decimal TotalBalance { get; set; }
        public decimal TotalIncome { get; set; }
        public decimal TotalExpense { get; set; }
        public decimal MonthlyIncome { get; set; }
        public decimal MonthlyExpense { get; set; }
        public decimal SavingsRate { get; set; }
        public decimal IncomeChangePercent { get; set; }
        public decimal ExpenseChangePercent { get; set; }
    }
}