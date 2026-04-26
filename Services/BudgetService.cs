using Microsoft.EntityFrameworkCore;
using WpfApp3.Data;
using WpfApp3.Models;

namespace WpfApp3.Services
{
    public class BudgetService : IBudgetService
    {
        private readonly IDbContextFactory<ExpenseDbContext> _dbFactory;
        private readonly IDataService _dataService;

        public BudgetService(IDbContextFactory<ExpenseDbContext> dbFactory, IDataService dataService)
        {
            _dbFactory = dbFactory;
            _dataService = dataService;
        }

        public async Task<List<Budget>> GetBudgetsAsync(int userId)
        {
            await using var context = await _dbFactory.CreateDbContextAsync();
            return await context.Budgets
                .AsNoTracking()
                .Include(b => b.Category)
                .Where(b => b.UserId == userId)
                .OrderBy(b => b.Category != null ? b.Category.Name : "")
                .ToListAsync();
        }

        public async Task<Budget?> GetBudgetByIdAsync(int budgetId, int userId)
        {
            await using var context = await _dbFactory.CreateDbContextAsync();
            return await context.Budgets
                .Include(b => b.Category)
                .FirstOrDefaultAsync(b => b.Id == budgetId && b.UserId == userId);
        }

        public async Task<Budget?> GetBudgetByCategoryAndDateAsync(int categoryId, int userId, DateTime date)
        {
            await using var context = await _dbFactory.CreateDbContextAsync();
            return await context.Budgets
                .FirstOrDefaultAsync(b => b.CategoryId == categoryId
                    && b.UserId == userId
                    && b.IsActive
                    && date >= b.StartDate
                    && date <= b.EndDate);
        }

        public async Task<bool> CreateBudgetAsync(Budget budget, int userId)
        {
            try
            {
                await using var context = await _dbFactory.CreateDbContextAsync();
                // Kiểm tra xem đã có budget cho category và khoảng thời gian này chưa
                var existingBudget = await context.Budgets
                    .FirstOrDefaultAsync(b => b.CategoryId == budget.CategoryId
                        && b.UserId == userId
                        && b.StartDate <= budget.EndDate
                        && b.EndDate >= budget.StartDate);

                if (existingBudget != null)
                    return false;
                // Kiểm tra số dư tổng
                var summary = await _dataService.GetFinancialSummaryAsync(userId);
                if (budget.Amount > summary.TotalBalance)
                    return false;

                budget.UserId = userId;
                budget.SpentAmount = 0;
                budget.IsActive = true;
                budget.CreatedAt = DateTime.Now;
                budget.UpdatedAt = DateTime.Now;
                context.Budgets.Add(budget);
                await context.SaveChangesAsync();

                // Tạo transaction chi tiêu để trừ tiền từ số dư tổng
                var category = await context.Categories.FindAsync(budget.CategoryId);
                var budgetTransaction = new Transaction
                {
                    Amount = budget.Amount,
                    Description = $"Chuyển tiền vào hũ chi tiêu '{category?.Name ?? "Danh mục"}'",
                    Type = TransactionType.Expense,
                    CategoryId = budget.CategoryId,
                    UserId = userId,
                    Date = DateTime.Now,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now,
                    BudgetId = null, // Trừ từ tổng số dư, không trừ từ hũ
                    IsAllocation = true // Đánh dấu là giao dịch phân bổ
                };

                context.Transactions.Add(budgetTransaction);
                await context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }
        public async Task<bool> UpdateBudgetAsync(int budgetId, decimal newAmount, DateTime startDate, DateTime endDate, bool isActive, int userId)
        {
            try
            {
                await using var context = await _dbFactory.CreateDbContextAsync();
                var budget = await context.Budgets
                    .FirstOrDefaultAsync(b => b.Id == budgetId && b.UserId == userId);
                if (budget == null)
                    return false;

                // Kiểm tra xem đã có budget khác cho category và khoảng thời gian này chưa
                var existingBudget = await context.Budgets
                    .FirstOrDefaultAsync(b => b.Id != budgetId
                        && b.CategoryId == budget.CategoryId
                        && b.UserId == userId
                        && b.StartDate <= endDate
                        && b.EndDate >= startDate);
                if (existingBudget != null)
                    return false;

                // Xử lý việc tăng/giam mức chi tiêu
                var oldAmount = budget.Amount;
                var amountDifference = newAmount - oldAmount;
                var category = await context.Categories.FindAsync(budget.CategoryId);

                // Tính số tiền đã chi thực tế
                var actualSpentAmount = await CalculateActualSpentAmountAsync(budgetId);

                if (amountDifference > 0) // tăng ngân sách 
                {
                    // Kiểm tra số dư tổng
                    var summary = await _dataService.GetFinancialSummaryAsync(userId);
                    if (amountDifference > summary.TotalBalance)
                        return false;

                    var budgetTransaction = new Transaction
                    {
                        Amount = amountDifference,
                        Description = $"Tăng ngân sách cho {category?.Name}",
                        Type = TransactionType.Expense,
                        CategoryId = budget.CategoryId,
                        UserId = userId,
                        Date = DateTime.Now,
                        CreatedAt = DateTime.Now,
                        IsAllocation = true // Đánh dấu là giao dịch phân bổ
                    };
                    context.Transactions.Add(budgetTransaction);
                }
                else if (amountDifference < 0) // giảm ngân sách 
                {
                    // Validation: Không thể giảm ngân sách xuống thấp hơn số tiền đã chi
                    if (newAmount < actualSpentAmount)
                    {
                        return false;
                    }

                    var refundAmount = -amountDifference;
                    var refundTransaction = new Transaction
                    {
                        Amount = refundAmount,
                        Description = $"Giảm ngân sách {category?.Name}",
                        Type = TransactionType.Income,
                        CategoryId = budget.CategoryId,
                        UserId = userId,
                        Date = DateTime.Now,
                        CreatedAt = DateTime.Now,
                        BudgetId = null,
                        IsAllocation = true // Đánh dấu là giao dịch phân bổ
                    };
                    context.Transactions.Add(refundTransaction);
                }

                budget.Amount = newAmount;
                budget.StartDate = startDate;
                budget.EndDate = endDate;
                budget.IsActive = isActive;
                budget.UpdatedAt = DateTime.Now;

                context.Budgets.Update(budget);
                await context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<(bool Success, string Message)> DeleteBudgetAsync(int budgetId, int userId)
        {
            try
            {
                await using var context = await _dbFactory.CreateDbContextAsync();
                var budget = await context.Budgets
                    .Include(b => b.Category)
                    .FirstOrDefaultAsync(b => b.Id == budgetId && b.UserId == userId);
                if (budget == null)
                    return (false, "Không tìm thấy hũ chi tiêu để xóa.");

                // Tính toán số tiền đã chi thực tế
                var actualSpentAmount = await CalculateActualSpentAmountAsync(budgetId);

                // Tính số tiền còn lại
                var remainingAmount = budget.Amount - actualSpentAmount;

                // hoàn tiền nếu còn thừa (remainingAmount > 0)
                if (remainingAmount > 0)
                {
                    // Tìm danh mục thu nhập
                    var incomeCategory = await context.Categories
                        .FirstOrDefaultAsync(c => c.Type == TransactionType.Income && c.UserId == userId && c.Name == "Hoàn trả");
                    if (incomeCategory == null)
                    {
                        incomeCategory = new Category
                        {
                            Name = "Hoàn trả",
                            Type = TransactionType.Income,
                            Color = "#4CAF50",
                            Icon = "Refresh",
                            UserId = userId,
                            CreatedAt = DateTime.Now
                        };
                        context.Categories.Add(incomeCategory);
                        await context.SaveChangesAsync(); // Lưu để lấy Id
                    }

                    var refundTransaction = new Transaction
                    {
                        Amount = remainingAmount,
                        Description = $"Hoàn trả từ hũ chi tiêu '{budget.Category?.Name ?? "Không xác định"}'",
                        Type = TransactionType.Income,
                        CategoryId = incomeCategory.Id,
                        UserId = userId,
                        Date = DateTime.Now,
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now,
                        BudgetId = null,        // Ghi vào tổng 
                        IsRefunded = false,     // Không đánh dấu IsRefunded để được tính vào tổng số dư
                        IsAllocation = true     // không tính vào thu nhập tháng, nhưng vẫn được tính vào tổng số dư
                    };

                    context.Transactions.Add(refundTransaction);
                }

                // Đánh dấu các giao dịch liên quan
                var relatedTransactions = await context.Transactions
                    .Where(t => t.BudgetId == budgetId)
                    .ToListAsync();

                foreach (var transaction in relatedTransactions)
                {
                    transaction.IsRefunded = true; // Đánh dấu các chi tiêu cũ là "đã hoàn"
                }

                // Xóa budget
                context.Budgets.Remove(budget);
                await context.SaveChangesAsync();
                return (true, "Hũ chi tiêu đã được xóa thành công!");
            }
            catch (Exception ex)
            {
                return (false, $"Lỗi khi xóa hũ chi tiêu: {ex.Message}");
            }
        }

        //update so tien da chi
        public async Task<bool> UpdateBudgetSpentAmountAsync(int categoryId, int userId, decimal amount, DateTime? transactionDate = null)
        {
            try
            {
                await using var context = await _dbFactory.CreateDbContextAsync();

                var date = transactionDate ?? DateTime.Now;
                var budget = await context.Budgets
                    .FirstOrDefaultAsync(b => b.CategoryId == categoryId
                        && b.UserId == userId
                        && b.IsActive
                        && date >= b.StartDate
                        && date <= b.EndDate);

                if (budget != null)
                {
                    budget.SpentAmount += amount;
                    budget.UpdatedAt = DateTime.Now;
                    context.Budgets.Update(budget);
                    await context.SaveChangesAsync();
                    return true;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        public async Task<BudgetAlertResult> CheckBudgetAlertsAsync(int userId)
        {
            var result = new BudgetAlertResult();

            try
            {
                var budgets = await GetBudgetsAsync(userId);

                foreach (var budget in budgets.Where(b => b.IsActive))
                {
                    var percentage = budget.Amount > 0 ? (budget.SpentAmount / budget.Amount) * 100 : 0;

                    if (percentage >= 100)
                    {
                        result.OverBudgetItems.Add($"• {budget.Category?.Name ?? "Không có danh mục"}: Đã vượt quá {percentage:F1}%");
                    }
                    else if (percentage >= 80)
                    {
                        result.WarningItems.Add($"• {budget.Category?.Name ?? "Không có danh mục"}: Đã chi {percentage:F1}%");
                    }
                }
            }
            catch
            {
            }

            return result;
        }

        public async Task<decimal> CalculateActualSpentAmountAsync(int budgetId)
        {
            await using var context = await _dbFactory.CreateDbContextAsync();
            // Chỉ tính tổng các chi tiêu thực tế (không phải phân bổ) thuộc hũ này
            return await context.Transactions
                .Where(t => t.BudgetId == budgetId &&
                            t.Type == TransactionType.Expense &&
                            !t.IsAllocation) // Đảm bảo chỉ tính chi tiêu thật
                .SumAsync(t => t.Amount);
        }
    }
}