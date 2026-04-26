using System.ComponentModel.DataAnnotations;

namespace WpfApp3.Models
{
    public class Category
    {
        public int Id { get; set; }
        
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;
        
        [StringLength(7)]
        public string Color { get; set; } = "#2196F3";
        
        [StringLength(50)]
        public string Icon { get; set; } = "Category";
        
        public TransactionType Type { get; set; }
        
        public int UserId { get; set; }
        public User? User { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
        
        public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    }
}
