using System.ComponentModel.DataAnnotations;

namespace WpfApp3.Models
{
    public class User
    {
        public int Id { get; set; }
        
        [Required]
        [StringLength(50)]
        public string Username { get; set; } = string.Empty;
        
        [Required]
        [StringLength(100)]
        public string Email { get; set; } = string.Empty;
        
        [Required]
        [StringLength(255)]
        public string PasswordHash { get; set; } = string.Empty;
        
        [StringLength(100)]
        public string? FullName { get; set; }
        
        [StringLength(500)]
        public string? Avatar { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? LastLoginAt { get; set; }
        public bool IsActive { get; set; } = true;
        
        public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
        public ICollection<Budget> Budgets { get; set; } = new List<Budget>();
        public ICollection<Goal> Goals { get; set; } = new List<Goal>();
        public ICollection<Category> Categories { get; set; } = new List<Category>();
    }
}




