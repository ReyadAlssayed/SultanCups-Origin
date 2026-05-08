using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SultanCups.Models
{
    public class Production
    {
        [Key]
        public int production_id { get; set; }

        public int product_id { get; set; }

        public decimal box_cost { get; set; }

        public int box_count { get; set; }

        [Column(TypeName = "date")]
        public DateTime production_date { get; set; }

        public DateTime created_at { get; set; } = DateTime.UtcNow;

        [NotMapped]
        public decimal total_cost => box_cost * box_count;

        [ForeignKey("product_id")]
        public Product Product { get; set; } = null!;
        public string? notes { get; set; }
    }
}