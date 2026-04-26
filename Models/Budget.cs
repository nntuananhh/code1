using System.ComponentModel.DataAnnotations;

namespace WpfApp3.Models
{
    //Hu chi tieu
    public class Budget 
    {
        public int Id { get; set; }
        
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;
        
        [Required]
        public decimal Amount { get; set; }
        
        [Required]
        public decimal SpentAmount { get; set; } = 0;
        
        [Required]
        public DateTime StartDate { get; set; }
        
        [Required]
        public DateTime EndDate { get; set; }
        
        public int CategoryId { get; set; }
        public Category? Category { get; set; }
        
        public int UserId { get; set; }
        public User? User { get; set; }
        
        public bool IsActive { get; set; } = true;
        
        [StringLength(500)]
        public string? Notes { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}
