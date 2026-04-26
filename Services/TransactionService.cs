using Microsoft.EntityFrameworkCore;
using WpfApp3.Data;
using WpfApp3.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WpfApp3.Services
{
    public class TransactionService : ITransactionService
    {
        private readonly IDbContextFactory<ExpenseDbContext> _dbFactory;
        private readonly IDataService _dataService;
        private readonly IBudgetService _budgetService;

        public TransactionService(IDbContextFactory<ExpenseDbContext> dbFactory, IDataService dataService, IBudgetService budgetService)
        {
            _dbFactory = dbFactory;
            _dataService = dataService;
            _budgetService = budgetService;
        }

        public async Task<List<Transaction>> GetTransactionsAsync(int userId, TransactionType? type = null)
        {
            await using var context = await _dbFactory.CreateDbContextAsync();
            var query = context.Transactions
                .AsNoTracking()
                .Include(t => t.Category)
                .Where(t => t.UserId == userId);

            if (type.HasValue)
            {
                query = query.Where(t => t.Type == type.Value);
            }

            // Lọc bỏ các giao dịch phân bổ khỏi danh sách chung
            query = query.Where(t => !t.IsAllocation);

            return await query.OrderByDescending(t => t.Date).ToListAsync();
        }

        public async Task<Transaction?> GetTransactionByIdAsync(int transactionId, int userId)
        {
            await using var context = await _dbFactory.CreateDbContextAsync();
            return await context.Transactions
                .Include(t => t.Category)
                .FirstOrDefaultAsync(t => t.Id == transactionId && t.UserId == userId);
        }

        public async Task<(bool Success, string Message)> CreateTransactionAsync(Transaction transaction, int userId)
        {
            try
            {
                if (transaction.Amount <= 0)
                    return (false, "Số tiền phải lớn hơn 0.");
                if (string.IsNullOrWhiteSpace(transaction.Description))
                    return (false, "Vui lòng nhập mô tả giao dịch.");

                if (transaction.Type == TransactionType.Expense)
                {
                    var validation = await ValidateExpenseTransactionAsync(transaction.Amount, transaction.CategoryId, userId, transaction.Date);
                    if (!validation.CanAfford)
                        return (false, validation.Message);
                }

                await using var context = await _dbFactory.CreateDbContextAsync();

                transaction.UserId = userId;
                transaction.CreatedAt = DateTime.Now;
                transaction.UpdatedAt = DateTime.Now;

                if (transaction.Type == TransactionType.Expense)
                {
                    var budget = await _budgetService.GetBudgetByCategoryAndDateAsync(transaction.CategoryId, userId, transaction.Date);

                    if (budget != null)
                    {
                        transaction.BudgetId = budget.Id;
                        // Cập nhật số tiền đã chi cho hũ
                        await _budgetService.UpdateBudgetSpentAmountAsync(transaction.CategoryId, userId, transaction.Amount, transaction.Date);
                    }
                }

                context.Transactions.Add(transaction);
                await context.SaveChangesAsync();

                return (true, "Giao dịch đã được thêm thành công!");
            }
            catch (Exception ex)
            {
                return (false, $"Lỗi khi thêm giao dịch: {ex.Message}");
            }
        }

        public async Task<(bool Success, string Message)> UpdateTransactionAsync(int transactionId, decimal amount, string description, int categoryId, DateTime date, int userId)
        {
            try
            {
                await using var context = await _dbFactory.CreateDbContextAsync();

                var transaction = await context.Transactions
                    .FirstOrDefaultAsync(t => t.Id == transactionId && t.UserId == userId);

                if (transaction == null)
                    return (false, "Không tìm thấy giao dịch để cập nhật.");

                if (amount <= 0)
                    return (false, "Số tiền phải lớn hơn 0.");
                if (string.IsNullOrWhiteSpace(description))
                    return (false, "Vui lòng nhập mô tả giao dịch.");

                // Lưu thông tin cũ để hoàn trả cho hũ cũ
                var oldAmount = transaction.Amount;
                var oldCategoryId = transaction.CategoryId;
                var oldBudgetId = transaction.BudgetId;
                var oldDate = transaction.Date;
                var oldType = transaction.Type;

                if (transaction.Type == TransactionType.Expense)
                {
                    var validation = await ValidateExpenseTransactionUpdateAsync(transactionId, amount, categoryId, userId, date);
                    if (!validation.CanAfford)
                        return (false, validation.Message);
                }

                if (oldType == TransactionType.Expense && oldBudgetId.HasValue)
                {
                    // Hoàn trả lại số tiền cũ cho hũ cũ
                    await _budgetService.UpdateBudgetSpentAmountAsync(oldCategoryId, userId, -oldAmount, oldDate);
                }

                // Cập nhật thông tin giao dịch
                transaction.Amount = amount;
                transaction.Description = description.Trim();
                transaction.CategoryId = categoryId;
                transaction.Date = date;
                transaction.UpdatedAt = DateTime.Now;

                // Xử lý ngân sách cho chi tiêu (nếu vẫn là Expense)
                if (transaction.Type == TransactionType.Expense)
                {
                    // Tìm hũ mới
                    var budget = await _budgetService.GetBudgetByCategoryAndDateAsync(categoryId, userId, date);

                    if (budget != null)
                    {
                        transaction.BudgetId = budget.Id;
                        // Cộng số tiền MỚI vào hũ MỚI
                        await _budgetService.UpdateBudgetSpentAmountAsync(categoryId, userId, amount, date);
                    }
                    else
                    {
                        // Chi tiêu này không thuộc hũ nào
                        transaction.BudgetId = null;
                    }
                }
                else
                {
                    // Nếu đổi thành Thu nhập, xóa BudgetId
                    transaction.BudgetId = null;
                }
                await context.SaveChangesAsync();

                return (true, "Giao dịch đã được cập nhật thành công!");
            }
            catch (Exception ex)
            {
                return (false, $"Lỗi khi cập nhật giao dịch: {ex.Message}");
            }
        }

        public async Task<(bool Success, string Message)> DeleteTransactionAsync(int transactionId, int userId)
        {
            try
            {
                await using var context = await _dbFactory.CreateDbContextAsync();

                var transaction = await context.Transactions.FindAsync(transactionId);
                if (transaction == null || transaction.UserId != userId)
                    return (false, "Không tìm thấy giao dịch để xóa.");

                // Nếu là chi tiêu có hũ, hoàn tiền lại cho hũ
                if (transaction.Type == TransactionType.Expense && transaction.BudgetId.HasValue)
                {
                    await _budgetService.UpdateBudgetSpentAmountAsync(transaction.CategoryId, userId, -transaction.Amount, transaction.Date);
                }

                context.Transactions.Remove(transaction);
                await context.SaveChangesAsync();

                return (true, "Giao dịch đã được xóa thành công!");
            }
            catch (Exception ex)
            {
                return (false, $"Lỗi khi xóa giao dịch: {ex.Message}");
            }
        }

        public async Task<(bool CanAfford, string Message)> ValidateExpenseTransactionAsync(decimal amount, int categoryId, int userId, DateTime date)
        {
            try
            {
                // Kiểm tra hũ cho danh mục này
                var budget = await _budgetService.GetBudgetByCategoryAndDateAsync(categoryId, userId, date);

                if (budget != null)
                {
                    // CÓ HŨ: Kiểm tra số dư hũ
                    var remainingAmount = budget.Amount - budget.SpentAmount;

                    if (amount > remainingAmount)
                    {
                        return (false, $"Không đủ tiền trong ngân sách '{budget.Category?.Name}'!\n\n" +
                                       $"Số tiền còn lại trong hũ: {remainingAmount:N0} ₫\n" +
                                       $"Số tiền muốn chi tiêu: {amount:N0} ₫");
                    }
                }
                else
                {
                    // KHÔNG CÓ HŨ: Kiểm tra "Ví tổng" (TotalBalance)
                    var summary = await _dataService.GetFinancialSummaryAsync(userId);
                    if (amount > summary.TotalBalance)
                    {
                        return (false, $"Số dư tài khoản không đủ!\n\n" +
                                       $"Số dư hiện tại: {summary.TotalBalance:N0} ₫\n" +
                                       $"Số tiền chi tiêu: {amount:N0} ₫");
                    }
                }
                return (true, string.Empty);
            }
            catch (Exception ex)
            {
                return (false, $"Lỗi khi kiểm tra số dư: {ex.Message}");
            }
        }
        public async Task<(bool CanAfford, string Message)> ValidateExpenseTransactionUpdateAsync(int transactionId, decimal newAmount, int newCategoryId, int userId, DateTime newDate)
        {
            try
            {
                await using var context = await _dbFactory.CreateDbContextAsync();
                var transaction = await context.Transactions.AsNoTracking().FirstOrDefaultAsync(t => t.Id == transactionId && t.UserId == userId);
                if (transaction == null) return (false, "Không tìm thấy giao dịch.");

                // Kiểm tra hũ MỚI
                var budget = await _budgetService.GetBudgetByCategoryAndDateAsync(newCategoryId, userId, newDate);

                if (budget != null)
                {
                    // CÓ HŨ MỚI: Tính toán số dư của hũ đó
                    var oldAmount = transaction.Amount;
                    var oldCategoryId = transaction.CategoryId;
                    var oldBudgetId = transaction.BudgetId;

                    // Lấy số tiền đã chi HIỆN TẠI của hũ MỚI
                    var currentBudgetSpent = budget.SpentAmount;

                    // Nếu hũ MỚI cũng là hũ CŨ, ta phải "hoàn" lại số tiền cũ trước khi tính
                    if (oldBudgetId.HasValue && oldBudgetId.Value == budget.Id)
                    {
                        currentBudgetSpent -= oldAmount;
                    }

                    var remainingAmount = budget.Amount - currentBudgetSpent;

                    if (newAmount > remainingAmount)
                    {
                        return (false, $"Không đủ tiền trong ngân sách '{budget.Category?.Name}'!\n" +
                                       $"Số tiền còn lại trong hũ: {remainingAmount:N0} ₫\n" +
                                       $"Số tiền muốn chi tiêu: {newAmount:N0} ₫");
                    }
                }
                else
                {
                    // KHÔNG CÓ HŨ MỚI (chi tiêu vào "Ví tổng")
                    var oldAmount = transaction.Amount;
                    var amountDifference = newAmount - oldAmount; // Số tiền chênh lệch

                    // Nếu chi tiêu nhiều hơn trước (hoặc chuyển từ hũ ra ví tổng)
                    if (amountDifference > 0)
                    {
                        var summary = await _dataService.GetFinancialSummaryAsync(userId);

                        // Kiểm tra xem "Ví tổng" có đủ cho phần chênh lệch không
                        if (amountDifference > summary.TotalBalance)
                        {
                            return (false, $"Số dư tài khoản không đủ!\n\n" +
                                           $"Số dư hiện tại: {summary.TotalBalance:N0} ₫\n" +
                                           $"Số tiền tăng thêm: {amountDifference:N0} ₫");
                        }
                    }
                }
                return (true, string.Empty);
            }
            catch (Exception ex)
            {
                return (false, $"Lỗi khi kiểm tra số dư: {ex.Message}");
            }
        }
    }
}