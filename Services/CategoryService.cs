using Microsoft.EntityFrameworkCore;
using WpfApp3.Data;
using WpfApp3.Models;

namespace WpfApp3.Services
{
    public class CategoryService : ICategoryService
    {
        private readonly IDbContextFactory<ExpenseDbContext> _dbFactory;

        public CategoryService(IDbContextFactory<ExpenseDbContext> dbFactory)
        {
            _dbFactory = dbFactory;
        }

        public async Task<List<Category>> GetCategoriesAsync(int userId, TransactionType? type = null)
        {
            await using var context = await _dbFactory.CreateDbContextAsync();
            var query = context.Categories
                .AsNoTracking()
                .Where(c => c.UserId == userId);

            if (type.HasValue)
            {
                query = query.Where(c => c.Type == type.Value);
            }

            return await query.OrderBy(c => c.Name).ToListAsync();
        }
        public async Task<bool> CreateCategoryAsync(Category category, int userId)
        {
            try
            {
                await using var context = await _dbFactory.CreateDbContextAsync();
                var exists = await context.Categories
                    .AnyAsync(c => c.Name == category.Name &&
                                   c.Type == category.Type &&
                                   c.UserId == userId);

                if (exists)
                {
                    return false;
                }
                category.UserId = userId;
                category.CreatedAt = DateTime.Now;
                category.UpdatedAt = DateTime.Now;
                context.Categories.Add(category);
                await context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> UpdateCategoryAsync(int categoryId, string name, TransactionType type, string color, string icon, int userId)
        {
            try
            {
                await using var context = await _dbFactory.CreateDbContextAsync();
                // Kiểm tra category có tồn tại và thuộc về user
                var category = await context.Categories
                    .FirstOrDefaultAsync(c => c.Id == categoryId && c.UserId == userId);
                if (category == null)
                    return false;

                // Kiểm tra tên trùng với category khác cùng type
                var exists = await context.Categories
                    .AnyAsync(c => c.Name == name &&
                                   c.Type == type &&
                                   c.UserId == userId &&
                                   c.Id != categoryId);

                if (exists)
                    return false;

                category.Name = name;
                category.Type = type;
                category.Color = color;
                category.Icon = icon;
                category.UpdatedAt = DateTime.Now;
                context.Categories.Update(category);
                await context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}