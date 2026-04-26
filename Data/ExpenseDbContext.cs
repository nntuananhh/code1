using Microsoft.EntityFrameworkCore;
using WpfApp3.Models;

namespace WpfApp3.Data
{
    public class ExpenseDbContext : DbContext
    {
        public ExpenseDbContext(DbContextOptions<ExpenseDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Transaction> Transactions { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Budget> Budgets { get; set; }
        public DbSet<Goal> Goals { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Username).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Email).IsRequired().HasMaxLength(100);
                entity.Property(e => e.PasswordHash).IsRequired().HasMaxLength(255);
                entity.Property(e => e.FullName).HasMaxLength(100);
                entity.HasIndex(e => e.Username).IsUnique();
                entity.HasIndex(e => e.Email).IsUnique();
            });

            modelBuilder.Entity<Transaction>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Amount).HasPrecision(18, 2);
                entity.Property(e => e.Description).IsRequired().HasMaxLength(200);

                // xoa category -> xoa transactions
                entity.HasOne(e => e.Category)
                      .WithMany(c => c.Transactions)
                      .HasForeignKey(e => e.CategoryId)
                      .OnDelete(DeleteBehavior.Cascade);

                // xóa budget khong lam gi
                entity.HasOne(e => e.Budget)
                      .WithMany()
                      .HasForeignKey(e => e.BudgetId)
                      .OnDelete(DeleteBehavior.NoAction);

                // xoa user khong lam gi
                entity.HasOne(e => e.User)
                      .WithMany(u => u.Transactions)
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(DeleteBehavior.NoAction);
            });

            // --- Cấu hình Category ---
            modelBuilder.Entity<Category>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Color).HasMaxLength(7);
                entity.Property(e => e.Icon).HasMaxLength(50);

                // xoa user xoa category
                entity.HasOne(e => e.User)
                      .WithMany(u => u.Categories)
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // --- Cấu hình Budget ---
            modelBuilder.Entity<Budget>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Amount).HasPrecision(18, 2);
                entity.Property(e => e.SpentAmount).HasPrecision(18, 2);
                entity.Property(e => e.Notes).HasMaxLength(500);

                // xoa category xoa budget
                entity.HasOne(e => e.Category)
                      .WithMany()
                      .HasForeignKey(e => e.CategoryId)
                      .OnDelete(DeleteBehavior.Cascade);

                // xoa user khong lam gi
                entity.HasOne(e => e.User)
                      .WithMany(u => u.Budgets)
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(DeleteBehavior.NoAction);
            });

            // --- Cấu hình Goal ---
            modelBuilder.Entity<Goal>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Description).HasMaxLength(500);
                entity.Property(e => e.TargetAmount).HasPrecision(18, 2);
                entity.Property(e => e.CurrentAmount).HasPrecision(18, 2);
                entity.Property(e => e.Color).HasMaxLength(7);

                // xoa user xoa goal
                entity.HasOne(e => e.User)
                      .WithMany(u => u.Goals)
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
