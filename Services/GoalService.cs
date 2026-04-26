using Microsoft.EntityFrameworkCore;
using WpfApp3.Data;
using WpfApp3.Models;

namespace WpfApp3.Services
{
    public class GoalService : IGoalService
    {
        private readonly IDbContextFactory<ExpenseDbContext> _dbFactory;
        private readonly IDataService _dataService;

        public GoalService(IDbContextFactory<ExpenseDbContext> dbFactory, IDataService dataService)
        {
            _dbFactory = dbFactory;
            _dataService = dataService;
        }

        public async Task<List<Goal>> GetGoalsAsync(int userId)
        {
            await using var context = await _dbFactory.CreateDbContextAsync();
            return await context.Goals
                .AsNoTracking()
                .Where(g => g.UserId == userId)
                .OrderByDescending(g => g.TargetDate)
                .ToListAsync();
        }

        public async Task<Goal?> GetGoalByIdAsync(int goalId, int userId)
        {
            await using var context = await _dbFactory.CreateDbContextAsync();
            return await context.Goals
                .FirstOrDefaultAsync(g => g.Id == goalId && g.UserId == userId);
        }

        public async Task<bool> CreateGoalAsync(Goal goal, int userId)
        {
            try
            {
                await using var context = await _dbFactory.CreateDbContextAsync();

                goal.UserId = userId;
                goal.CreatedAt = DateTime.Now;
                goal.UpdatedAt = DateTime.Now;

                context.Goals.Add(goal);
                await context.SaveChangesAsync();

                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> UpdateGoalAsync(int goalId, string name, string? description, decimal targetAmount, DateTime targetDate, int userId)
        {
            try
            {
                await using var context = await _dbFactory.CreateDbContextAsync();

                var goal = await context.Goals
                    .FirstOrDefaultAsync(g => g.Id == goalId && g.UserId == userId);

                if (goal == null)
                    return false;

                goal.Name = name;
                goal.Description = description;
                goal.TargetAmount = targetAmount;
                goal.TargetDate = targetDate;
                goal.UpdatedAt = DateTime.Now;

                if (goal.CurrentAmount >= targetAmount && goal.CompletedDate == null)
                {
                    goal.CompletedDate = DateTime.Now;
                }
                else if (goal.CurrentAmount < targetAmount && goal.CompletedDate != null)
                {
                    goal.CompletedDate = null;
                }

                context.Goals.Update(goal);
                await context.SaveChangesAsync();

                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> DeleteGoalAsync(int goalId, int userId)
        {
            try
            {
                await using var context = await _dbFactory.CreateDbContextAsync();

                var goal = await context.Goals
                    .FirstOrDefaultAsync(g => g.Id == goalId && g.UserId == userId);

                if (goal == null)
                    return false;

                context.Goals.Remove(goal);
                await context.SaveChangesAsync();

                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<(bool Success, string Message)> AddMoneyToGoalAsync(int goalId, decimal amount, int userId)
        {
            try
            {
                // Kiểm tra số dư tài khoản
                var summary = await _dataService.GetFinancialSummaryAsync(userId);

                if (amount > summary.TotalBalance)
                {
                    return (false, $"Số dư tài khoản không đủ!\n\n" +
                                  $"Số dư hiện tại: {summary.TotalBalance:N0} ₫\n" +
                                  $"Số tiền muốn thêm: {amount:N0} ₫\n" +
                                  $"Còn thiếu: {amount - summary.TotalBalance:N0} ₫");
                }

                await using var context = await _dbFactory.CreateDbContextAsync();

                var goal = await context.Goals
                    .FirstOrDefaultAsync(g => g.Id == goalId && g.UserId == userId);

                if (goal == null)
                    return (false, "Không tìm thấy mục tiêu.");

                var newCurrentAmount = goal.CurrentAmount + amount;

                // Tìm hoặc tạo category "Mục tiêu"
                var goalCategory = await context.Categories
                    .FirstOrDefaultAsync(c => c.Name == "Mục tiêu" && c.UserId == userId);

                if (goalCategory == null)
                {
                    goalCategory = new Category
                    {
                        Name = "Mục tiêu",
                        Color = "#FF9800",
                        Icon = "Target",
                        Type = TransactionType.Expense,
                        UserId = userId,
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    };
                    context.Categories.Add(goalCategory);
                    await context.SaveChangesAsync();
                }

                // Tạo transaction chi tiêu cho việc thêm tiền vào mục tiêu
                var transaction = new Transaction
                {
                    Amount = amount,
                    Description = $"Thêm tiền vào mục tiêu: {goal.Name}",
                    Type = TransactionType.Expense,
                    CategoryId = goalCategory.Id,
                    UserId = userId,
                    Date = DateTime.Now,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };
                context.Transactions.Add(transaction);

                // Cập nhật mục tiêu
                goal.CurrentAmount = newCurrentAmount;
                goal.UpdatedAt = DateTime.Now;

                // Kiểm tra xem mục tiêu có hoàn thành không
                if (newCurrentAmount >= goal.TargetAmount && goal.CompletedDate == null)
                {
                    goal.CompletedDate = DateTime.Now;
                }

                context.Goals.Update(goal);
                await context.SaveChangesAsync();

                var message = newCurrentAmount >= goal.TargetAmount
                    ? $"Chúc mừng! Bạn đã hoàn thành mục tiêu '{goal.Name}'!"
                    : $"Đã thêm {amount:N0} ₫ vào mục tiêu '{goal.Name}'!";

                return (true, message);
            }
            catch (Exception ex)
            {
                return (false, $"Lỗi khi thêm tiền vào mục tiêu: {ex.Message}");
            }
        }

        public Task<string> GetGoalStatusAsync(Goal goal)
        {
            return Task.FromResult(GetGoalStatus(goal));
        }

        public string GetGoalStatus(Goal goal)
        {
            if (goal.IsCompleted)
                return "Hoàn thành";

            var now = DateTime.Now;
            if (goal.TargetDate < now)
                return "Quá hạn";

            var daysLeft = (goal.TargetDate - now).Days;
            if (daysLeft <= 7)
                return "Sắp hết hạn";

            return "Đang thực hiện";
        }
    }
}











