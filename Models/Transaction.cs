using System.ComponentModel.DataAnnotations;

namespace WpfApp3.Models
{
    public class Transaction
    {
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string Description { get; set; } = string.Empty;

        [Required]
        public decimal Amount { get; set; }

        [Required]
        public DateTime Date { get; set; }

        [Required]
        public TransactionType Type { get; set; }

        public int CategoryId { get; set; }
        public Category? Category { get; set; }

        public int? BudgetId { get; set; }
        public Budget? Budget { get; set; }

        public int UserId { get; set; }
        public User? User { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        public bool IsRefunded { get; set; } = false;
        public bool IsAllocation { get; set; } = false;
    }

    public enum TransactionType
    {
        Income = 1,
        Expense = 2
    }
}